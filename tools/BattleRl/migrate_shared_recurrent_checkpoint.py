"""Actual saved recurrent/Adam/history migration into the witnessed shared rules."""
import argparse
import copy
import json
import os
from pathlib import Path
import numpy as np
from recurrent_checkpoint import load_checkpoint, save_checkpoint, Adam
from recurrent_policy import RecurrentPolicy
from recurrent_semantic_expansion import expand_arrays
from task_feasibility import digest, file_hash
from mixed_task_pool import validate_pool, MixedTaskSampler
from original_data_signature import data_signature,world_source_hash


def main():
    parser=argparse.ArgumentParser()
    for name in ('checkpoint','old-dll','new-dll','old-pool','new-pool','mapping','out'):
        parser.add_argument('--'+name,required=True)
    parser.add_argument('--native-shapeshift',action='store_true')
    parser.add_argument('--native-world',action='store_true')
    args=parser.parse_args();out=Path(args.out)
    if out.exists():raise ValueError('Preserve checkpoint evidence')
    old_pool=json.loads(Path(args.old_pool).read_text());new_pool=json.loads(Path(args.new_pool).read_text())
    old_rows=validate_pool(old_pool,args.old_dll);new_rows=validate_pool(new_pool,args.new_dll)
    mapping=json.loads(Path(args.mapping).read_text())
    if set(mapping)!={r['task_id'] for r in old_rows} or set(mapping.values())!={r['task_id'] for r in new_rows}:
        raise ValueError('Every old train task needs exactly one new binding')
    new_by_id={r['task_id']:r for r in new_rows}
    if any(r['task']['definition']!=new_by_id[mapping[r['task_id']]]['task']['definition'] for r in old_rows):
        raise ValueError('Original task definition or random stream changed')
    with np.load(args.checkpoint,allow_pickle=False) as arrays:header=json.loads(str(arrays['header']))
    metadata=header['metadata']
    if metadata['rules_sha256']!=file_hash(args.old_dll) or metadata['pool_sha256']!=file_hash(args.old_pool):
        raise ValueError('Saved source binding differs')
    if metadata['rules_data_sha256']!=data_signature()['sha256']:raise ValueError('Original data changed')
    parameters,optimizer,random,replay=load_checkpoint(args.checkpoint,metadata)
    if not replay['at_native_episode_boundary']:raise ValueError('Native episode boundary required')
    os.environ.update(BATTLE_RL_NATIVE_PAYMENTS='1',BATTLE_RL_NATIVE_CONVERSIONS='1',BATTLE_RL_NATIVE_ENERGY_TARGETS='1',
                      BATTLE_RL_ORIGINAL_OPENINGS='1',BATTLE_RL_PROTOCOL='--serve-journey-artifacts')
    from train_ppo import Rules
    os.environ.update(BATTLE_RL_DLL=str(Path(args.old_dll).resolve()),BATTLE_RL_SHARED_SKILLS='1' if metadata.get('native_shared_skills') else '0',BATTLE_RL_SHAPESHIFT='1' if metadata.get('native_shapeshift') else '0',BATTLE_RL_ORIGINAL_WORLD='1' if metadata.get('native_world') else '0');old=Rules()
    os.environ.update(BATTLE_RL_DLL=str(Path(args.new_dll).resolve()),BATTLE_RL_SHARED_SKILLS='1',BATTLE_RL_SHAPESHIFT='1' if args.native_shapeshift else '0',BATTLE_RL_ORIGINAL_WORLD='1' if args.native_world else '0');new=Rules()
    try:
        definition=old_rows[0]['task']['definition'];a=old.reset_task(definition);b=new.reset_task(definition)
        old_size,new_size=len(a['features']),len(b['features'])
        if metadata['catalog_sha256']!=digest(old.actions) or metadata['feature_schema']!=old.info['featureSchema']:
            raise ValueError('Actual old catalog/schema mismatch')
        updated=expand_arrays(parameters,old.actions,new.actions,old_size,new_size)
        first=expand_arrays(optimizer.first,old.actions,new.actions,old_size,new_size)
        second=expand_arrays(optimizer.second,old.actions,new.actions,old_size,new_size)
        migrated_optimizer=Adam(updated,**optimizer.settings());migrated_optimizer.steps=optimizer.steps
        migrated_optimizer.first=first;migrated_optimizer.second=second
        old_policy=RecurrentPolicy(old_size,old.actions);old_policy.bind_parameters(parameters)
        new_policy=RecurrentPolicy(new_size,new.actions);new_policy.bind_parameters(updated)
        actions={r['Key']:i for i,r in enumerate(new.actions)}
        previous_old=previous_new=-1;reward=0.;old_memory=new_memory=None;maximum=0.;operations=[]
        for _ in range(4):
            np.testing.assert_array_equal(a['features'],b['features'][:old_size])
            probability,value,old_memory=old_policy.step(a['features'],a['legal'],previous_old,reward,old_memory)
            historical_mask=[actions[old.actions[i]['Key']] for i in a['legal']]
            other,other_value,new_memory=new_policy.step(b['features'],historical_mask,previous_new,reward,new_memory)
            discrepancy=max(float(np.max(np.abs(probability[a['legal']]-other[historical_mask]))),abs(value-other_value),float(np.max(np.abs(old_memory-new_memory))))
            maximum=max(maximum,discrepancy)
            if discrepancy>1e-12:raise ValueError('Historical-mask probability/memory/value changed')
            selected=next((i for i in a['legal'] if old.actions[i]['Kind']=='fight'),a['legal'][0])
            previous_old=selected;previous_new=actions[old.actions[selected]['Key']]
            operations.append(old.actions[selected]['Key']);a=old.step(selected);b=new.step(previous_new)
            if a['reward']!=b['reward']:raise ValueError('Actual raw reward changed')
            reward=a['reward']
        new_replay=copy.deepcopy(replay);sampler=new_replay['sampler']
        sampler['task_ids']=sorted(mapping.values())
        sampler['attempts']={mapping[k]:v for k,v in sampler['attempts'].items()}
        for evidence in sampler['first_evidence'].values():
            if evidence.get('task_id') in mapping:evidence['task_id']=mapping[evidence['task_id']]
        MixedTaskSampler(new_rows,sampler)
        # Previous real use remains history, not current-rule training evidence.
        new_replay['data_binding']=dict(legacy_events=copy.deepcopy(sampler['events']),
            legacy_data_unbound_through_episode=replay['next_episode']-1,
            legacy_reason='prior_rule_version_before_shared_and_agility_entry',prior_rules_sha256=metadata['rules_sha256'])
        next_metadata=dict(metadata,rules_sha256=file_hash(args.new_dll),catalog_sha256=digest(new.actions),
            feature_schema=new.info['featureSchema'],pool_sha256=file_hash(args.new_pool),native_shared_skills=True,
            task_sampler_sha256=file_hash(Path(__file__).with_name('certified_task_sampler.py')),
            architecture=f'MLP256-256-128_GRU256_factored{len({r["Kind"] for r in new.actions})}_original_catalog')
        if args.native_shapeshift:next_metadata['native_shapeshift']=True
        if args.native_world:next_metadata.update(native_world=True,world_source_sha256=world_source_hash())
        out.mkdir();save_checkpoint(out/'checkpoint.npz',migrated_optimizer,random,next_metadata,new_replay)
        restored,restored_optimizer,restored_random,restored_replay=load_checkpoint(out/'checkpoint.npz',next_metadata)
        for key in updated:
            for x,y in ((updated[key],restored[key]),(first[key],restored_optimizer.first[key]),(second[key],restored_optimizer.second[key])):np.testing.assert_array_equal(x,y)
        assert restored_random.bit_generator.state==random.bit_generator.state and restored_optimizer.steps==optimizer.steps
        assert restored_replay==new_replay
        report=dict(status='semantic_saved_checkpoint_migration_no_learning',array_roundtrip_exact=3*len(updated),
            rng_optimizer_steps_and_actual_episode_counts_preserved=True,old_observations=old_size,new_observations=new_size,
            historical_mask_actual_operations=operations,max_probability_memory_value_difference=maximum,
            source_checkpoint_sha256=file_hash(args.checkpoint),new_checkpoint_sha256=file_hash(out/'checkpoint.npz'),
            scope='Historical full masks tested, not new legal distribution equality. Old use counts legacy, no teacher or new PPO coverage. Unity gate pending.')
        (out/'migration.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report))
    finally:old.close();new.close()


if __name__=='__main__':main()
