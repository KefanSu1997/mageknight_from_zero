"""CPU protocol fixture only; non-ruin opening search is never a training filter."""
import json
from pathlib import Path
import subprocess
import time
import argparse
from copy import deepcopy
from solo_trajectory_audit import audit_native_journal
from solo_public_records import policy_records, restore as restore_public_records
from solo_candidate_entities import deed_entities, lifecycle_entities, reward_entities
from solo_candidate_entities import candidate_entities

ROOT = Path(__file__).resolve().parents[2]
DLL = ROOT / 'tools/SoloConquestRl/bin/Debug/net8.0/SoloConquestRl.dll'


def run(input_assets=None, public_schema=None):
    processes = []
    deed_candidates_checked = 0
    lifecycle_candidates_checked = {}
    all_candidates_checked = 0
    prepared_states = 0
    input_builder = None
    if input_assets is not None:
        from solo_public_schema import PublicSchema
        from solo_text_assets import SoloTextAssets
        from solo_policy_inputs import SoloPolicyInputs
        input_builder=SoloPolicyInputs(PublicSchema(public_schema),SoloTextAssets(input_assets,ROOT))
    def start():
        p = subprocess.Popen(['dotnet', str(DLL), str(ROOT)], stdin=subprocess.PIPE,
                             stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, encoding='utf-8')
        processes.append(p)
        return p
    def ask(p, **request):
        nonlocal deed_candidates_checked
        nonlocal all_candidates_checked
        nonlocal prepared_states
        p.stdin.write(json.dumps(request)+'\n'); p.stdin.flush()
        line = p.stdout.readline()
        if not line:
            raise RuntimeError('Native adapter exited: '+p.stderr.read())
        response = json.loads(line)
        state = response.get('result')
        if isinstance(state, dict) and 'state' in state:
            state = state['state']
        if response.get('ok') and isinstance(state, dict) and 'observation' in state and 'commands' in state:
            if input_builder is not None:
                prepared=input_builder.prepare(state)
                assert len(prepared['candidates'])==len(state['commands'])
                assert input_builder.tensors.decode(prepared['state'])==state['observation']
                prepared_states+=1
            for encoded in state['commands']:
                command = json.loads(encoded)
                candidate_entities(state['observation'],command)
                all_candidates_checked += 1
                if command.get('Deed') is not None:
                    bound = deed_entities(state['observation'], command['Deed'])
                    assert bound and bound[0]['parameter'] == ('HandSlot',)
                    deed_candidates_checked += 1
                for kind in ('Basic', 'Rest', 'Healing', 'Learning'):
                    if command.get(kind) is not None:
                        lifecycle_entities(state['observation'], kind, command[kind])
                        lifecycle_candidates_checked[kind] = lifecycle_candidates_checked.get(kind, 0) + 1
                for kind in ('EndTurn','LevelReward','CityArtifactReward'):
                    if command.get(kind) is not None:
                        reward_entities(state['observation'], kind, command[kind])
                        lifecycle_candidates_checked[kind] = lifecycle_candidates_checked.get(kind, 0) + 1
        return response
    try:
        p = start()
        assert ask(p, op='capabilities')['result']['formal_training_ready'] is False
        setup = dict(Version=1, Hero='Tovak', DummyHero='Goldyx', DummyCrystals=[0,1,2,0],
                     DualColorRule=1, Seeds=dict(Tiles=1, Player=191, Enemies=192, Mana=193,
                     Offers=194, RoundShuffle=195, Progression=196, Dummy=197, Tactics=198,
                     DummySkills=199, Ruins=200))
        for seed in range(1, 1000):
            setup['Seeds']['Tiles'] = seed
            response = ask(p, op='reset', setup=setup)
            if response['ok']:
                break
            assert 'ruin' in response['message'].lower(), response
        assert response['ok'], response
        initial = response['result']
        if input_builder is not None:
            for corrupt in ('payload','observation','command'):
                changed=deepcopy(initial)
                if corrupt=='payload':changed['support_payload_json']+=' '
                elif corrupt=='observation':changed['observation']['Inventory']['Fame']+=1
                else:changed['commands'][0]+=' '
                try:input_builder.prepare(changed)
                except ValueError:pass
                else:raise AssertionError('Changed input accepted: '+corrupt)
        projected = policy_records(initial)
        assert restore_public_records(projected['state']) == {
            k:v for k,v in initial['observation'].items() if k!='Step'}
        assert len(projected['candidates']) == len(initial['commands'])
        first = ask(p, op='step', support_sha256=initial['support_sha256'], index=0)
        assert first['ok'], first
        before = ask(p, op='observe')['result']
        assert not ask(p, op='step', support_sha256=initial['support_sha256'], index=0)['ok']
        assert ask(p, op='observe')['result'] == before
        archive = ask(p, op='export')['result']
        assert archive['version'] == 3 and len(archive['binding_sha256']) == 64
        q = start()
        restored = ask(q, op='restore', archive=archive)
        assert restored['ok'], restored
        for key in ('observation','support_sha256','commands'):
            assert restored['result'][key] == before[key], key
        assert policy_records(restored['result']) == policy_records(before)
        digest = archive['binding_sha256']
        archive['binding_sha256'] = '0'*64
        assert not ask(q, op='restore', archive=archive)['ok']
        assert ask(q, op='observe')['result'] == restored['result']
        archive['binding_sha256'] = digest
        for private_key in ('initial_state_sha256', 'current_state_sha256'):
            saved_digest = archive[private_key]
            archive[private_key] = '0'*64
            assert not ask(q, op='restore', archive=archive)['ok']
            assert ask(q, op='observe')['result'] == restored['result']
            archive[private_key] = saved_digest
        saved_effect_seed = archive['setup']['Seeds']['Effects']
        archive['setup']['Seeds']['Effects'] = saved_effect_seed + 1
        assert not ask(q, op='restore', archive=archive)['ok']
        assert ask(q, op='observe')['result'] == restored['result']
        archive['setup']['Seeds']['Effects'] = saved_effect_seed
        for private_key in ('PrivateStateBefore', 'PrivateStateAfter'):
            saved_digest = archive['records'][0][private_key]
            archive['records'][0][private_key] = '0'*64
            assert not ask(q, op='restore', archive=archive)['ok']
            assert ask(q, op='observe')['result'] == restored['result']
            archive['records'][0][private_key] = saved_digest
        original_reward = archive['records'][0]['Transition']['Reward']
        archive['records'][0]['Transition']['Reward'] += 1
        assert not ask(q, op='restore', archive=archive)['ok']
        assert ask(q, op='observe')['result'] == restored['result']
        archive['records'][0]['Transition']['Reward'] = original_reward
        # Exercise the actual protocol across the full clock lifecycle. This deliberately
        # discards/rests; it tests termination and persistence, not conquest competence.
        started = time.perf_counter()
        state = before
        decisions = 1
        while state['commands'] and decisions < 400:
            commands = [json.loads(c) for c in state['commands']]
            def priority(i):
                c = commands[i]
                rank = next((n for n, name in enumerate(('Clock', 'TacticInput', 'EndTurn', 'Rest'))
                             if c.get(name) is not None), 100)
                return rank, -len((c.get('EndTurn') or {}).get('DiscardHandSlots', []))
            index = min(range(len(commands)), key=priority)
            assert priority(index)[0] < 100, 'No clock/discard lifecycle continuation'
            response = ask(p, op='step', support_sha256=state['support_sha256'], index=index)
            assert response['ok'], response
            state = response['result']['state']
            decisions += 1
        completed = ask(p, op='export')['result']
        assert completed['terminal'] and decisions == len(completed['records'])
        audit = audit_native_journal(completed['records'])
        assert audit['passed'], audit
        saved_mid_digest = completed['records'][1]['PrivateStateBefore']
        completed['records'][1]['PrivateStateBefore'] = '0'*64
        assert not audit_native_journal(completed['records'])['passed']
        completed['records'][1]['PrivateStateBefore'] = saved_mid_digest
        final_reward = completed['records'][-1]['Transition']['Reward']
        completed['records'][-1]['Transition']['Reward'] = final_reward + 1
        assert not audit_native_journal(completed['records'])['passed']
        completed['records'][-1]['Transition']['Reward'] = final_reward
        rewards = sum(row['Transition']['Reward'] for row in completed['records'])
        last = completed['records'][-1]
        assert abs(rewards-last['RewardScale']*(last['Transition']['Score']['Total']-
                                              completed['initial_score'])) < 1e-10
        terminal_restore = ask(q, op='restore', archive=completed)
        assert terminal_restore['ok'], terminal_restore
        for key in ('observation', 'support_sha256', 'commands'):
            assert terminal_restore['result'][key] == state[key], key
        assert not ask(q, op='step', support_sha256=None, index=0)['ok']
        assert ask(q, op='observe')['result'] == terminal_restore['result']
        assert deed_candidates_checked > 0
        print(json.dumps(dict(passed=True, fixture_tile_seed=seed, separate_process_restore=True,
                              deed_candidates_checked=deed_candidates_checked,
                              all_candidates_checked=all_candidates_checked,
                              prepared_states=prepared_states,
                              lifecycle_candidates_checked=lifecycle_candidates_checked,
                              stale_support_rejected=True, reward_tamper_rejected=True,
                              hidden_state_tamper_rejected=True, hidden_rng_tamper_rejected=True,
                              native_terminal_decisions=decisions, terminal_restore=True,
                              lifecycle_seconds=time.perf_counter()-started,
                              training_started=False)))
    finally:
        for p in processes:
            p.stdin.close()
            p.wait(timeout=30)


if __name__ == '__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--input-assets');parser.add_argument('--public-schema')
    args=parser.parse_args()
    if bool(args.input_assets)!=bool(args.public_schema):parser.error('Both input asset and schema paths required')
    run(args.input_assets,args.public_schema)
