"""Replay a saved actor failure; inspect formal attack alternatives without updating it."""
import argparse
import json
import os
from pathlib import Path
import numpy as np
from evaluate_recurrent_ppo import load_frozen_policy, parameter_hash
from task_feasibility import file_hash
from train_ppo import Rules


def main():
    parser=argparse.ArgumentParser()
    for name in ('dll','checkpoint','pool','episode','out'):
        parser.add_argument('--'+name,required=True)
    args=parser.parse_args()
    out=Path(args.out)
    if out.exists(): raise ValueError('Preserve existing diagnostic evidence')
    episode=json.loads(Path(args.episode).read_text())
    certificates=json.loads(Path(args.pool).read_text())['tasks']
    certificate=next(c for c in certificates if c['task_id']==episode['task_id'])
    os.environ.update(BATTLE_RL_DLL=str(Path(args.dll).resolve()),BATTLE_RL_PROTOCOL='--serve-journey-artifacts',
                      BATTLE_RL_NATIVE_PAYMENTS='1',BATTLE_RL_NATIVE_CONVERSIONS='1',
                      BATTLE_RL_NATIVE_ENERGY_TARGETS='1',BATTLE_RL_ORIGINAL_OPENINGS='1')
    rules=Rules()
    try:
        current=rules.reset_task(certificate['task']['definition'])
        policy,_=load_frozen_policy(args.checkpoint,args.pool,args.dll,rules,current)
        original=parameter_hash(policy)
        keys={a['Key']:i for i,a in enumerate(rules.actions)}
        memory,previous,reward=None,-1,0.
        observations=[]
        for step,row in enumerate(episode['trace']):
            action=keys[row['action']['Key']]
            if current['state']!=row['before'] or action not in current['legal'] or rules.actions[action]!=row['action']:
                raise ValueError(f'Actual prefix/legality mismatch at {step}')
            probabilities,value,memory=policy.step(current['features'],current['legal'],previous,reward,memory)
            np.testing.assert_allclose(probabilities[action],row['selection']['selected_probability'],rtol=1e-9,atol=1e-12)
            if row['action']['Kind']=='advance' and row['before']['phase'] in ('Ranged','Melee') and row['before']['attacks']:
                attacks=[i for i in current['legal'] if rules.actions[i]['Kind']=='attack']
                best=max(attacks,key=lambda i:probabilities[i]) if attacks else None
                observations.append(dict(step=step,phase=row['before']['phase'],selected_probability=float(probabilities[action]),
                    value=float(value),legal_attack_count=len(attacks),attack_probability_mass=float(sum(probabilities[i] for i in attacks)),
                    best_attack=None if best is None else rules.actions[best],best_attack_probability=None if best is None else float(probabilities[best]),
                    enemies=row['before']['enemies'],contributions=row['before']['attacks']))
            following=rules.step(action)
            if following['state']!=row['after'] or following['reward']!=row['reward']:
                raise ValueError(f'Actual after-state/raw reward mismatch at {step}')
            previous,reward,current=action,following['reward'],following
        # Probe only an action admitted by the actual engine at the exact same prefix.
        # This proves an immediate alternative outcome, never an initial task proof or learning effect.
        for observation in observations:
            if observation['best_attack'] is None: continue
            current=rules.reset_task(certificate['task']['definition'])
            for row in episode['trace'][:observation['step']]:
                if current['state']!=row['before']: raise ValueError('Counterfactual prefix changed')
                current=rules.step(keys[row['action']['Key']])
                if current['state']!=row['after']: raise ValueError('Counterfactual prefix after changed')
            row=episode['trace'][observation['step']]
            if current['state']!=row['before']: raise ValueError('Alternative starts from another state')
            action=keys[observation['best_attack']['Key']]
            if action not in current['legal']: raise ValueError('Alternative is not formally legal')
            following=rules.step(action)
            observation['actual_alternative']=dict(before=current['state'],action=rules.actions[action],after=following['state'],
                                                   raw_reward=following['reward'],native_done=following['done'])
        assert parameter_hash(policy)==original
        result=dict(status='exact_actual_replay_and_legal_alternatives_verified',observations=observations,
                    source_episode=dict(path=str(Path(args.episode).resolve()),sha256=file_hash(args.episode)),
                    checkpoint_sha256=file_hash(args.checkpoint),diagnostic_code_sha256=file_hash(__file__),
                    actor_unchanged=True,optimizer_updates=0,scope='Immediate counterfactual only; no future victory, impossibility or policy improvement claim')
        out.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
        print(json.dumps([dict(step=r['step'],legal_attacks=r['legal_attack_count'],attack_mass=r['attack_probability_mass'],
            alternative_kills=None if 'actual_alternative' not in r else r['actual_alternative']['after']['kills']-r['actual_alternative']['before']['kills']) for r in observations]))
    finally: rules.close()


if __name__=='__main__': main()
