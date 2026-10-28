"""Sequence PPO over certified tasks and the real frozen MageKnight rules.

Whole episode sequences are recomputed under current weights in every epoch.
No shuffled individual time steps, teacher actions or hidden audit inputs.
The current seed-backed certified pool is an integration stage, not full scope.
"""
import argparse
import json
import os
from pathlib import Path
import time
import shutil
os.environ['OPENBLAS_NUM_THREADS'] = '1'
os.environ['OMP_NUM_THREADS'] = '1'
os.environ['MKL_NUM_THREADS'] = '1'
import numpy as np

from recurrent_policy import RecurrentPolicy
from recurrent_architecture import build_policy
from recurrent_checkpoint import Adam, load_checkpoint, save_checkpoint
from task_feasibility import SEQUENCE_GOAL, digest, eligible, file_hash
from training_reward import CompletionReward
from train_ppo import Rules, estimate_advantages, prepare_training_start
from trajectory_audit import audit
from certified_task_sampler import CertifiedTaskSampler
from sequence_training_reward import TerminalSequenceReward
from original_data_signature import data_signature,world_source_hash
from planning_revisit_reward import PlanningRevisitReward
from mixed_task_pool import MixedTaskSampler, validate_pool
from progress_potential_reward import ProgressPotentialReward
from encounter_floor_sampler import EncounterFloorSampler
from skill_floor_sampler import SkillFloorSampler
from targeted_skill_floor_sampler import TargetedSkillFloorSampler
from shared_planning_revisit_reward import SharedPlanningRevisitReward
from world_consumer_potential_reward import WorldConsumerPotentialReward


class EpisodeFailure(RuntimeError):
    def __init__(self, reason, trace, attempted_action=None):
        super().__init__(reason)
        self.trace, self.attempted_action = trace, attempted_action


def override_learning_rate(optimizer, requested):
    if requested is None:
        return None
    if not np.isfinite(requested) or requested<=0:
        raise ValueError('Learning rate must be positive and finite')
    previous=optimizer.learning_rate
    optimizer.learning_rate=float(requested)
    return dict(previous=previous,requested=optimizer.learning_rate,
                scope='Only Adam step size changed; parameter/momentum/RNG history retained')


def collect_episode(rules, policy, random, certificate,reward_mode='existing', enter_combat=False, planning_cost=0.,progress_scale=0.,shared_planning_cost=0.,world_consumer_scale=0.,step_cost=0.):
    task = certificate['task']
    current = rules.reset_task(task['definition']) if task['generator']=='independent_original_factors_v1' else rules.reset(task['seed'])
    if digest(current['state']) != certificate['task']['initial_state_sha256']:
        raise ValueError('Certified task initial state changed')
    current, curriculum_prefix = prepare_training_start(rules,current,enter_combat)
    observations, previous_actions, previous_rewards, legal, selected = [], [], [], [], []
    old_log, values, rewards, terminal, trace = [], [], [], [], []
    memory, previous_action, previous_reward = None, -1, 0.
    if reward_mode=='terminal_sequence' and task['goal']!=SEQUENCE_GOAL:
        raise ValueError('Terminal sequence reward requires a complete-task certificate')
    objective = TerminalSequenceReward() if reward_mode=='terminal_sequence' else CompletionReward(True)
    planning_objective=PlanningRevisitReward(planning_cost)
    progress_objective=ProgressPotentialReward(progress_scale)
    shared_planning_objective=SharedPlanningRevisitReward(shared_planning_cost)
    world_objective=WorldConsumerPotentialReward(world_consumer_scale)
    while not current['done']:
        try:
            probability, value, memory = policy.step(current['features'], current['legal'],
                                                       previous_action, previous_reward, memory)
        except Exception as error:
            failure = EpisodeFailure(str(error), curriculum_prefix + trace)
            failure.current_state = current['state']
            raise failure from error
        action = int(random.choice(len(probability), p=probability))
        if action not in current['legal']:
            raise ValueError('Policy sampled a formally illegal action')
        try:
            following = rules.step(action)
        except RuntimeError as error:
            raise EpisodeFailure(str(error), curriculum_prefix + trace, rules.actions[action]) from error
        reward = objective.step(current['state'], following['state'], following['reward'], following['done'])
        planning_penalty=planning_objective.step(rules.actions[action],current['state'],following['state'])
        reward+=planning_penalty
        progress_shaping=progress_objective.step(current['state'],following['state'],following['done'])
        reward+=progress_shaping
        shared_penalty=shared_planning_objective.step(rules.actions[action],current['state'],following['state'])
        reward+=shared_penalty
        world_shaping=world_objective.step(current['state'],following['state'],following['done'])
        reward+=world_shaping
        reward-=step_cost
        if (reward_mode=='existing' and task['goal'] == SEQUENCE_GOAL and following['done']
                and not following['state'].get('taskProgress', {}).get('sequenceCompleted')
                and objective.whole_wins > 0):
            # Earlier group wins cannot stand for completing the selected sequence.
            reward -= 10
        observations.append(current['features'])
        previous_actions.append(previous_action)
        previous_rewards.append(previous_reward)
        legal.append(current['legal'])
        selected.append(action)
        old_log.append(np.log(max(probability[action], 1e-30)))
        values.append(value)
        rewards.append(reward)
        terminal.append(following['done'])
        trace.append(dict(before=current['state'], action=rules.actions[action],
                          action_index=action, selected_probability=float(probability[action]), value_prediction=float(value),
                          after=following['state'], reward=following['reward'], training_reward=reward,
                          training_planning_penalty=planning_penalty,training_progress_shaping=progress_shaping,
                          training_shared_planning_penalty=shared_penalty,training_world_consumer_shaping=world_shaping,
                          training_step_penalty=-step_cost))
        # No reset at nextTurn, fight, advance, or roundFinish.
        previous_action, previous_reward = action, following['reward']
        current = following
    if not trace:
        raise ValueError('Certified task supplied no formal episode')
    advantage = estimate_advantages(np.asarray(rewards), np.asarray(values),
                                    np.asarray(terminal), 0., .995, .99)
    report = audit(curriculum_prefix + trace)
    report['curriculum_prefix_operations']=len(curriculum_prefix)
    report['policy_selected_fights']=sum(r['action']['Kind']=='fight' for r in trace)
    if report['errors']:
        raise EpisodeFailure('Original rule audit failed: ' + str(report['errors']), curriculum_prefix + trace)
    return dict(observations=np.asarray(observations)[:, None],
                previous_actions=np.asarray(previous_actions)[:, None],
                previous_rewards=np.asarray(previous_rewards)[:, None], legal=legal,
                selected=np.asarray(selected)[:, None], old_log=np.asarray(old_log),
                raw_advantage=advantage, returns=advantage + np.asarray(values),
                rollout_values=np.asarray(values),
                trace=trace, curriculum_prefix=curriculum_prefix, report=report,
                task_id=certificate['task_id'], seed=certificate['task']['seed'],feasibility_status=certificate['status'])


