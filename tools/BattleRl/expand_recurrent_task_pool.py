"""Explicit append-only certified train-pool migration, without a PPO update."""
import argparse
import copy
import json
from pathlib import Path
import numpy as np
from coverage_tasks import split
from recurrent_checkpoint import load_checkpoint, save_checkpoint
from task_feasibility import SEQUENCE_GOAL, eligible, file_hash
from certified_task_sampler import CertifiedTaskSampler
from original_data_signature import data_signature


def validate_expansion(old, new, dll):
    old_ids = {c['task_id']:c for c in old}
    new_ids = {c['task_id']:c for c in new}
    if not old or len(old_ids)!=len(old) or len(new_ids)!=len(new):
        raise ValueError('Empty or duplicated pool')
    if not set(old_ids)<=set(new_ids):
        raise ValueError('Expansion cannot remove old tasks or their histories')
    if any(not eligible(c,dll,SEQUENCE_GOAL) for c in old+new):
        raise ValueError('Stale or unproved task cannot enter expansion')
    if any(c['task'].get('definition') is None or split(c['task']['definition'])!='train' for c in new):
        raise ValueError('Only certified train compositions may enter training')
    if any(old_ids[key]['task']!=new_ids[key]['task'] for key in old_ids):
        raise ValueError('Existing task binding changed')
    return sorted(set(new_ids)-set(old_ids))


def migrate(checkpoint, old_pool_path, new_pool_path, dll, out):
    old_pool=json.loads(Path(old_pool_path).read_text())
    new_pool=json.loads(Path(new_pool_path).read_text())
    added=validate_expansion(old_pool['tasks'],new_pool['tasks'],dll)
    if not added or old_pool['goal']!=SEQUENCE_GOAL or new_pool['goal']!=SEQUENCE_GOAL:
        raise ValueError('Migration requires a genuine expansion of the same complete-task goal')
    with np.load(checkpoint,allow_pickle=False) as arrays:
        metadata=json.loads(str(arrays['header']))['metadata']
    expected=dict(metadata)
    expected.update(rules_sha256=file_hash(dll), pool_sha256=file_hash(old_pool_path), task_goal=SEQUENCE_GOAL,
                    rules_data_sha256=data_signature()['sha256'],
                    architecture='MLP256-256-128_GRU256_factored43_original_catalog',
                    history='previous_actual_action_embedding32_and_raw_reward_div20')
    for field,source in (('recurrent_policy_sha256','recurrent_policy.py'),('recurrent_core_sha256','recurrent_core.py'),
                         ('ppo_loss_sha256','train_ppo.py'),('training_objective_sha256','training_reward.py'),
                         ('task_sampler_sha256','certified_task_sampler.py')):
        expected[field]=file_hash(Path(__file__).with_name(source))
    if metadata.get('reward_mode')=='terminal_sequence':
        expected['sequence_reward_sha256']=file_hash(Path(__file__).with_name('sequence_training_reward.py'))
    if 'planning_reward_sha256' in metadata:
        expected['planning_reward_sha256']=file_hash(Path(__file__).with_name('planning_revisit_reward.py'))
    if 'progress_reward_sha256' in metadata:
        expected['progress_reward_sha256']=file_hash(Path(__file__).with_name('progress_potential_reward.py'))
    if 'encounter_sampler_sha256' in metadata:
        expected['encounter_sampler_sha256']=file_hash(Path(__file__).with_name('encounter_floor_sampler.py'))
    if any(c['task']['catalog_sha256']!=metadata['catalog_sha256'] or
           c['task']['feature_schema']!=metadata['feature_schema'] for c in new_pool['tasks']):
        raise ValueError('Expansion changes original actions or observations')
    parameters,optimizer,random,replay=load_checkpoint(checkpoint,expected)
    if not replay.get('at_native_episode_boundary'):
        raise ValueError('Task pool migration requires an actual saved episode boundary')
    CertifiedTaskSampler(old_pool['tasks'],replay['sampler'])
    expanded=copy.deepcopy(replay)
    expanded['sampler']['task_ids']=sorted(c['task_id'] for c in new_pool['tasks'])
    # No fabricated visits/events for newly certified tasks.
    CertifiedTaskSampler(new_pool['tasks'],expanded['sampler'])
    expanded['pool_expansion']=dict(source_checkpoint_sha256=file_hash(checkpoint),
                                   old_pool_sha256=file_hash(old_pool_path),new_pool_sha256=file_hash(new_pool_path),
                                   added_task_ids=added)
    new_metadata=dict(metadata,pool_sha256=file_hash(new_pool_path))
    output=Path(out)
    if output.exists():
        raise ValueError('Preserve existing migration evidence; do not overwrite')
    output.mkdir(parents=True)
    target=output/'checkpoint.npz'
    save_checkpoint(target,optimizer,random,new_metadata,expanded)
    restored,other,other_random,other_replay=load_checkpoint(target,new_metadata)
    for key in parameters:
        np.testing.assert_array_equal(parameters[key],restored[key])
        np.testing.assert_array_equal(optimizer.first[key],other.first[key])
        np.testing.assert_array_equal(optimizer.second[key],other.second[key])
    assert optimizer.steps==other.steps and random.bit_generator.state==other_random.bit_generator.state
    assert expanded==other_replay
    for field in ('attempts','events','first_evidence'):
        assert replay['sampler'][field]==other_replay['sampler'][field]
    report=dict(status='verified_append_only_expansion',source_checkpoint=str(Path(checkpoint).resolve()),
                source_checkpoint_sha256=file_hash(checkpoint),new_checkpoint_sha256=file_hash(target),
                migration_code_sha256=file_hash(__file__),old_certified_tasks=len(old_pool['tasks']),
                new_certified_tasks=len(new_pool['tasks']),added_task_ids=added,
                unchanged_parameter_and_adam_arrays=3*len(parameters),optimizer_steps=optimizer.steps,
                next_episode=replay['next_episode'],total_steps=replay['total_steps'],
                rng_and_actual_history_unchanged=True,ppo_updates=0,
                scope='Certified pool expansion only; no new tasks counted as trained')
    (output/'migration.json').write_text(json.dumps(report,indent=2))
    return report


def main():
    parser=argparse.ArgumentParser()
    for key in ('checkpoint','old-pool','new-pool','dll','out'):
        parser.add_argument('--'+key,required=True)
    args=parser.parse_args()
    print(json.dumps(migrate(args.checkpoint,args.old_pool,args.new_pool,args.dll,args.out)))


if __name__=='__main__':
    main()
