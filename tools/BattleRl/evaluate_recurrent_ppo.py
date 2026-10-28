"""Frozen checkpoint evaluation with explicit composition split gates.

Calibration evaluates training compositions and is never held-out evidence.
Validation/test must have their own certified witnesses and disjoint task
compositions. Evaluation actions come only from the saved actor.
"""
import argparse
from collections import Counter
import hashlib
import json
import os
from pathlib import Path
import time
import numpy as np

from coverage_tasks import combination, split
from recurrent_checkpoint import load_checkpoint
from recurrent_policy import RecurrentPolicy
from recurrent_architecture import build_policy
from task_feasibility import SEQUENCE_GOAL, digest, eligible, file_hash
from train_ppo import Rules
from trajectory_audit import audit
from original_data_signature import data_signature,world_source_hash


def partition_gate(training, evaluation, partition):
    train_compositions = {digest(combination(c['task']['definition'])) for c in training}
    for certificate in evaluation:
        task = certificate['task']['definition']
        composition = digest(combination(task))
        if partition == 'calibration':
            if composition not in train_compositions:
                raise ValueError('Calibration must identify a training composition')
        elif partition in ('validation', 'test'):
            if composition in train_compositions or split(task) != partition:
                raise ValueError('Held-out composition overlaps training or belongs to another split')
        else:
            raise ValueError('Unknown evaluation partition')


def parameter_hash(policy):
    value = hashlib.sha256()
    for key, array in policy.parameters().items():
        value.update(key.encode())
        value.update(array.tobytes())
    return value.hexdigest()


def load_frozen_policy(checkpoint, training_pool, dll, rules, initial):
    with np.load(checkpoint,allow_pickle=False) as arrays:
        metadata=json.loads(str(arrays['header']))['metadata']
    expected=dict(metadata)
    policy, architecture_metadata = build_policy(len(initial['features']), rules.actions,
        rules.info['featureSchema'], bool(metadata.get('entity_transformer',False)), bool(metadata.get('shared_argument_head',False)), bool(metadata.get('printed_information',False)))
    if metadata.get('native_world'):expected['world_source_sha256']=world_source_hash()
    expected.update(rules_sha256=file_hash(dll),pool_sha256=file_hash(training_pool),task_goal=SEQUENCE_GOAL,
                    rules_data_sha256=data_signature()['sha256'],
                    architecture=f'MLP256-256-128_GRU256_factored{len({a["Kind"] for a in rules.actions})}_original_catalog',
                    history='previous_actual_action_embedding32_and_raw_reward_div20',
                    catalog_sha256=digest(rules.actions),feature_schema=rules.info['featureSchema'])
    expected.update(architecture_metadata)
    for field,source in (('recurrent_policy_sha256','recurrent_policy.py'),('recurrent_core_sha256','recurrent_core.py'),
                         ('ppo_loss_sha256','train_ppo.py'),('training_objective_sha256','training_reward.py'),
                         ('task_sampler_sha256','certified_task_sampler.py')):
        expected[field]=file_hash(Path(__file__).with_name(source))
    if metadata.get('reward_mode')=='terminal_sequence':
        expected['sequence_reward_sha256']=file_hash(Path(__file__).with_name('sequence_training_reward.py'))
    if 'planning_reward_sha256' in metadata:
        expected['planning_reward_sha256']=file_hash(Path(__file__).with_name('planning_revisit_reward.py'))
    if 'mixed_task_sampler_sha256' in metadata:
        expected['mixed_task_sampler_sha256']=file_hash(Path(__file__).with_name('mixed_task_pool.py'))
    if 'progress_reward_sha256' in metadata:
        expected['progress_reward_sha256']=file_hash(Path(__file__).with_name('progress_potential_reward.py'))
    if 'encounter_sampler_sha256' in metadata:
        expected['encounter_sampler_sha256']=file_hash(Path(__file__).with_name('encounter_floor_sampler.py'))
    if 'skill_sampler_sha256' in metadata:
        expected['skill_sampler_sha256']=file_hash(Path(__file__).with_name('skill_floor_sampler.py'))
    if 'target_skill_sampler_sha256' in metadata:
        expected['target_skill_sampler_sha256']=file_hash(Path(__file__).with_name('targeted_skill_floor_sampler.py'))
    if 'shared_planning_reward_sha256' in metadata:
        expected['shared_planning_reward_sha256']=file_hash(Path(__file__).with_name('shared_planning_revisit_reward.py'))
    if 'world_consumer_reward_sha256' in metadata:
        expected['world_consumer_reward_sha256']=file_hash(Path(__file__).with_name('world_consumer_potential_reward.py'))
    parameters,_,_,replay=load_checkpoint(checkpoint,expected)
    policy.bind_parameters(parameters)
    return policy,replay


