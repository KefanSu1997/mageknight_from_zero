"""Explicit zero-extension migration of public text argument inputs and Adam."""
import argparse
import copy
import json
import os
from pathlib import Path
import numpy as np
from recurrent_architecture import build_policy
from recurrent_checkpoint import Adam, load_checkpoint, save_checkpoint
from task_feasibility import file_hash


def main():
    parser=argparse.ArgumentParser()
    for key in ('checkpoint','pool','dll','old-factory','out'):parser.add_argument('--'+key,required=True)
    args=parser.parse_args()
    with np.load(args.checkpoint,allow_pickle=False) as arrays:
        header=json.loads(str(arrays['header']))
    old_meta=header['metadata']
    if old_meta['recurrent_architecture_sha256']!=file_hash(args.old_factory):
        raise ValueError('Source actor code differs from explicitly archived factory')
    if old_meta['rules_sha256']!=file_hash(args.dll) or old_meta['pool_sha256']!=file_hash(args.pool):
        raise ValueError('Actual source rules/pool differ')
    out=Path(args.out)
    if out.exists():raise ValueError('Preserve old migration evidence')
    os.environ.update(BATTLE_RL_DLL=str(Path(args.dll).resolve()),BATTLE_RL_PROTOCOL='--serve-journey-artifacts',
        BATTLE_RL_NATIVE_PAYMENTS='1',BATTLE_RL_NATIVE_CONVERSIONS='1',BATTLE_RL_NATIVE_ENERGY_TARGETS='1',
        BATTLE_RL_ORIGINAL_OPENINGS='1',BATTLE_RL_SHARED_SKILLS='1',BATTLE_RL_SHAPESHIFT='1',BATTLE_RL_ORIGINAL_WORLD='1')
    from train_ppo import Rules
    parameters,old_optimizer,random,replay=load_checkpoint(args.checkpoint,old_meta)
    if not replay['at_native_episode_boundary']:raise ValueError('Actual episode boundary required')
    rules=Rules()
    try:
        task=json.loads(Path(args.pool).read_text())['tasks'][0]['task']['definition']
        current=rules.reset_task(task)
        policies=[]
        reports=[]
        for printed,name in ((False,'control'),(True,'printed')):
            policy,architecture=build_policy(len(current['features']),rules.actions,rules.info['featureSchema'],True,True,printed)
            target=policy.parameters()
            if list(target)!=list(parameters):raise ValueError('Parameter family order changed')
            optimizer=Adam(target,**old_optimizer.settings());optimizer.steps=old_optimizer.steps
            for key in target:
                old=parameters[key]
                if target[key].shape==old.shape:
                    target[key][...]=old;optimizer.first[key][...]=old_optimizer.first[key];optimizer.second[key][...]=old_optimizer.second[key]
                elif printed and key=='head.argument_project' and target[key].shape[1:]==old.shape[1:]:
                    target[key][...]=0;target[key][:len(old)]=old
                    optimizer.first[key][:len(old)]=old_optimizer.first[key];optimizer.second[key][:len(old)]=old_optimizer.second[key]
                else:raise ValueError('Undeclared shape migration: '+key)
            policy.bind_parameters(target)
            meta=dict(old_meta,**architecture)
            folder=out/name;folder.mkdir(parents=True)
            save_checkpoint(folder/'checkpoint.npz',optimizer,random,meta,copy.deepcopy(replay))
            restored,other,other_random,other_replay=load_checkpoint(folder/'checkpoint.npz',meta)
            for key,value in target.items():
                np.testing.assert_array_equal(value,restored[key]);np.testing.assert_array_equal(optimizer.first[key],other.first[key]);np.testing.assert_array_equal(optimizer.second[key],other.second[key])
                np.testing.assert_array_equal(restored[key][:len(parameters[key])] if key=='head.argument_project' else restored[key],parameters[key])
            assert other_random.bit_generator.state==random.bit_generator.state and other_replay==replay and other.steps==old_optimizer.steps
            policies.append(policy);reports.append(dict(name=name,features=policy.head.argument_features.shape[1],arrays=3*len(target),history_rng_adam_preserved=True))
        memories=[None,None];previous=-1;raw=0.;maximum=0.;trace=[]
        for step in range(6):
            results=[p.step(current['features'],current['legal'],previous,raw,m) for p,m in zip(policies,memories)]
            delta=float(np.max(np.abs(results[0][0]-results[1][0])));maximum=max(maximum,delta)
            np.testing.assert_allclose(results[0][0],results[1][0],rtol=1e-11,atol=1e-12)
            np.testing.assert_allclose(results[0][1],results[1][1],atol=1e-12)
            np.testing.assert_allclose(results[0][2],results[1][2],atol=1e-12)
            choices=[i for i in current['legal'] if rules.actions[i]['Kind']==('fight' if step==0 else 'advance')]
            selected=choices[0] if choices else current['legal'][0]
            following=rules.step(selected);trace.append(dict(before=current['state'],action=rules.actions[selected],after=following['state'],reward=following['reward']))
            memories=[result[2] for result in results];previous,raw,current=selected,following['reward'],following
            if current['done']:break
        (out/'migration.json').write_text(json.dumps(dict(source_checkpoint=str(Path(args.checkpoint).resolve()),source_sha256=file_hash(args.checkpoint),variants=reports,actual_operations=len(trace),maximum_full_legal_probability_difference=maximum,scope='Zero public-input extension/complete optimizer restore only, not learning or Unity'),indent=2))
        (out/'actual_warm_trace.json').write_text(json.dumps(trace,ensure_ascii=False),encoding='utf-8')
        print(json.dumps(dict(variants=reports,operations=len(trace),maximum_probability_delta=maximum)))
    finally:rules.close()


if __name__=='__main__':main()
