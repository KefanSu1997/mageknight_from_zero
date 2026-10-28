"""Verify actual formal summon supply, ownership, restoration and seeded replay.

This is protocol evidence, not a PPO or Unity acceptance claim. Keep target-capacity
gaps visible; do not discard the encounter or fabricate a weaker enemy.
"""
import json
from pathlib import Path
from train_ppo import Rules

destination = Path('AutomationOutputs/BattleRl/20261004_seeded_summon_supply.json')
env = Rules()
if env.info.get('enemySlots',3)>3:
    destination=destination.with_name('20261004_v37_seeded_summon_supply.json')
traces = []


def step_kind(state, kind, trace):
    choices = [i for i in state['legal'] if env.actions[i]['Kind'] == kind]
    assert choices, (kind, state['state']['phase'])
    action = choices[0]
    after = env.step(action)
    trace.append({'before': state['state'], 'action': env.actions[action],
                  'after': after['state']})
    return after


def exercise(seed):
    trace = []
    traces.append({'seed': seed, 'trace': trace})
    state = env.reset(seed)
    assert len(state['state']['enemySupply']['draw']) == 16
    state = step_kind(state, 'fight', trace)
    originals = [(e['instance'], e['id']) for e in state['state']['enemies']]
    state = step_kind(state, 'advance', trace)
    defense = state['state']
    assert defense['phase'] == 'Block'
    bindings = defense['summoning']['bindings']
    assert bindings, (seed, originals)
    tokens = {b['token'] for b in bindings}
    assert len(tokens) == len(bindings)
    assert tokens == {e['instance'] for e in defense['enemySupply']['inUse']}
    assert len(defense['enemySupply']['draw']) + len(tokens) == 16
    for binding in bindings:
        actual = defense['enemies'][binding['index']]
        assert (actual['instance'], actual['id']) == (binding['token'], binding['tokenId'])
        assert (binding['summoner'], binding['summonerId']) == originals[binding['originalIndex']]
    capacity_gap = len(defense['enemies']) > env.info.get('enemySlots',3)
    # Preserve the expanded formal state even when the current policy cannot
    # address every defender. Never count this fixture as trainable coverage.
    if capacity_gap:
        return {'seed': seed, 'bindings': bindings, 'defenders': len(defense['enemies']),
                'status': 'blocked_policy_target_capacity', 'training_coverage': False}
    state = step_kind(state, 'advance', trace)
    if seed==155 and not capacity_gap:
        actual=[i for i in state['legal'] if env.actions[i]['Kind']=='unitDamage' and env.actions[i]['Targets']==3 and env.actions[i]['Choice']]
        assert actual, 'Fourth actual defender must expose real ordered unit damage selection'
        index=actual[0]
        after=env.step(index)
        trace.append({'before':state['state'],'action':env.actions[index],'after':after['state']})
        state=after
        assert any(plan['target']==3 and plan['units'] for plan in state['state']['unitDamagePlan'])
    state = step_kind(state, 'advance', trace)
    restored = state['state']
    assert restored['phase'] == 'Melee'
    assert [(e['instance'], e['id']) for e in restored['enemies']] == originals
    assert not restored['summoning']['active'] and not restored['summoning']['bindings']
    assert not restored['enemySupply']['inUse']
    assert tokens == {e['instance'] for e in restored['enemySupply']['discard']}
    assert len(restored['enemySupply']['draw']) + len(restored['enemySupply']['discard']) == 16
    return {'seed': seed, 'bindings': bindings, 'defenders': len(defense['enemies']),
            'status': 'formal_supply_restoration_passed', 'training_coverage': False}


result = {'scope': 'formal protocol supply only; no PPO or Unity acceptance', 'traces': traces}
try:
    results = [exercise(seed) for seed in (336, 154, 155)]
    repeated = exercise(336)
    assert repeated == results[0], 'Same seed must own the same actual brown draws'
    assert traces[-1]['trace'] == traces[0]['trace'], 'Same seed must replay exact operation states'
    assert results[2]['status'] == ('formal_supply_restoration_passed' if env.info.get('enemySlots',3)>3 else 'blocked_policy_target_capacity')
    result.update(status='passed' if env.info.get('enemySlots',3)>3 else 'passed_with_explicit_policy_gap', results=results,
                  deterministic_replay=True)
    print(json.dumps({k: v for k, v in result.items() if k != 'traces'}, ensure_ascii=False))
except Exception as error:
    result.update(status='failed', error=str(error))
    raise
finally:
    destination.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    env.close()
