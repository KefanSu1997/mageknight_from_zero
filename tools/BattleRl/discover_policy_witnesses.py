"""Frozen PPO as a real-rule witness finder, with all failed attempts retained.

Policy-conditioned solvable subsets are explicitly selection-biased; their
later evaluation is not a score for the full withheld task distribution.
"""
import argparse
import json
import os
from pathlib import Path
import numpy as np
from coverage_tasks import split
from evaluate_recurrent_ppo import load_frozen_policy, parameter_hash, partition_gate, rollout
from task_feasibility import SEQUENCE_GOAL, certify_witness, digest, eligible, file_hash
from train_ppo import Rules


def candidate_rows(document,partition,limit,selected_queue=False):
    if selected_queue:
        if partition!='train':
            raise ValueError('Selected training deficit queue cannot be used as withheld validation')
        by_id={r['task_id']:r for r in document['candidates']}
        rows=[by_id[i] for i in document['selected_train_drafts'][:limit]]
    else:
        rows=sorted((r for r in document['candidates'] if r['split']==partition),
                    key=lambda r:(r['estimated_cost'],r['task_id']))[:limit]
    if any(row['split']!=partition for row in rows):
        raise ValueError('Selected queue crosses task split')
    return rows


def main():
    parser=argparse.ArgumentParser()
    for key in ('dll','checkpoint','training-pool','drafts','out'):
        parser.add_argument('--'+key,required=True)
    parser.add_argument('--partition',choices=('train','validation'),default='validation')
    parser.add_argument('--max-tasks',type=int,default=2)
    parser.add_argument('--attempts-per-task',type=int,default=3)
    parser.add_argument('--seed',type=int,default=8001)
    parser.add_argument('--mode',choices=('sampled','joint_map','hierarchical_greedy'),default='sampled')
    parser.add_argument('--use-selected-train-queue',action='store_true')
    args=parser.parse_args()
    if min(args.max_tasks,args.attempts_per_task)<=0:
        parser.error('Positive bounded task/attempt counts required')
    if args.mode!='sampled' and args.attempts_per_task!=1:
        parser.error('Deterministic discovery must not repeat the same task rollout')
    os.environ.update(BATTLE_RL_DLL=str(Path(args.dll).resolve()),BATTLE_RL_PROTOCOL='--serve-journey-artifacts',
                      BATTLE_RL_NATIVE_PAYMENTS='1',BATTLE_RL_NATIVE_CONVERSIONS='1',
                      BATTLE_RL_NATIVE_ENERGY_TARGETS='1',BATTLE_RL_ORIGINAL_OPENINGS='1')
    with np.load(args.checkpoint,allow_pickle=False) as saved:
        os.environ['BATTLE_RL_SHARED_SKILLS']='1' if json.loads(str(saved['header']))['metadata'].get('native_shared_skills') else '0'
        os.environ['BATTLE_RL_SHAPESHIFT']='1' if json.loads(str(saved['header']))['metadata'].get('native_shapeshift') else '0'
        os.environ['BATTLE_RL_ORIGINAL_WORLD']='1' if json.loads(str(saved['header']))['metadata'].get('native_world') else '0'
    training=json.loads(Path(args.training_pool).read_text())['tasks']
    if not training or any(not eligible(c,args.dll,SEQUENCE_GOAL) for c in training):
        raise ValueError('Frozen source training pool is stale')
    document=json.loads(Path(args.drafts).read_text())
    rows=candidate_rows(document,args.partition,args.max_tasks,args.use_selected_train_queue)
    if not rows or any(split(r['task_definition'])!=args.partition for r in rows):
        raise ValueError('Draft partition mismatch')
    # Structural split gate only; these descriptors are NOT solvability certificates.
    descriptors=[dict(task=dict(definition=r['task_definition'])) for r in rows]
    if args.partition=='validation':
        partition_gate(training,descriptors,'validation')
    output=Path(args.out)
    if output.exists():
        raise ValueError('Do not overwrite real witness discovery attempts')
    output.mkdir(parents=True)
    rules=Rules()
    tasks,quarantine,attempts=[],[],[]
    try:
        initial=rules.reset_task(rows[0]['task_definition'])
        policy,_=load_frozen_policy(args.checkpoint,args.training_pool,args.dll,rules,initial)
        original,checkpoint_sha=parameter_hash(policy),file_hash(args.checkpoint)
        random=np.random.default_rng(args.seed)
        for row in rows:
            definition=row['task_definition']
            current=rules.reset_task(definition)
            descriptor=dict(task_id=row['task_id'],hero=definition['HeroId'],
                            task=dict(definition=definition,initial_state_sha256=digest(current['state'])))
            accepted=False
            for attempt in range(args.attempts_per_task):
                result=rollout(rules,policy,descriptor,random,args.mode)
                path=output/f'{row["task_id"]}_{attempt}.json'
                path.write_text(json.dumps(result,ensure_ascii=False),encoding='utf-8')
                attempts.append(dict(draft_id=row['task_id'],attempt=attempt,path=str(path.resolve()),
                                     sha256=file_hash(path),completed=result['completed_task']))
                if result['failure'] or result['report']['errors']:
                    raise RuntimeError('Real rule or audit failure preserved before stopping')
                if result['completed_task']:
                    witness=output/f'{row["task_id"]}_witness.json'
                    witness.write_text(json.dumps(dict(seed=definition['RandomSeed'],task_definition=definition,
                        goal=SEQUENCE_GOAL,trace=result['trace'],provenance='Frozen PPO discovery, not training/independent evaluation'),ensure_ascii=False),encoding='utf-8')
                    certificate=certify_witness(rules,witness,args.dll)
                    assert eligible(certificate,args.dll,SEQUENCE_GOAL)
                    tasks.append(certificate)
                    accepted=True
                    break
            if not accepted:
                quarantine.append(dict(draft_id=row['task_id'],task_definition=definition,status='unknown',
                                       impossibility_proved=False,excluded_from_victory_pool=True))
            print(json.dumps(dict(task=row['task_id'],certified=accepted)),flush=True)
        assert parameter_hash(policy)==original and file_hash(args.checkpoint)==checkpoint_sha
        result=dict(schema_version=1,goal=SEQUENCE_GOAL,partition=args.partition,tasks=tasks,quarantine=quarantine,
                    attempts=attempts,checkpoint_sha256=checkpoint_sha,code_sha256=file_hash(__file__),
                    seed=args.seed,optimizer_updates=0,frozen_parameters_verified=True,
                    mode=args.mode,
                    source_drafts_sha256=file_hash(args.drafts),source_training_pool_sha256=file_hash(args.training_pool),
                    selected_train_queue=args.use_selected_train_queue,
                    selection='Policy-conditioned witness subset; not full held-out distribution performance',
                    training_feedback=False)
        (output/'solvable_pool.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    finally:
        rules.close()


if __name__=='__main__':
    main()
