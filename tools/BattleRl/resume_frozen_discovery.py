"""Recover an interrupted frozen evaluation by exact saved-policy/native replay.

This is required RNG recovery, not a new regression batch or teacher training.
Old failed reports remain in the source directory and in prior_report fields.
"""
import argparse
import json
import os
from pathlib import Path
import numpy as np
from discover_policy_witnesses import candidate_rows
from evaluate_recurrent_ppo import load_frozen_policy, parameter_hash, rollout
from task_feasibility import SEQUENCE_GOAL, certify_witness, digest, file_hash
from trajectory_audit import audit


def main():
    parser = argparse.ArgumentParser()
    for key in ('dll', 'checkpoint', 'training-pool', 'drafts', 'source', 'reference', 'out'):
        parser.add_argument('--' + key, required=True)
    parser.add_argument('--seed', type=int, required=True)
    args = parser.parse_args()
    output = Path(args.out)
    if output.exists():
        raise ValueError('Preserve evaluation recovery evidence')
    reference = json.loads((Path(args.reference) / 'solvable_pool.json').read_text())
    for key, path in (('checkpoint_sha256', args.checkpoint),
                      ('source_training_pool_sha256', args.training_pool),
                      ('source_drafts_sha256', args.drafts)):
        if reference[key] != file_hash(path):
            raise ValueError('Frozen reference source differs: ' + key)
    if reference['mode'] != 'sampled' or reference['partition'] != 'validation':
        raise ValueError('This recovery supports the actual sampled validation protocol only')
    process = json.loads((Path(args.source + '_launch') / 'process.json').read_text())
    if f'--seed {args.seed}' not in process['CommandLine']:
        raise ValueError('Recovery seed differs from actual source command')
    with np.load(args.checkpoint, allow_pickle=False) as saved:
        metadata = json.loads(str(saved['header']))['metadata']
    os.environ.update(BATTLE_RL_DLL=str(Path(args.dll).resolve()),
        BATTLE_RL_PROTOCOL='--serve-journey-artifacts', BATTLE_RL_NATIVE_PAYMENTS='1',
        BATTLE_RL_NATIVE_CONVERSIONS='1', BATTLE_RL_NATIVE_ENERGY_TARGETS='1',
        BATTLE_RL_ORIGINAL_OPENINGS='1',
        BATTLE_RL_SHARED_SKILLS=str(int(bool(metadata.get('native_shared_skills')))),
        BATTLE_RL_SHAPESHIFT=str(int(bool(metadata.get('native_shapeshift')))),
        BATTLE_RL_ORIGINAL_WORLD=str(int(bool(metadata.get('native_world')))))
    from train_ppo import Rules
    rows = candidate_rows(json.loads(Path(args.drafts).read_text()), 'validation', 33, False)
    sources = {p.stem[:-2]: p for p in Path(args.source).glob('*_0.json')}
    if set(sources) - {r['task_id'] for r in rows}:
        raise ValueError('Unexpected source composition')
    count = len(sources)
    if {r['task_id'] for r in rows[:count]} != set(sources):
        raise ValueError('Saved attempts must be an exact contiguous protocol prefix')
    output.mkdir(parents=True)
    rules = Rules()
    attempts, certificates, quarantine = [], [], []
    random = np.random.default_rng(args.seed)
    recovered_operations = 0
    try:
        initial = rules.reset_task(rows[0]['task_definition'])
        policy, _ = load_frozen_policy(args.checkpoint, args.training_pool, args.dll, rules, initial)
        original_hash = parameter_hash(policy)
        keys = {a['Key']: i for i, a in enumerate(rules.actions)}
        for position, row in enumerate(rows):
            current = rules.reset_task(row['task_definition'])
            descriptor = dict(task_id=row['task_id'], hero=row['task_definition']['HeroId'],
                task=dict(definition=row['task_definition'], initial_state_sha256=digest(current['state'])))
            if position < count:
                source = sources[row['task_id']]
                result = json.loads(source.read_text())
                memory, previous, raw = None, -1, 0.
                for step, event in enumerate(result['trace']):
                    index = keys[event['action']['Key']]
                    if current['state'] != event['before'] or index not in current['legal']:
                        raise ValueError(f'Original native prefix changed at {position}:{step}')
                    probability, _, memory = policy.step(current['features'], current['legal'], previous, raw, memory)
                    np.testing.assert_allclose(probability[index], event['selection']['selected_probability'], rtol=1e-9, atol=1e-12)
                    if int(random.choice(len(probability), p=probability)) != index:
                        raise ValueError(f'Original policy RNG choice changed at {position}:{step}')
                    following = rules.step(index)
                    if following['state'] != event['after'] or following['reward'] != event['reward']:
                        raise ValueError(f'Original native result changed at {position}:{step}')
                    previous, raw, current = index, following['reward'], following
                    recovered_operations += 1
                result['prior_report'] = result['report']
                result['report'] = audit(result['trace'])
                result['recovered_source'] = dict(path=str(source.resolve()), sha256=file_hash(source))
            else:
                result = rollout(rules, policy, descriptor, random, 'sampled')
            path = output / f'{row["task_id"]}_0.json'
            path.write_text(json.dumps(result, ensure_ascii=False), encoding='utf-8')
            if result['failure'] or result['report']['errors']:
                raise RuntimeError('New or unresolved failure retained before stopping recovery')
            attempts.append(dict(draft_id=row['task_id'], attempt=0, path=str(path.resolve()),
                                 sha256=file_hash(path), completed=result['completed_task']))
            if result['completed_task']:
                witness = output / f'{row["task_id"]}_witness.json'
                witness.write_text(json.dumps(dict(seed=row['task_definition']['RandomSeed'],
                    task_definition=row['task_definition'], goal=SEQUENCE_GOAL,
                    trace=result['trace'], provenance='Frozen exact recovered evaluation, not teacher data'), ensure_ascii=False), encoding='utf-8')
                certificates.append(certify_witness(rules, witness, args.dll))
            else:
                quarantine.append(dict(draft_id=row['task_id'], task_definition=row['task_definition'],
                    status='unknown', impossibility_proved=False, excluded_from_victory_pool=True))
            state = dict(next_task=position + 1, actor_rng=random.bit_generator.state,
                recovered_operations=recovered_operations, checkpoint_sha256=file_hash(args.checkpoint))
            (output / 'recovery_progress.json').write_text(json.dumps(state, indent=2))
            print(json.dumps(dict(task=row['task_id'], recovered=position < count,
                                  completed=result['completed_task'])), flush=True)
        if parameter_hash(policy) != original_hash or reference['checkpoint_sha256'] != file_hash(args.checkpoint):
            raise ValueError('Frozen parameters changed')
        final = dict(reference, seed=args.seed, tasks=certificates, quarantine=quarantine,
            attempts=attempts, optimizer_updates=0, frozen_parameters_verified=True,
            recovery_source=str(Path(args.source).resolve()), recovered_operations=recovered_operations,
            recovery_code_sha256=file_hash(__file__), original_saved_attempts=count,
            exact_native_probability_and_actor_rng_recovery=True)
        (output / 'solvable_pool.json').write_text(json.dumps(final, ensure_ascii=False, indent=2), encoding='utf-8')
    finally:
        rules.close()


if __name__ == '__main__':
    main()
