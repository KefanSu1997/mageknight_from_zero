"""Bind an old checkpoint to currently revalidated data without retroactive claims."""
import argparse
import copy
import json
from pathlib import Path
import numpy as np
from original_data_signature import data_signature
from recurrent_checkpoint import load_checkpoint,save_checkpoint
from task_feasibility import SEQUENCE_GOAL,eligible,file_hash


def main():
    parser=argparse.ArgumentParser()
    for name in ('checkpoint','old-pool','new-pool','dll','out'):
        parser.add_argument('--'+name,required=True)
    args=parser.parse_args()
    old=json.loads(Path(args.old_pool).read_text())
    new=json.loads(Path(args.new_pool).read_text())
    by_id={c['task_id']:c for c in old['tasks']}
    if len(by_id)!=len(new['tasks']) or any(c['task_id'] not in by_id or c['task']!=by_id[c['task_id']]['task']
        or not eligible(c,args.dll,SEQUENCE_GOAL) for c in new['tasks']):
        raise ValueError('Data binding requires complete current replay of the same task bindings')
    with np.load(args.checkpoint,allow_pickle=False) as arrays:
        metadata=json.loads(str(arrays['header']))['metadata']
    if 'rules_data_sha256' in metadata:
        raise ValueError('Already data-bound checkpoint must use ordinary strict restore')
    expected=dict(metadata,rules_sha256=file_hash(args.dll),pool_sha256=file_hash(args.old_pool))
    for field,source in (('recurrent_policy_sha256','recurrent_policy.py'),('recurrent_core_sha256','recurrent_core.py'),
                         ('ppo_loss_sha256','train_ppo.py'),('training_objective_sha256','training_reward.py'),
                         ('task_sampler_sha256','certified_task_sampler.py')):
        expected[field]=file_hash(Path(__file__).with_name(source))
    if metadata.get('reward_mode')=='terminal_sequence':
        expected['sequence_reward_sha256']=file_hash(Path(__file__).with_name('sequence_training_reward.py'))
    parameters,optimizer,random,replay=load_checkpoint(args.checkpoint,expected)
    if not replay['at_native_episode_boundary']:
        raise ValueError('Binding requires a saved native episode boundary')
    if replay['sampler']['task_ids']!=sorted(by_id):
        raise ValueError('Actual sampling history does not match old task pool')
    signature=data_signature()
    updated=dict(metadata,pool_sha256=file_hash(args.new_pool),rules_data_sha256=signature['sha256'])
    history=copy.deepcopy(replay)
    history['data_binding']=dict(signature=signature,legacy_data_unbound_through_episode=replay['next_episode'],
                                 legacy_events=copy.deepcopy(replay['sampler']['events']),
                                 source_checkpoint_sha256=file_hash(args.checkpoint),
                                 scope='Previous training data version unrecorded; current witness replay is not retroactive coverage')
    output=Path(args.out)
    if output.exists():
        raise ValueError('Preserve existing binding evidence')
    output.mkdir(parents=True)
    save_checkpoint(output/'checkpoint.npz',optimizer,random,updated,history)
    loaded,other,other_random,restored=load_checkpoint(output/'checkpoint.npz',updated)
    for key in parameters:
        for a,b in ((parameters[key],loaded[key]),(optimizer.first[key],other.first[key]),(optimizer.second[key],other.second[key])):
            np.testing.assert_array_equal(a,b)
    assert random.bit_generator.state==other_random.bit_generator.state and replay['sampler']==restored['sampler']
    report=dict(status='current_data_binding_verified',data_signature=signature,
                same_parameter_adam_arrays=3*len(parameters),optimizer_steps=other.steps,
                next_episode=restored['next_episode'],total_steps=restored['total_steps'],
                rng_history_unchanged=True,historical_data_version_proved=False,
                source_checkpoint_sha256=file_hash(args.checkpoint),new_pool_sha256=file_hash(args.new_pool))
    (output/'binding.json').write_text(json.dumps(report,indent=2))
    print(json.dumps({key:report[key] for key in ('status','same_parameter_adam_arrays','next_episode','historical_data_version_proved')}))


if __name__=='__main__':
    main()
