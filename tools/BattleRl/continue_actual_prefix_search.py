"""Continue real PPO prefixes by exact root replay, never snapshot rebuilding."""
import argparse
import json
import os
from pathlib import Path
from coverage_tasks import split
from discover_task_witnesses import discover
from task_feasibility import SEQUENCE_GOAL, digest, file_hash
from train_ppo import Rules


def verify_prefix(rules, task, trace):
    current=rules.reset_task(task)
    by_key={action['Key']:i for i,action in enumerate(rules.actions)}
    prefix=[]
    for row in trace:
        if current['state']!=row['before']:
            raise ValueError('Actual prefix before-state differs on exact root replay')
        index=by_key.get(row['action']['Key'])
        if index is None or rules.actions[index]!=row['action'] or index not in current['legal']:
            raise ValueError('Actual prefix action definition or chosen-action legality differs')
        following=rules.step(index)
        if following['state']!=row['after'] or following['reward']!=row['reward']:
            raise ValueError('Actual prefix after-state/reward differs')
        prefix.append(index)
        current=following
    if current['done']:
        raise ValueError('Terminal prefix cannot be continued')
    return tuple(prefix)


def main():
    parser=argparse.ArgumentParser()
    for key in ('dll','source-pool','out'):
        parser.add_argument('--'+key,required=True)
    parser.add_argument('--seconds',type=float,default=35)
    parser.add_argument('--rule-step-budget',type=int,default=2400)
    args=parser.parse_args()
    if args.seconds<0 or args.rule_step_budget<0:
        parser.error('Nonnegative search budgets required')
    source=json.loads(Path(args.source_pool).read_text())
    definitions={r['draft_id']:r['task_definition'] for r in source['quarantine']}
    if source['partition']!='validation':
        raise ValueError('This continuation queue preserves validation isolation')
    output=Path(args.out)
    if output.exists():
        raise ValueError('Preserve previous actual search evidence')
    output.mkdir(parents=True)
    os.environ.update(BATTLE_RL_DLL=str(Path(args.dll).resolve()),BATTLE_RL_PROTOCOL='--serve-journey-artifacts',
                      BATTLE_RL_NATIVE_PAYMENTS='1',BATTLE_RL_NATIVE_CONVERSIONS='1',
                      BATTLE_RL_NATIVE_ENERGY_TARGETS='1',BATTLE_RL_ORIGINAL_OPENINGS='1')
    rules=Rules()
    results=[]
    try:
        for attempt in source['attempts']:
            path=Path(attempt['path'])
            if file_hash(path)!=attempt['sha256']:
                raise ValueError('Saved real attempt changed')
            task=definitions[attempt['draft_id']]
            if split(task)!='validation':
                raise ValueError('Source prefix crosses task composition split')
            document=json.loads(path.read_text())
            trace=document['trace']
            cutoff=next((i+1 for i,row in enumerate(trace) if
                         row['after']['taskProgress']['encounterIndex']>row['before']['taskProgress']['encounterIndex']
                         and row['after']['phase']=='Outside'),None)
            if cutoff is None:
                continue
            prefix=verify_prefix(rules,task,trace[:cutoff])
            target=output/f'{attempt["draft_id"]}_{attempt["attempt"]}'
            result=discover(rules,task,args.dll,target,seconds=args.seconds,
                            rule_step_budget=args.rule_step_budget,initial_prefix=prefix)
            result['source_prefix']=dict(path=str(path.resolve()),sha256=file_hash(path),operations=cutoff,
                                          trace_prefix_sha256=digest(trace[:cutoff]),verification_rule_steps=cutoff)
            (target/'continuation_result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
            results.append(result)
            print(json.dumps(dict(task=attempt['draft_id'],prefix_operations=cutoff,status=result['status'])),flush=True)
        pool=dict(schema_version=1,partition='validation',goal=SEQUENCE_GOAL,
                  tasks=[r['certificate'] for r in results if r['status']=='solvable_witness'],
                  quarantine=[r for r in results if r['status']!='solvable_witness'],
                  source_pool_sha256=file_hash(args.source_pool),code_sha256=file_hash(__file__),
                  selection='Continuation of frozen-policy-selected prefixes; not full validation distribution',
                  training_feedback=False)
        (output/'solvable_pool.json').write_text(json.dumps(pool,ensure_ascii=False,indent=2),encoding='utf-8')
    finally:
        rules.close()


if __name__=='__main__':
    main()