def batch_gradients(policy, episodes):
    total = sum(len(e['trace']) for e in episodes)
    gradients = {key: np.zeros_like(value) for key, value in policy.parameters().items()}
    loss = 0.
    for episode in episodes:
        episode_loss, episode_gradients, _ = policy.loss_grad(
            episode['observations'], episode['previous_actions'], episode['previous_rewards'],
            episode['legal'], episode['selected'], episode['old_log'], episode['advantage'], episode['returns'])
        weight = len(episode['trace']) / total
        loss += weight * episode_loss
        for key in gradients:
            # This episode owns these disposable arrays. Scale in place to avoid
            # allocating a parameter-sized temporary for every sequence.
            episode_gradients[key] *= weight
            gradients[key] += episode_gradients[key]
    return loss, gradients


def updated_kl(policy, episodes, with_values=False):
    differences = []
    for e in episodes:
        if with_values:
            # Reuse the same recurrent forward and legal-head calls needed for
            # KL. Preserve the actual history and do not reset at turn boundaries.
            hidden, _, _, _ = policy._sequence(e['observations'], e['previous_actions'], e['previous_rewards'])
            logs, values = [], []
            for row, legal, action in zip(hidden.reshape(-1, hidden.shape[-1]), e['legal'], e['selected'].ravel()):
                probabilities, value = policy.head.action_forward(row, legal)
                logs.append(np.log(max(probabilities[action], 1e-30)))
                values.append(value)
            new_log=np.asarray(logs)
            e['updated_values']=np.asarray(values)
        else:
            new_log = policy.sequence_log_probabilities(e['observations'], e['previous_actions'],
                                                         e['previous_rewards'], e['legal'], e['selected'])
        ratio = new_log - e['old_log']
        differences.extend(np.maximum(np.expm1(ratio) - ratio, 0.))
    return float(np.mean(differences))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--dynamic-single-curriculum', action='store_true')
    parser.add_argument('--fixed-task-pool', action='store_true')
    parser.add_argument('--evaluation-pool')
    parser.add_argument('--evaluation-every', type=int, default=20)
    parser.add_argument('--dll', required=True)
    parser.add_argument('--pool', required=True)
    parser.add_argument('--out', required=True)
    parser.add_argument('--updates', type=int, default=8)
    parser.add_argument('--episodes-per-update', type=int, default=4)
    parser.add_argument('--epochs', type=int, default=6)
    parser.add_argument('--resume')
    parser.add_argument('--workers', type=int, default=1)
    parser.add_argument('--change-collection', action='store_true')
    parser.add_argument('--step-cost', type=float)
    parser.add_argument('--change-step-objective', action='store_true')
    parser.add_argument('--printed-information', action='store_true')
    parser.add_argument('--learning-rate',type=float)
    parser.add_argument('--reward-mode',choices=('existing','terminal_sequence'),default='existing')
    parser.add_argument('--change-reward-objective',action='store_true')
    parser.add_argument('--mixed-combat-start',action='store_true')
    parser.add_argument('--change-start-curriculum',action='store_true')
    parser.add_argument('--planning-revisit-cost',type=float)
    parser.add_argument('--change-planning-objective',action='store_true')
    parser.add_argument('--progress-potential-scale',type=float)
    parser.add_argument('--change-progress-objective',action='store_true')
    parser.add_argument('--encounter-floor-mass',type=float)
    parser.add_argument('--change-task-sampling',action='store_true')
    parser.add_argument('--native-shared-skills',action='store_true')
    parser.add_argument('--native-shapeshift',action='store_true')
    parser.add_argument('--native-world',action='store_true')
    parser.add_argument('--entity-transformer',action='store_true')
    parser.add_argument('--shared-argument-head',action='store_true')
    parser.add_argument('--skill-floor-mass',type=float)
    parser.add_argument('--shared-planning-revisit-cost',type=float)
    parser.add_argument('--change-shared-planning-objective',action='store_true')
    parser.add_argument('--skill-floor-target',action='append')
    parser.add_argument('--world-consumer-potential-scale',type=float)
    parser.add_argument('--change-world-consumer-objective',action='store_true')
    parser.add_argument('--baseline-cache',help='Reuse bound completed baseline archives; rerun only missing tasks')
    parser.add_argument('--prior-run',help='Continue a saved fixed experiment with cumulative update numbers')
    args = parser.parse_args()
    if args.workers < 1 or (args.step_cost is not None and (not np.isfinite(args.step_cost) or args.step_cost < 0)):
        parser.error('Positive workers and finite nonnegative training step cost required')
    if min(args.updates, args.episodes_per_update, args.epochs) <= 0:
        parser.error('Positive update, episode and epoch counts required')
    os.environ.update(BATTLE_RL_DLL=str(Path(args.dll).resolve()),
                      BATTLE_RL_SHARED_SKILLS='1' if args.native_shared_skills else '0',
                      BATTLE_RL_SHAPESHIFT='1' if args.native_shapeshift else '0',
                      BATTLE_RL_ORIGINAL_WORLD='1' if args.native_world else '0',
                      BATTLE_RL_PROTOCOL='--serve-journey-artifacts',
                      BATTLE_RL_NATIVE_PAYMENTS='1', BATTLE_RL_NATIVE_CONVERSIONS='1',
                      BATTLE_RL_NATIVE_ENERGY_TARGETS='1', BATTLE_RL_ORIGINAL_OPENINGS='1')
    pool = json.loads(Path(args.pool).read_text(encoding='utf-8'))
    certificates = pool['tasks']
    if not certificates or any(not eligible(c, args.dll, pool['goal']) for c in certificates):
        raise ValueError('Victory pool contains unknown, stale, unproved or empty tasks')
    mixed=bool(pool.get('unknown_tasks'))
    if mixed: certificates=validate_pool(pool,args.dll)
    output = Path(args.out)
    if output.exists():
        raise ValueError('Output already exists; inspect a live/finished run instead of overwriting it')
    output.mkdir(parents=True)
    rules = Rules()
    episode_index = 0
    total_steps = 0
    parallel = None
    worker_states = None
    step_cost = args.step_cost or 0.
    collection_change = None
    step_change = None
    try:
        first = certificates[0]['task']
        initial = rules.reset_task(first['definition']) if first['generator']=='independent_original_factors_v1' else rules.reset(first['seed'])
        if any(c['task']['catalog_sha256'] != digest(rules.actions)
               or c['task']['feature_schema'] != rules.info['featureSchema'] for c in certificates):
            raise ValueError('Certified catalog/features differ from the actual environment')
        policy, architecture_metadata = build_policy(len(initial['features']), rules.actions,
            rules.info['featureSchema'], args.entity_transformer, args.shared_argument_head, args.printed_information)
        metadata = dict(rules_sha256=file_hash(args.dll), catalog_sha256=digest(rules.actions),
                        rules_data_sha256=data_signature()['sha256'],
                        feature_schema=rules.info['featureSchema'], task_goal=pool['goal'],
                        pool_sha256=file_hash(args.pool), architecture=f'MLP256-256-128_GRU256_factored{len({a["Kind"] for a in rules.actions})}_original_catalog',
                        history='previous_actual_action_embedding32_and_raw_reward_div20',
                        recurrent_policy_sha256=file_hash(Path(__file__).with_name('recurrent_policy.py')),
                        recurrent_core_sha256=file_hash(Path(__file__).with_name('recurrent_core.py')),
                        ppo_loss_sha256=file_hash(Path(__file__).with_name('train_ppo.py')),
                        training_objective_sha256=file_hash(Path(__file__).with_name('training_reward.py')))
        metadata['task_sampler_sha256'] = file_hash(Path(__file__).with_name('certified_task_sampler.py'))
        metadata.update(architecture_metadata)
        if args.native_shared_skills:metadata['native_shared_skills']=True
        if args.native_shapeshift:metadata['native_shapeshift']=True
        if args.native_world:metadata.update(native_world=True,world_source_sha256=world_source_hash())
        floor_mass=0. if args.encounter_floor_mass is None else args.encounter_floor_mass
        skill_mass=0. if args.skill_floor_mass is None else args.skill_floor_mass
        skill_targets=sorted(set(args.skill_floor_target or []))
        if not np.isfinite(skill_mass) or not 0<=skill_mass<1:raise ValueError('Invalid skill coverage mass')
        if not np.isfinite(floor_mass) or not 0<=floor_mass<1: raise ValueError('Invalid encounter coverage mass')
        if mixed: metadata['mixed_task_sampler_sha256']=file_hash(Path(__file__).with_name('mixed_task_pool.py'))
        optimizer, random = Adam(policy.parameters()), np.random.default_rng(7)
        sampler_type=MixedTaskSampler if mixed else CertifiedTaskSampler
        def make_sampler(state=None):
            if (state or {}).get('dynamic'):
                if not args.dynamic_single_curriculum:raise ValueError('Dynamic checkpoint requires explicit curriculum flag')
                from dynamic_single_curriculum import DynamicSingleCurriculum
                return DynamicSingleCurriculum(certificates,rules,output,state)
            if skill_targets:
                if skill_mass<=0:raise ValueError('Targeted coverage needs positive skill floor mass')
                return TargetedSkillFloorSampler(certificates,state,floor_mass,skill_mass,skill_targets)
            if skill_mass>0:return SkillFloorSampler(certificates,state,floor_mass,skill_mass)
            return EncounterFloorSampler(certificates,state,floor_mass) if floor_mass>0 else sampler_type(certificates,state)
        sampler = None if args.resume else make_sampler()
        objective_change=None
        curriculum_change=None
        prefix_operations=0
        data_binding=None
        planning_change=None
        progress_change=None
        sampling_change=None
        skill_sampling_change=None
        shared_planning_change=None
        world_consumer_change=None
        world_consumer_scale=0. if args.world_consumer_potential_scale is None else args.world_consumer_potential_scale
        WorldConsumerPotentialReward(world_consumer_scale)
        skill_target_change=None
        shared_planning_cost=0. if args.shared_planning_revisit_cost is None else args.shared_planning_revisit_cost
        SharedPlanningRevisitReward(shared_planning_cost)
        progress_scale=0. if args.progress_potential_scale is None else args.progress_potential_scale
        ProgressPotentialReward(progress_scale)
        planning_cost=0. if args.planning_revisit_cost is None else args.planning_revisit_cost
        PlanningRevisitReward(planning_cost)
        if args.resume:
            with np.load(args.resume,allow_pickle=False) as arrays:
                old_metadata=json.loads(str(arrays['header']))['metadata']
                old_mode=old_metadata.get('reward_mode','existing')
            if bool(old_metadata.get('printed_information',False)) != args.printed_information:
                raise ValueError('Printed input differs; complete parameter/Adam migration required')
            prior_workers=old_metadata.get('collection_workers',1)
            if prior_workers!=args.workers and not args.change_collection:
                raise ValueError('Collector differs; explicitly declare the new independent RNG streams')
            collection_change=None if prior_workers==args.workers else dict(before=prior_workers,after=args.workers)
            prior_step=old_metadata.get('training_step_cost',0.)
            step_cost=prior_step if args.step_cost is None else args.step_cost
            if prior_step!=step_cost and not args.change_step_objective:
                raise ValueError('Step objective differs; explicitly declare experimental change')
            step_change=None if prior_step==step_cost else dict(before=prior_step,after=step_cost)
            prior_floor=old_metadata.get('encounter_floor_mass',0.)
            prior_world_scale=old_metadata.get('world_consumer_potential_scale',0.)
            world_consumer_scale=prior_world_scale if args.world_consumer_potential_scale is None else args.world_consumer_potential_scale
            WorldConsumerPotentialReward(world_consumer_scale)
            if prior_world_scale!=world_consumer_scale and not args.change_world_consumer_objective:
                raise ValueError('World potential differs; explicit experimental objective change required')
            if prior_world_scale!=world_consumer_scale:world_consumer_change=dict(before=prior_world_scale,after=world_consumer_scale)
            if bool(old_metadata.get('entity_transformer',False)) != args.entity_transformer:
                raise ValueError('Actor architecture differs; explicit complete checkpoint migration required')
            if bool(old_metadata.get('shared_argument_head',False)) != args.shared_argument_head:
                raise ValueError('Action head differs; explicit complete checkpoint migration required')
            prior_targets=old_metadata.get('skill_floor_targets',[])
            skill_targets=prior_targets if args.skill_floor_target is None else sorted(set(args.skill_floor_target))
            if prior_targets!=skill_targets and not args.change_task_sampling:
                raise ValueError('Skill task targets differ; explicit experimental change required')
            if prior_targets!=skill_targets:skill_target_change=dict(before=prior_targets,after=skill_targets)
            prior_shared_cost=old_metadata.get('shared_planning_revisit_cost',0.)
            shared_planning_cost=prior_shared_cost if args.shared_planning_revisit_cost is None else args.shared_planning_revisit_cost
            SharedPlanningRevisitReward(shared_planning_cost)
            if prior_shared_cost!=shared_planning_cost and not args.change_shared_planning_objective:
                raise ValueError('Shared planning objective differs; explicit experimental change required')
            if prior_shared_cost!=shared_planning_cost:shared_planning_change=dict(before=prior_shared_cost,after=shared_planning_cost)
            prior_skill=old_metadata.get('skill_floor_mass',0.)
            skill_mass=prior_skill if args.skill_floor_mass is None else args.skill_floor_mass
            if not np.isfinite(skill_mass) or not 0<=skill_mass<1:raise ValueError('Invalid skill coverage mass')
            if prior_skill!=skill_mass and not args.change_task_sampling:
                raise ValueError('Skill task sampling differs; explicit experimental change required')
            if prior_skill!=skill_mass:skill_sampling_change=dict(before=prior_skill,after=skill_mass)
            floor_mass=prior_floor if args.encounter_floor_mass is None else args.encounter_floor_mass
            if not np.isfinite(floor_mass) or not 0<=floor_mass<1:raise ValueError('Invalid encounter coverage mass')
            if prior_floor!=floor_mass and not args.change_task_sampling:
                raise ValueError('Task sampling differs; explicit experimental change required')
            if prior_floor!=floor_mass:sampling_change=dict(before=prior_floor,after=floor_mass)
            prior_progress=old_metadata.get('progress_potential_scale',0.)
            progress_scale=prior_progress if args.progress_potential_scale is None else args.progress_potential_scale
            ProgressPotentialReward(progress_scale)
            if prior_progress!=progress_scale and not args.change_progress_objective:
                raise ValueError('Progress objective differs; explicit experimental change required')
            if prior_progress!=progress_scale:progress_change=dict(before=prior_progress,after=progress_scale)
            prior_cost=old_metadata.get('planning_revisit_cost',0.)
            planning_cost=prior_cost if args.planning_revisit_cost is None else args.planning_revisit_cost
            PlanningRevisitReward(planning_cost)
            if prior_cost!=planning_cost and not args.change_planning_objective:
                raise ValueError('Planning objective differs; explicit experimental change required')
            if prior_cost!=planning_cost: planning_change=dict(before=prior_cost,after=planning_cost)
            if old_mode not in ('existing','terminal_sequence'):
                raise ValueError('Unknown saved training reward objective')
            if old_mode!=args.reward_mode and not args.change_reward_objective:
                raise ValueError('Reward objective differs; explicit experimental migration required')
            expected=dict(metadata)
            if 'world_consumer_reward_sha256' in old_metadata:
                expected.update(world_consumer_reward_sha256=file_hash(Path(__file__).with_name('world_consumer_potential_reward.py')),world_consumer_potential_scale=prior_world_scale)
            if 'target_skill_sampler_sha256' in old_metadata:
                expected.update(target_skill_sampler_sha256=file_hash(Path(__file__).with_name('targeted_skill_floor_sampler.py')),skill_floor_targets=prior_targets)
            if 'shared_planning_reward_sha256' in old_metadata:
                expected.update(shared_planning_reward_sha256=file_hash(Path(__file__).with_name('shared_planning_revisit_reward.py')),shared_planning_revisit_cost=prior_shared_cost)
            if 'skill_sampler_sha256' in old_metadata:
                expected.update(skill_sampler_sha256=file_hash(Path(__file__).with_name('skill_floor_sampler.py')),skill_floor_mass=prior_skill)
            if 'encounter_sampler_sha256' in old_metadata:
                expected.update(encounter_sampler_sha256=file_hash(Path(__file__).with_name('encounter_floor_sampler.py')),
                                encounter_floor_mass=prior_floor)
            if 'progress_reward_sha256' in old_metadata:
                expected.update(progress_reward_sha256=file_hash(Path(__file__).with_name('progress_potential_reward.py')),
                                progress_potential_scale=prior_progress)
            if 'planning_reward_sha256' in old_metadata:
                expected.update(planning_reward_sha256=file_hash(Path(__file__).with_name('planning_revisit_reward.py')),
                                planning_revisit_cost=prior_cost)
            if old_mode=='terminal_sequence':
                expected.update(reward_mode=old_mode,sequence_reward_sha256=file_hash(Path(__file__).with_name('sequence_training_reward.py')))
            if 'parallel_rollouts_sha256' in old_metadata:
                expected.update(parallel_rollouts_sha256=file_hash(Path(__file__).with_name('parallel_rollouts.py')))
            if 'collection_workers' in old_metadata:expected['collection_workers']=prior_workers
            if 'training_step_cost' in old_metadata:expected['training_step_cost']=prior_step
            if 'dynamic_curriculum_sha256' in old_metadata:
                if not args.dynamic_single_curriculum:raise ValueError('Dynamic curriculum cannot be silently removed')
                expected['dynamic_curriculum_sha256']=file_hash(Path(__file__).with_name('dynamic_single_curriculum.py'))
            if old_metadata.get('fixed_task_pool'):
                if not args.fixed_task_pool or not args.evaluation_pool:
                    raise ValueError('Fixed train/eval experiment requires its saved flags')
                expected['fixed_task_pool']=True
                expected['evaluation_pool_sha256']=file_hash(args.evaluation_pool)
            parameters, optimizer, random, replay = load_checkpoint(args.resume, expected)
            if prior_workers==args.workers and args.workers>1:
                worker_states=replay.get('parallel_worker_rngs')
                if worker_states is None:raise ValueError('Parallel checkpoint lacks exact worker RNG states')
            prior_curriculum=replay.get('mixed_combat_start',False)
            if prior_curriculum!=args.mixed_combat_start and not args.change_start_curriculum:
                raise ValueError('Start curriculum differs; explicit experimental change required')
            if prior_curriculum!=args.mixed_combat_start:
                curriculum_change=dict(before=prior_curriculum,after=args.mixed_combat_start,
                                       scope='Formal initialization only; no prefix PPO samples, rewards or activation coverage')
            prefix_operations=replay.get('curriculum_prefix_operations',0)
            policy.bind_parameters(parameters)
            episode_index, total_steps = replay['next_episode'], replay['total_steps']
            sampler = make_sampler(replay['sampler'])
            data_binding=replay.get('data_binding')
            if old_mode!=args.reward_mode:
                objective_change=dict(before=old_mode,after=args.reward_mode,scope='Training objective only; native rules/rewards and full checkpoint history retained')
        if args.reward_mode=='terminal_sequence':
            metadata.update(reward_mode=args.reward_mode,sequence_reward_sha256=file_hash(Path(__file__).with_name('sequence_training_reward.py')))
        learning_rate_override=override_learning_rate(optimizer,args.learning_rate)
        if args.dynamic_single_curriculum:
            from dynamic_single_curriculum import DynamicSingleCurriculum
            if any(len(c['task']['definition']['EnemyGroups'])!=1 for c in certificates):
                raise ValueError('Dynamic single-battle curriculum requires a single-battle base pool')
            if not isinstance(sampler,DynamicSingleCurriculum):
                sampler=DynamicSingleCurriculum(certificates,rules,output,sampler.state())
            metadata['dynamic_curriculum_sha256']=file_hash(Path(__file__).with_name('dynamic_single_curriculum.py'))
        metadata.update(training_step_cost=step_cost,collection_workers=args.workers)
        if args.workers>1:
            from parallel_rollouts import ParallelRollouts
            metadata['parallel_rollouts_sha256']=file_hash(Path(__file__).with_name('parallel_rollouts.py'))
            parallel=ParallelRollouts(rules,args.workers,random.bit_generator.state,worker_states)
        if world_consumer_scale>0:metadata.update(world_consumer_reward_sha256=file_hash(Path(__file__).with_name('world_consumer_potential_reward.py')),world_consumer_potential_scale=world_consumer_scale)
        if skill_targets:metadata.update(target_skill_sampler_sha256=file_hash(Path(__file__).with_name('targeted_skill_floor_sampler.py')),skill_floor_targets=skill_targets)
        if shared_planning_cost>0:metadata.update(shared_planning_reward_sha256=file_hash(Path(__file__).with_name('shared_planning_revisit_reward.py')),shared_planning_revisit_cost=shared_planning_cost)
        if skill_mass>0:metadata.update(skill_sampler_sha256=file_hash(Path(__file__).with_name('skill_floor_sampler.py')),skill_floor_mass=skill_mass)
        if floor_mass>0:
            metadata.update(encounter_sampler_sha256=file_hash(Path(__file__).with_name('encounter_floor_sampler.py')),
                            encounter_floor_mass=floor_mass)
        if progress_scale>0:
            metadata.update(progress_reward_sha256=file_hash(Path(__file__).with_name('progress_potential_reward.py')),
                            progress_potential_scale=progress_scale)
        if planning_cost>0:
            metadata.update(planning_reward_sha256=file_hash(Path(__file__).with_name('planning_revisit_reward.py')),
                            planning_revisit_cost=planning_cost)
        manifest = dict(metadata=metadata, device='CPU_NumPy', certified_tasks=len(certificates),
                        episodes_per_update=args.episodes_per_update, epochs=args.epochs,
                        optimizer_state_restored=bool(args.resume), memory_reset='only native episode boundary',
                        optimizer_settings=optimizer.settings(), learning_rate_override=learning_rate_override,
                        reward_mode=args.reward_mode,objective_change=objective_change,
                        mixed_combat_start=args.mixed_combat_start,curriculum_change=curriculum_change,
                        planning_revisit_cost=planning_cost,planning_objective_change=planning_change,
                        initialization='Odd native episode indices use one actual legal fight before actor initialization; all later boundaries carry memory',
                        success_scope=pool['goal'], legacy_seed_binding=any(c['task']['generator']=='legacy_seed' for c in certificates),
                        gaps=['independent_task_factors', 'impossibility_solver', 'full_original_coverage',
                              'unseen_combination_evaluation', 'Transformer_comparison', 'Unity_UI_acceptance'])
        if args.resume:
            manifest['resume_checkpoint'] = dict(path=str(Path(args.resume).resolve()), sha256=file_hash(args.resume),
                                                pool_expansion=replay.get('pool_expansion'))
        manifest.update(certified_tasks=len(pool['tasks']),unknown_training_tasks=len(pool.get('unknown_tasks',[])),
                        encounter_floor_mass=floor_mass,task_sampling_change=sampling_change,
                        progress_potential_scale=progress_scale,progress_objective_change=progress_change,
                        sampling='global_certified_unknown_mix_v1' if mixed else 'certified_priority')
        manifest.update(skill_floor_mass=skill_mass,skill_task_sampling_change=skill_sampling_change)
        manifest.update(shared_planning_revisit_cost=shared_planning_cost,shared_planning_objective_change=shared_planning_change)
        manifest.update(world_consumer_potential_scale=world_consumer_scale,world_consumer_objective_change=world_consumer_change)
        manifest.update(skill_floor_targets=skill_targets,skill_task_target_change=skill_target_change)
        manifest.update(collection_workers=args.workers,collection_change=collection_change,
                        training_step_cost=step_cost,step_objective_change=step_change,
                        environment_pids=[env.p.pid for env in parallel.environments] if parallel else [rules.p.pid])
        if args.dynamic_single_curriculum:
            manifest.update(sampling='dynamic_single_beta_frontier_v1',
                            legacy_skill_encounter_floors_active=False,
                            dynamic_first_admissions=128,dynamic_admissions_per_update=32)
        (output / 'manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
        evaluation_rows = None
        start_update=0;prior_seconds=0.
        if args.prior_run and (not args.fixed_task_pool or not args.evaluation_pool or args.baseline_cache):
            raise ValueError('Prior run requires fixed evaluation and cannot replace its baseline')
        if args.fixed_task_pool and not args.dynamic_single_curriculum:
            raise ValueError('Fixed difficulty curriculum requires dynamic sampler')
        if args.evaluation_pool:
            if not parallel or args.evaluation_every < 1:
                raise ValueError('Evaluation requires parallel environments and positive interval')
            from fixed_task_evaluation import validate_evaluation, evaluate
            evaluation_rows = validate_evaluation(args.evaluation_pool, certificates, rules)
            metadata.update(fixed_task_pool=args.fixed_task_pool,evaluation_pool_sha256=file_hash(args.evaluation_pool))
            manifest.update(fixed_task_pool=args.fixed_task_pool, evaluation_pool=args.evaluation_pool,
                evaluation_sha256=file_hash(args.evaluation_pool), evaluation_tasks=len(evaluation_rows),
                evaluation_every=args.evaluation_every)
            (output / 'manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
            if args.prior_run:
                prior=Path(args.prior_run)
                previous=json.loads((prior/'manifest.json').read_text())
                history=[json.loads(line) for line in (prior/'metrics.jsonl').read_text().splitlines()]
                last=history[-1]
                if (Path(args.resume).resolve()!=(prior/'checkpoint.npz').resolve()
                    or previous['metadata']!=metadata or last['episodes']!=episode_index
                    or last['total_steps']!=total_steps or last['optimizer_steps']!=optimizer.steps
                    or any(previous.get(k)!=manifest.get(k) for k in ('episodes_per_update','epochs','reward_mode','mixed_combat_start','training_step_cost','planning_revisit_cost','evaluation_every'))):
                    raise ValueError('Prior experiment/checkpoint/configuration mismatch')
                start_update=last['update'];prior_seconds=last['seconds']
                if not 0<start_update<args.updates:raise ValueError('Prior update is outside remaining experiment')
                for name in ('metrics.jsonl','evaluation_metrics.jsonl','train_eval.html'):
                    shutil.copyfile(prior/name,output/name)
                manifest['continuation']=dict(prior_run=str(prior),completed_updates=start_update,
                    checkpoint_sha256=file_hash(args.resume),partial_batch_used=False)
                (output/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
            else:
                evaluate(policy, parallel, evaluation_rows, output/'eval_0000', collect_episode,
                    (args.reward_mode,False,planning_cost,progress_scale,shared_planning_cost,world_consumer_scale,step_cost),
                    0, args.resume, cache_source=args.baseline_cache)
        start = time.perf_counter()-prior_seconds
        for update in range(start_update,args.updates):
            update_start = time.perf_counter()
            if args.workers > 1:
                # This immutable behavior checkpoint makes every collected
                # probability reproducible, including a failed partial batch.
                archive = output / 'behavior_checkpoints'
                archive.mkdir(exist_ok=True)
                behavior = archive / f'update_{update+1:04d}.npz'
                if behavior.exists():raise ValueError('Refusing to overwrite behavior checkpoint')
                parent = output/'checkpoint.npz' if (output/'checkpoint.npz').exists() else args.resume
                if parent:
                    shutil.copyfile(parent, behavior)
                else:
                    save_checkpoint(behavior, optimizer, random, metadata, dict(
                        next_episode=episode_index, total_steps=total_steps, sampler=sampler.state(),
                        at_native_episode_boundary=True, parallel_worker_rngs=parallel.state()))
                (archive/f'update_{update+1:04d}.json').write_text(json.dumps(dict(
                    first_episode=episode_index, batch_episodes=args.episodes_per_update,
                    checkpoint_sha256=file_hash(behavior))), encoding='utf-8')
            if args.dynamic_single_curriculum and not args.fixed_task_pool:
                sampler.expand(128 if sampler.updates==0 else 32)
            if args.native_world and world_source_hash()!=metadata['world_source_sha256']:raise ValueError('Original world source changed during training')
            if (data_signature()['sha256']!=metadata['rules_data_sha256'] or (metadata.get('native_world') and world_source_hash()!=metadata['world_source_sha256'])):
                raise ValueError('Runtime JSON data changed before rollout; do not continue this model')
            episodes = []
            jobs=None
            collected=None
            if parallel:
                jobs=[(episode_index+i,sampler.select(random)) for i in range(args.episodes_per_update)]
                options=lambda index:(args.reward_mode,args.mixed_combat_start and index%2==1,planning_cost,progress_scale,shared_planning_cost,world_consumer_scale,step_cost)
                collected=parallel.collect(policy,jobs,collect_episode,options,output)
            for _ in range(args.episodes_per_update):
                certificate = jobs[_][1] if jobs else sampler.select(random)
                try:
                    episode = collected[_] if collected is not None else collect_episode(rules, policy, random, certificate,args.reward_mode,
                                              args.mixed_combat_start and episode_index%2==1,planning_cost,progress_scale,shared_planning_cost,world_consumer_scale,step_cost)
                except Exception as error:
                    (output / 'failure.json').write_text(json.dumps(dict(episode=episode_index,
                        task_id=certificate['task_id'], seed=certificate['task']['seed'], error=str(error),
                        trace=getattr(error, 'trace', []), attempted_action=getattr(error, 'attempted_action', None))), encoding='utf-8')
                    raise
                episodes.append(episode)
                sampler.observe(episode, episode_index)
                prefix_operations+=len(episode['curriculum_prefix'])
                episode_path = output / f'episode_{episode_index}.json'
                if parallel:
                    os.link(output/'rollouts'/f'episode_{episode_index}.json', episode_path)
                else:
                    episode_path.write_text(json.dumps(
                        {key: episode[key] for key in ('task_id', 'seed', 'trace', 'curriculum_prefix', 'report','feasibility_status','worker_id') if key in episode}, ensure_ascii=False), encoding='utf-8')
                episode_index += 1
                total_steps += len(episode['trace'])
            collection_seconds = time.perf_counter() - update_start
            optimization_start = time.perf_counter()
            advantages = np.concatenate([e['raw_advantage'] for e in episodes])
            if (data_signature()['sha256']!=metadata['rules_data_sha256'] or (metadata.get('native_world') and world_source_hash()!=metadata['world_source_sha256'])):
                raise ValueError('Runtime JSON data changed during rollout; preserve episodes without updating')
            for e in episodes:
                e['advantage'] = (e['raw_advantage'] - advantages.mean()) / (advantages.std() + 1e-8)
            for epoch in range(args.epochs):
                loss, gradients = batch_gradients(policy, episodes)
                optimizer.update(gradients)
                kl = updated_kl(policy, episodes, with_values=True)
                if kl > .03:
                    break
            optimization_seconds = time.perf_counter() - optimization_start
            from value_diagnostics import measure
            value_targets=np.concatenate([e['returns'] for e in episodes])
            value_before=np.concatenate([e['rollout_values'] for e in episodes])
            value_after=np.concatenate([e['updated_values'] for e in episodes])
            critic_before=measure(value_before,value_targets)
            critic_after=measure(value_after,value_targets)
            save_start = time.perf_counter()
            np.savez_compressed(output/f'value_update_{update+1:04d}.npz', targets=value_targets,
                                before=value_before, after=value_after,
                                episode_lengths=np.asarray([len(e['trace']) for e in episodes]))
            replay_state=dict(next_episode=episode_index,total_steps=total_steps,sampler=sampler.state(),at_native_episode_boundary=True,
                              mixed_combat_start=args.mixed_combat_start,curriculum_prefix_operations=prefix_operations)
            if parallel:replay_state['parallel_worker_rngs']=parallel.state()
            if (data_signature()['sha256']!=metadata['rules_data_sha256'] or (metadata.get('native_world') and world_source_hash()!=metadata['world_source_sha256'])):
                raise ValueError('Runtime JSON data changed during update; do not save a mislabelled checkpoint')
            if data_binding is not None:
                replay_state['data_binding']=data_binding
            save_checkpoint(output / 'checkpoint.npz',optimizer,random,metadata,replay_state)
            if evaluation_rows is not None and ((update+1) % args.evaluation_every == 0 or update+1 == args.updates):
                frozen = output/f'eval_checkpoint_{update+1:04d}.npz'
                shutil.copyfile(output/'checkpoint.npz', frozen)
                evaluate(policy, parallel, evaluation_rows, output/f'eval_{update+1:04d}', collect_episode,
                    (args.reward_mode,False,planning_cost,progress_scale,shared_planning_cost,world_consumer_scale,step_cost),
                    update+1, frozen)
            (output / 'actual_training_events.json').write_text(json.dumps(sampler.state(), ensure_ascii=False, indent=2), encoding='utf-8')
            summary = dict(update=update + 1, episodes=episode_index, total_steps=total_steps,
                           value_exp_var_before=critic_before['explained_variance'],
                           value_exp_var_after=critic_after['explained_variance'],
                           value_target_variance=critic_after['target_variance'],
                           value_mse_before=critic_before['mse'], value_mse_after=critic_after['mse'],
                           value_bias_after=critic_after['bias'], value_samples=critic_after['samples'],
                           collection_workers=args.workers, target_updates=args.updates,
                           collection_seconds=collection_seconds, optimization_seconds=optimization_seconds,
                           checkpoint_seconds=time.perf_counter()-save_start,
                           seconds=time.perf_counter() - start, loss=loss, updated_full_sequence_kl=kl,
                           optimizer_steps=optimizer.steps,
                           curriculum_prefix_operations=prefix_operations,
                           unknown_episodes=sum(e['feasibility_status']=='legal_reset_unknown' for e in episodes),
                           completed_declared_tasks=sum(bool(e['trace'][-1]['after'].get('taskProgress', {}).get('sequenceCompleted')) for e in episodes),
                           formal_group_wins=sum(int(x['after']['phase'] == 'End' and x['after']['won']
                               and not x['before']['won']) for e in episodes for x in e['trace']))
            (output / 'progress.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
            if args.dynamic_single_curriculum:
                summary.update(dynamic_tasks=len(sampler.certificates),
                               dynamic_observed_tasks=len(sampler.results))
                (output / 'progress.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
            from training_curves import record
            record(output, summary, episodes)
            print(json.dumps(summary), flush=True)
    finally:
        if parallel:parallel.close()
        rules.close()


if __name__ == '__main__':
    main()
