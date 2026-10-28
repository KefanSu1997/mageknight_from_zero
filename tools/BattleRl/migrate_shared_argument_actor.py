"""Warm public-argument expansion and paired control, with full saved state."""
import argparse
import json
import os
from pathlib import Path
import numpy as np
from recurrent_architecture import build_policy
from recurrent_checkpoint import load_checkpoint, save_checkpoint, Adam
from mixed_task_pool import validate_pool
from task_feasibility import file_hash, digest
from original_data_signature import data_signature, world_source_hash


def main():
    parser=argparse.ArgumentParser()
    for key in ('source','pool','dll','out','previous-factory'):parser.add_argument('--'+key,required=True)
    args=parser.parse_args();out=Path(args.out)
    if out.exists():raise ValueError('Preserve migration evidence')
    with np.load(args.source,allow_pickle=False) as a:metadata=json.loads(str(a['header']))['metadata']
    if metadata['recurrent_architecture_sha256']!=file_hash(args.previous_factory):raise ValueError('Archived factory binding differs')
    if metadata['pool_sha256']!=file_hash(args.pool) or metadata['rules_sha256']!=file_hash(args.dll):raise ValueError('Actual source pool/rules differ')
    if metadata['rules_data_sha256']!=data_signature()['sha256'] or metadata['world_source_sha256']!=world_source_hash():raise ValueError('Original public data changed')
    old_arrays,opt,rng,replay=load_checkpoint(args.source,metadata)
    if not replay['at_native_episode_boundary'] or metadata.get('shared_argument_head'):raise ValueError('Ordinary entity actor boundary required')
    rows=validate_pool(json.loads(Path(args.pool).read_text()),args.dll)
    os.environ.update(BATTLE_RL_DLL=str(Path(args.dll).resolve()),BATTLE_RL_PROTOCOL='--serve-journey-artifacts',
        BATTLE_RL_NATIVE_PAYMENTS='1',BATTLE_RL_NATIVE_CONVERSIONS='1',BATTLE_RL_NATIVE_ENERGY_TARGETS='1',
        BATTLE_RL_ORIGINAL_OPENINGS='1',BATTLE_RL_SHARED_SKILLS='1',BATTLE_RL_SHAPESHIFT='1',BATTLE_RL_ORIGINAL_WORLD='1')
    from train_ppo import Rules
    from evaluate_recurrent_ppo import load_frozen_policy
    rules=Rules()
    try:
        current=rules.reset_task(rows[0]['task']['definition'])
        if metadata['catalog_sha256']!=digest(rules.actions) or metadata['feature_schema']!=rules.info['featureSchema']:raise ValueError('Formal interface differs')
        control,control_meta=build_policy(len(current['features']),rules.actions,rules.info['featureSchema'],True,False)
        control.bind_parameters(old_arrays)
        shared,shared_meta=build_policy(len(current['features']),rules.actions,rules.info['featureSchema'],True,True)
        expanded=shared.parameters()
        for key,value in old_arrays.items():expanded[key][...]=value
        shared_optimizer=Adam(expanded,**opt.settings());shared_optimizer.steps=opt.steps
        for key in old_arrays:
            shared_optimizer.first[key][...]=opt.first[key]
            shared_optimizer.second[key][...]=opt.second[key]
        memory_a=memory_b=None;previous=-1;reward=0.;maximum=0.;operations=[]
        for _ in range(6):
            a,v,memory_a=control.step(current['features'],current['legal'],previous,reward,memory_a)
            b,w,memory_b=shared.step(current['features'],current['legal'],previous,reward,memory_b)
            difference=max(float(np.max(np.abs(a-b))),abs(v-w),float(np.max(np.abs(memory_a-memory_b))))
            maximum=max(maximum,difference)
            if difference>1e-12:raise ValueError('Warm head changed full legal distribution/value/history')
            selected=next((i for i in current['legal'] if rules.actions[i]['Kind'] in ('fight','advance')),current['legal'][0])
            operations.append(rules.actions[selected]['Key']);following=rules.step(selected)
            previous,reward,current=selected,following['reward'],following
            if current['done']:break
        out.mkdir(parents=True)
        branches=[]
        for name,optimizer,extra in [('control',opt,control_meta),('shared',shared_optimizer,shared_meta)]:
            directory=out/name;directory.mkdir();updated=dict(metadata,**extra)
            save_checkpoint(directory/'checkpoint.npz',optimizer,rng,updated,replay)
            arrays,restored,random,history=load_checkpoint(directory/'checkpoint.npz',updated)
            for key in optimizer.parameters:
                for a,b in ((optimizer.parameters[key],arrays[key]),(optimizer.first[key],restored.first[key]),(optimizer.second[key],restored.second[key])):np.testing.assert_array_equal(a,b)
            assert restored.steps==opt.steps and random.bit_generator.state==rng.bit_generator.state and history==replay
            initial=rules.reset_task(rows[0]['task']['definition'])
            loaded,loaded_history=load_frozen_policy(directory/'checkpoint.npz',args.pool,args.dll,rules,initial)
            assert loaded_history==replay
            branches.append(dict(branch=name,array_roundtrip_exact=3*len(arrays),frozen_reader_loaded=True))
        report=dict(status='explicit_shared_argument_warm_migration_no_learning',branches=branches,maximum_full_legal_probability_value_memory_difference=maximum,
            actual_formal_operations=operations,old_arrays_moments_rng_counts_preserved=True,actual_episode_count=replay['next_episode'],actual_steps=replay['total_steps'],actual_adam_steps=opt.steps,
            scope='Public argument descriptors only, not original effect understanding. Two new shared matrices initialized explicitly, projection zero. No real PPO update or model/Unity acceptance.')
        (out/'migration.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
    finally:rules.close()


if __name__=='__main__':main()