def select_action(policy, probabilities, random, mode):
    if mode=='joint_map':
        return int(np.argmax(probabilities))
    if mode=='hierarchical_greedy':
        return policy.head.greedy(probabilities)
    if mode=='sampled':
        return int(random.choice(len(probabilities),p=probabilities))
    raise ValueError('Unknown decoder; do not silently use sampling')


def selection_evidence(policy, probabilities, legal, selected):
    groups=policy.head.p['action_kind_groups']
    mass=np.bincount(groups,weights=probabilities,minlength=len(policy.head.p['bk']))
    counts=np.bincount(groups[legal],minlength=len(mass))
    top=np.argsort(-mass)[:3]
    joint=int(np.argmax(probabilities))
    hierarchical=policy.head.greedy(probabilities)
    return dict(selected_probability=float(probabilities[selected]),
                selected_kind_probability=float(mass[groups[selected]]),
                joint_map_key=policy.actions[joint]['Key'],
                hierarchical_greedy_key=policy.actions[hierarchical]['Key'],
                decoders_disagree=joint!=hierarchical,
                top_kinds=[dict(kind=next(a['Kind'] for a,g in zip(policy.actions,groups) if g==index),
                                probability=float(mass[index]),legal_tuples=int(counts[index])) for index in top])


def rollout(rules, policy, certificate, random, mode):
    current = rules.reset_task(certificate['task']['definition'])
    if digest(current['state']) != certificate['task']['initial_state_sha256']:
        raise ValueError('Certified initial state changed')
    memory, previous_action, previous_reward = None, -1, 0.
    trace, boundaries = [], []
    failure = None
    while not current['done']:
        probabilities, _, following_memory = policy.step(current['features'], current['legal'],
                                                         previous_action, previous_reward, memory)
        selected = select_action(policy,probabilities,random,mode)
        if selected not in current['legal']:
            raise ValueError('Frozen actor chose illegal action')
        try:
            following = rules.step(selected)
        except RuntimeError as error:
            failure = dict(error=str(error), attempted_action=rules.actions[selected], before=current['state'])
            break
        trace.append(dict(before=current['state'], action=rules.actions[selected],
                          after=following['state'], reward=following['reward'],
                          selection=selection_evidence(policy,probabilities,current['legal'],selected)))
        if rules.actions[selected]['Kind'] in ('nextTurn', 'fight', 'roundFinish'):
            boundaries.append(dict(step=len(trace)-1, kind=rules.actions[selected]['Kind'],
                                   memory_carried_to_next_step=True,
                                   following_memory_sha256=hashlib.sha256(following_memory.tobytes()).hexdigest(),
                                   day_before=current['state']['dayPart'], day_after=following['state']['dayPart']))
        memory = following_memory  # Only the native episode reset above clears memory.
        previous_action, previous_reward = selected, following['reward']
        current = following
    report = audit(trace)
    return dict(task_id=certificate['task_id'], hero=certificate['hero'], trace=trace,
                boundaries=boundaries, report=report, failure=failure,
                completed_task=bool(current['state'].get('taskProgress', {}).get('sequenceCompleted')),
                native_done=current['done'],
                stop_reason=current['state']['stopReason'],
                formal_group_wins=sum(r['after']['phase']=='End' and r['after']['won']
                                      and not r['before']['won'] for r in trace))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--dll', required=True)
    parser.add_argument('--checkpoint', required=True)
    parser.add_argument('--training-pool', required=True)
    parser.add_argument('--evaluation-pool', required=True)
    parser.add_argument('--partition', choices=('calibration','validation','test'), required=True)
    parser.add_argument('--mode', choices=('sampled','joint_map','hierarchical_greedy'), default='joint_map')
    parser.add_argument('--repeats', type=int, default=1)
    parser.add_argument('--seed', type=int, default=7001)
    parser.add_argument('--out', required=True)
    args = parser.parse_args()
    if args.repeats<=0:
        parser.error('Positive evaluation repeat count required')
    os.environ.update(BATTLE_RL_DLL=str(Path(args.dll).resolve()), BATTLE_RL_PROTOCOL='--serve-journey-artifacts',
                      BATTLE_RL_NATIVE_PAYMENTS='1', BATTLE_RL_NATIVE_CONVERSIONS='1',
                      BATTLE_RL_NATIVE_ENERGY_TARGETS='1', BATTLE_RL_ORIGINAL_OPENINGS='1')
    with np.load(args.checkpoint,allow_pickle=False) as saved:
        os.environ['BATTLE_RL_SHARED_SKILLS']='1' if json.loads(str(saved['header']))['metadata'].get('native_shared_skills') else '0'
        os.environ['BATTLE_RL_SHAPESHIFT']='1' if json.loads(str(saved['header']))['metadata'].get('native_shapeshift') else '0'
        os.environ['BATTLE_RL_ORIGINAL_WORLD']='1' if json.loads(str(saved['header']))['metadata'].get('native_world') else '0'
    training = json.loads(Path(args.training_pool).read_text())['tasks']
    evaluation_document=json.loads(Path(args.evaluation_pool).read_text())
    evaluation = evaluation_document['tasks']
    if not training or not evaluation or any(not eligible(c,args.dll,SEQUENCE_GOAL) for c in training+evaluation):
        raise ValueError('Evaluation requires current complete-task certificates')
    partition_gate(training, evaluation, args.partition)
    training_document=json.loads(Path(args.training_pool).read_text())
    if training_document.get('unknown_tasks'):
        from mixed_task_pool import validate_pool
        partition_gate(validate_pool(training_document,args.dll),evaluation,args.partition)
    output = Path(args.out)
    if output.exists():
        raise ValueError('Existing evaluation evidence must not be overwritten')
    output.mkdir(parents=True)
    rules = Rules()
    try:
        initial = rules.reset_task(evaluation[0]['task']['definition'])
        policy,replay=load_frozen_policy(args.checkpoint,args.training_pool,args.dll,rules,initial)
        original = parameter_hash(policy)
        checkpoint_sha = file_hash(args.checkpoint)
        random = np.random.default_rng(args.seed)  # Independent of the training RNG.
        manifest = dict(checkpoint=str(Path(args.checkpoint).resolve()), checkpoint_sha256=checkpoint_sha,
                        evaluator_sha256=file_hash(__file__), partition=args.partition, mode=args.mode,
                        evaluation_seed=args.seed, training_episodes=replay['next_episode'],
                        held_out=args.partition!='calibration', training_pool_sha256=file_hash(args.training_pool),
                        evaluation_pool_sha256=file_hash(args.evaluation_pool), optimizer_updates=0,
                        feasibility_selection=evaluation_document.get('selection','not_specified'),
                        unresolved_pool_entries=len(evaluation_document.get('quarantine',[])),
                        scope='Frozen actor task completion; original-rule and Unity acceptance remain independent')
        (output/'manifest.json').write_text(json.dumps(manifest,indent=2))
        results, started = [], time.perf_counter()
        for repeat in range(args.repeats):
            for certificate in evaluation:
                result = rollout(rules,policy,certificate,random,args.mode)
                result['repeat'] = repeat
                index = len(results)
                (output/f'episode_{index}.json').write_text(json.dumps(result,ensure_ascii=False),encoding='utf-8')
                results.append(result)
                if result['failure'] or result['report']['errors']:
                    raise RuntimeError('Actual evaluation failure preserved; no acceptance claim')
        assert parameter_hash(policy)==original and file_hash(args.checkpoint)==checkpoint_sha
        summary = dict(partition=args.partition, mode=args.mode, held_out=manifest['held_out'],
                       feasibility_selection=manifest['feasibility_selection'],
                       unresolved_pool_entries=manifest['unresolved_pool_entries'],
                       episodes=len(results), completed_tasks=sum(r['completed_task'] for r in results),
                       formal_group_wins=sum(r['formal_group_wins'] for r in results),
                       stop_reasons=dict(Counter(r['stop_reason'] or 'native_terminal_without_reason' for r in results)),
                       per_hero={hero:dict(episodes=sum(r['hero']==hero for r in results),
                                          completed=sum(r['hero']==hero and r['completed_task'] for r in results))
                                 for hero in sorted({r['hero'] for r in results})},
                       actual_steps=sum(len(r['trace']) for r in results), seconds=time.perf_counter()-started,
                       frozen_parameters_verified=True, checkpoint_unchanged=True,
                       audit_checks=sum(r['report']['checks'] for r in results),
                       gaps=['broad_held_out_task_pool','full_fees_materials_consumers_acceptance','Unity_UI_acceptance'])
        (output/'summary.json').write_text(json.dumps(summary,indent=2))
        print(json.dumps(summary),flush=True)
    finally:
        rules.close()


if __name__=='__main__':
    main()
