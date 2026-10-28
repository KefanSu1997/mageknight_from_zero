"""Pinned SB3-Contrib RecurrentPPO runner. No locally implemented PPO optimizer."""
import os
os.environ.setdefault('OMP_NUM_THREADS', '1')
import argparse
import copy
import gc
import hashlib
import gzip
import json
import random
import time
from pathlib import Path
import numpy as np
import torch
from sb3_contrib import RecurrentPPO
from stable_baselines3.common.callbacks import BaseCallback
from complete_episode_env import BattleEnv
from complete_episode_collector import CompleteEpisodeCollector
from sparse_battle_policy import SparseBattleRecurrentPolicy as BattleRecurrentPolicy
from sb3_battle_vec import ThreadedRulesVecEnv
from task_feasibility import digest, file_hash
from coverage_tasks import combination
from original_data_signature import data_signature
from action_argument_features import catalog_argument_features
from printed_argument_features import extend_arguments
from train_ppo import Rules
from single_battle_reward import SingleBattleReward
from sb3_tracking_page import render
from parallel_episode_eval import collect as collect_fixed
from rollout_archive import save as save_rollout
from compact_recurrent_buffer import CompactRecurrentDictRolloutBuffer
from sb3_battle_env import SharedCatalogRules


def configure_rules(dll):
    os.environ.update(BATTLE_RL_DLL=str(Path(dll).resolve()), BATTLE_RL_SHARED_SKILLS='1',
        BATTLE_RL_SHAPESHIFT='1', BATTLE_RL_ORIGINAL_WORLD='1', BATTLE_RL_PROTOCOL='--serve-journey-artifacts',
        BATTLE_RL_NATIVE_PAYMENTS='1', BATTLE_RL_NATIVE_CONVERSIONS='1',
        BATTLE_RL_NATIVE_ENERGY_TARGETS='1', BATTLE_RL_ORIGINAL_OPENINGS='1')


def rng_state():
    return dict(python=random.getstate(), numpy=np.random.get_state(), torch=torch.get_rng_state(),
                cuda=torch.cuda.get_rng_state_all() if torch.cuda.is_available() else [])


def restore_rng(state):
    random.setstate(state['python']);np.random.set_state(state['numpy']);torch.set_rng_state(state['torch'])
    if state['cuda']:torch.cuda.set_rng_state_all(state['cuda'])


def parameter_sha(policy):
    h=hashlib.sha256()
    for name, value in policy.state_dict().items():
        h.update(name.encode());h.update(value.detach().cpu().numpy().tobytes())
    return h.hexdigest()


def value_metrics(values, returns):
    v=np.asarray(values).ravel();t=np.asarray(returns).ravel();var=float(np.var(t))
    return dict(ev=None if var<1e-12 else float(1-np.var(t-v)/var),
                mse=float(np.mean((t-v)**2)),bias=float(np.mean(v-t)),samples=len(t))


def memory_record(output,stage):
    import ctypes
    class Memory(ctypes.Structure):
        _fields_=[('length',ctypes.c_ulong),('load',ctypes.c_ulong)]+[(n,ctypes.c_ulonglong) for n in
            ('physical_total','physical_free','commit_limit','commit_free','virtual_total','virtual_free','extended')]
    m=Memory();m.length=ctypes.sizeof(m)
    ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(m))
    with (output/'memory.jsonl').open('a') as f:
        f.write(json.dumps(dict(time=time.time(),stage=stage,physical_free=m.physical_free,
                               commit_free=m.commit_free,commit_limit=m.commit_limit))+'\n')


def performance_metrics(records):
    if not records:return dict(episodes=0)
    winners=[r for r in records if r['won']]
    return dict(episodes=len(records),wins=len(winners),win_rate=len(winners)/len(records),
        mean_kills=float(np.mean([r['kills'] for r in records])),
        mean_new_wound_cards=float(np.mean([r['new_wound_cards'] for r in records])),
        mean_new_unit_wounds=float(np.mean([r['new_unit_wounds'] for r in records])),
        winning_mean_new_wound_cards=float(np.mean([r['new_wound_cards'] for r in winners])) if winners else None,
        mean_steps=float(np.mean([r['steps'] for r in records])))


class EvidenceCallback(BaseCallback):
    def _on_rollout_start(self):
        self.model.rollout_started=time.perf_counter()
        self.model.rollout_audit_failures=[]
        self.model.rollout_tag=f'{self.model.completed_updates:04d}_step{self.model.num_timesteps}'
        self.model.save(self.model.output/f'behavior_{self.model.rollout_tag}')
        torch.save(dict(rng=rng_state(), lstm=self.model._last_lstm_states,
                        episode_starts=self.model._last_episode_starts),
                   self.model.output/f'behavior_{self.model.rollout_tag}_rng.pt')

    def _on_step(self):
        values=self.locals['values'].detach().cpu().numpy().flatten()
        logs=self.locals['log_probs'].detach().cpu().numpy().flatten()
        with (self.model.output/'sampled_actions.jsonl').open('a') as stream:
            for i,info in enumerate(self.locals['infos']):
                if 'audit_failure' in info:
                    self.model.rollout_audit_failures.append(dict(worker=i,task_id=info['task_id'],
                        worker_step=info['worker_step'],errors=info['audit_failure']))
                    with (self.model.output/'audit_quarantine.jsonl').open('a') as qf:
                        qf.write(json.dumps(dict(worker=i,behavior_update=self.model.completed_updates,
                                                task_id=info['task_id'],errors=info['audit_failure']))+'\n')
                if 'performance' in info:
                    with (self.model.output/'performance.jsonl').open('a') as pf:
                        pf.write(json.dumps(dict(worker=i,update=self.model.completed_updates,task_id=info['task_id'],**info['performance']))+'\n')
                stream.write(json.dumps(dict(worker=i,worker_step=info['worker_step'],
                    task_id=info['task_id'],behavior_update=self.model.completed_updates,
                    action=int(self.locals['actions'][i]),log_probability=float(logs[i]),
                    value=float(values[i])))+'\n')
        return True

    def _on_rollout_end(self):
        self.model.collection_seconds=time.perf_counter()-self.model.rollout_started
        started=time.perf_counter()
        save_rollout(self.model.rollout_buffer,self.model.output/f'rollout_{self.model.rollout_tag}.archive')
        self.model.archive_seconds=time.perf_counter()-started


class RecordedRecurrentPPO(CompleteEpisodeCollector, RecurrentPPO):
    """Only diagnostics/checkpoint hooks; all PPO mathematics remain upstream."""
    def _setup_model(self):
        super()._setup_model()
        self.rollout_buffer.__class__=CompactRecurrentDictRolloutBuffer
        self.rollout_buffer.reset()
    def _excluded_save_params(self):
        return super()._excluded_save_params()+['history_features','evaluation_rows']

    def train(self):
        if self.rollout_audit_failures:
            # Keep upstream recurrent PPO untouched. Conservative rejection includes
            # every prefix in this buffer, including the failed worker's full sequence.
            record=dict(rollout=self.rollout_tag,timesteps=self.num_timesteps,
                        completed_updates=self.completed_updates,optimizer_updated=False,
                        rejected_transitions=self.n_steps*self.n_envs,
                        failures=self.rollout_audit_failures)
            with (self.output/'rejected_rollouts.jsonl').open('a') as f:f.write(json.dumps(record)+'\n')
            render(self.output,f'已隔离异常采样批，继续采样；有效更新 {self.completed_updates}/100')
            return
        before=value_metrics(self.rollout_buffer.values,self.rollout_buffer.returns)
        # No native stepping occurs during PPO. Release the 32 rule processes,
        # retaining each actual trajectory for exact replay after optimization.
        gc.collect()
        memory_record(self.output,'resident_rules_before_optimization')
        started=time.perf_counter()
        super().train()
        if self.device.type=='cuda':torch.cuda.synchronize()
        elapsed=time.perf_counter()-started
        memory_record(self.output,'optimization_completed')
        predicted=[];targets=[]
        # Upstream recurrent sampler preserves sequences and marks padding.
        # Restore RNG so diagnostics do not alter future training minibatches.
        rng=rng_state()
        with torch.no_grad():
            for batch in self.rollout_buffer.get(self.batch_size):
                values,_,_=self.policy.evaluate_actions(batch.observations,batch.actions.flatten().long(),
                                                       batch.lstm_states,batch.episode_starts)
                valid=batch.mask>0
                predicted.extend(values.flatten()[valid].cpu().numpy())
                targets.extend(batch.returns[valid].cpu().numpy())
        restore_rng(rng)
        gc.collect()
        memory_record(self.output,'resident_rules_after_optimization')
        self.completed_updates+=1
        results=[r for env in self.env.envs for r in env.results.values()]
        row=dict(update=self.completed_updates, timesteps=self.num_timesteps, device=str(self.device),
                 collection_seconds=self.collection_seconds, optimization_seconds=elapsed,
                 archive_seconds=self.archive_seconds,
                 value_before=before,value_after=value_metrics(predicted,targets),
                 episodes=sum(r[0] for r in results),wins=sum(r[1] for r in results),
                 recent_mean_reward=float(np.mean([r['r'] for r in self.ep_info_buffer])) if self.ep_info_buffer else None,
                 recent_reward_episodes=len(self.ep_info_buffer),
                 distinct_tasks=len(set(k for env in self.env.envs for k in env.results)),
                 logger={k:float(v) for k,v in self.logger.name_to_value.items() if isinstance(v,(float,int,np.number))})
        performance_path=self.output/'performance.jsonl'
        if performance_path.exists():
            records=[json.loads(line) for line in performance_path.read_text().splitlines()]
            row['audit_failed_episodes']=sum(not r.get('audit_valid',True) for r in records)
            valid_records=[r for r in records if r.get('audit_valid',True)]
            row['performance_all']=performance_metrics(valid_records)
            row['performance_recent100']=performance_metrics(valid_records[-100:])
        with (self.output/'metrics.jsonl').open('a') as f:f.write(json.dumps(row)+'\n')
        render(self.output,f'已完成 {self.completed_updates}/100 更新')
        self.save(self.output/'checkpoint')
        torch.save(dict(rng=rng_state(), environments=[env.snapshot() for env in self.env.envs],
                        lstm=self._last_lstm_states, last_obs=self._last_obs,
                        episode_starts=self._last_episode_starts, completed_updates=self.completed_updates),
                   self.output/'continuation.pt')
        print(json.dumps(row),flush=True)
        if self.evaluation_every and self.completed_updates % self.evaluation_every == 0:
            frozen_evaluation(self, self.evaluation_rows, self.history_features)


def frozen_evaluation(model, rows, history):
    """All fixed tasks, separate native process/RNG; never trains on validation."""
    output=model.output/f'eval_{model.completed_updates:04d}'
    output.mkdir(exist_ok=False)
    rng=rng_state();before=parameter_sha(model.policy);was_training=model.policy.training
    training_envs=getattr(getattr(model,'env',None),'envs',[])
    snapshots=[env.snapshot() for env in training_envs]
    for env in training_envs:env.rules.close()
    try:
        model.policy.set_training_mode(False)
        records=collect_fixed(model,rows,history,output,workers=32)
    finally:
        for env,snapshot in zip(training_envs,snapshots):
            env.rules=SharedCatalogRules()
            env.restore(snapshot)
        restore_rng(rng);model.policy.set_training_mode(was_training)
    if parameter_sha(model.policy)!=before:raise AssertionError('Evaluation changed parameters')
    result=dict(update=model.completed_updates,episodes=len(records),wins=sum(r['won'] for r in records),
                performance=performance_metrics([r['performance'] for r in records]),
                mean_reward=float(np.mean([r['reward'] for r in records])),frozen_parameters=True,
                training_rng_restored=True,per_task_actor_seed=True,records=records)
    (output/'summary.json').write_text(json.dumps(result,indent=2))
    with (model.output/'evaluation_metrics.jsonl').open('a') as f:
        f.write(json.dumps({k:v for k,v in result.items() if k!='records'})+'\n')
    render(model.output,f'固定评估 update {model.completed_updates} 完成')


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--dll',default='tmp/battle_rl_original_world_bound_consumers_v2/BattleRl.dll')
    parser.add_argument('--train-pool',default='AutomationOutputs/BattleRl/20261011_fixed5000_dataset/train.json')
    parser.add_argument('--eval-pool',default='AutomationOutputs/BattleRl/20261011_fixed5000_dataset/eval.json')
    parser.add_argument('--out',required=True)
    parser.add_argument('--workers',type=int,default=32)
    parser.add_argument('--n-steps',type=int,default=128)
    parser.add_argument('--batch-size',type=int,default=128)
    parser.add_argument('--updates',type=int,default=100)
    parser.add_argument('--device',default='cuda')
    parser.add_argument('--reward-scale',type=float,default=1.)
    parser.add_argument('--resume',help='Prior SB3 run, never a NumPy checkpoint')
    parser.add_argument('--archive-only-migration',action='store_true')
    parser.add_argument('--audit-quarantine-migration',action='store_true')
    parser.add_argument('--quarantine-log',help='Previously saved audit failures to retain as excluded training tasks')
    parser.add_argument('--evaluation-every',type=int,default=20)
    parser.add_argument('--sparse-migration',action='store_true')
    parser.add_argument('--baseline',action='store_true',help='Full frozen validation before any optimizer update')
    args=parser.parse_args()
    if args.reward_scale<=0 or not np.isfinite(args.reward_scale):raise ValueError('Positive finite reward scale required')
    if args.device=='cuda' and not torch.cuda.is_available():raise RuntimeError('CUDA required; refusing silent CPU fallback')
    torch.set_num_threads(1)
    configure_rules(args.dll)
    output=Path(args.out);output.mkdir(parents=True,exist_ok=False)
    train=json.loads(Path(args.train_pool).read_text()); rows=train['tasks']+train.get('unknown_tasks',[])
    evaluation=json.loads(Path(args.eval_pool).read_text())['tasks']
    train_keys=[digest(combination(r['task']['definition'])) for r in rows]
    eval_keys=[digest(combination(r['task']['definition'])) for r in evaluation]
    if len(set(train_keys))!=len(rows) or len(set(eval_keys))!=len(evaluation) or set(train_keys)&set(eval_keys):
        raise ValueError('Composition duplicate/leakage')
    rules=Rules()
    try:
        catalog=digest(rules.actions);data=data_signature();dll_sha=file_hash(args.dll)
        for row in rows+evaluation:
            task=row['task']
            if (len(task['definition']['EnemyGroups'])!=1 or task['rules_sha256']!=dll_sha
                or task['catalog_sha256']!=catalog or task['feature_schema']!=rules.info['featureSchema']
                or row['task_id']!=digest(task) or row['data_signature']!=data):
                raise ValueError('Task/source binding mismatch')
        history=catalog_argument_features(rules.actions)
        descriptors,printed_signature=extend_arguments(rules.actions,history)
    finally:rules.close()
    descriptor_path=(output/'action_descriptors.npy').resolve()
    np.save(descriptor_path,descriptors,allow_pickle=False)
    manifest=dict(framework='sb3-contrib==2.7.0 RecurrentPPO',torch=torch.__version__,device=args.device,
                  episode_entry=BattleEnv.entry_version,reward_version=SingleBattleReward.version,reward_scale=args.reward_scale,gamma=1.,
                  implementation_sha={name:file_hash(Path(__file__).parent/name) for name in
                      ('train_sb3_recurrent.py','train_sb3_sparse.py','train_sb3_combat.py','train_sb3_combat_fast.py','train_sb3_episodes.py','complete_episode_env.py','complete_episode_collector.py','parallel_episode_eval.py','parallel_combat_eval_fast.py','combat_start_env.py','parallel_combat_eval.py','sparse_battle_policy.py','parallel_sparse_eval.py','sb3_battle_env.py','sb3_battle_policy.py','single_battle_reward.py','parallel_fixed_eval.py','rollout_archive.py','compact_recurrent_buffer.py')},
                  gpu=torch.cuda.get_device_name(0) if args.device=='cuda' else None,
                  collection_contract=dict(episodes_per_update=128,episode_horizon=128,gae_lambda=1.,timeout='task_failure_no_bootstrap'),
                  config=vars(args),training_tasks=len(rows),validation_tasks=len(evaluation),
                  train_sha=file_hash(args.train_pool),eval_sha=file_hash(args.eval_pool),catalog_sha=catalog,
                  rules_sha=dll_sha,data_sha=data['sha256'],printed_signature=printed_signature,
                  descriptor_sha=file_hash(descriptor_path),
                  architecture='MLP256-256-128 + separate actor/critic LSTM256 + printed action descriptor32',
                  initialization=('SB3 continuation from '+str(args.resume)) if args.resume else 'fresh PyTorch parameters; not NumPy continuation',
                  scope='single-battle framework experiment; evaluation reported separately; no Unity acceptance')
    (output/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    if args.quarantine_log:
        exclusions=[json.loads(line) for line in Path(args.quarantine_log).read_text().splitlines()]
        BattleEnv.quarantined_tasks.update(r['task_id'] for r in exclusions)
        (output/'inherited_quarantine.json').write_text(json.dumps(exclusions,indent=2))
    reward_config=dict(version=SingleBattleReward.version,scale=args.reward_scale)
    env=ThreadedRulesVecEnv([lambda i=i:BattleEnv(rows,output/'episodes',i,history,reward_config=reward_config) for i in range(args.workers)])
    try:
        if args.resume:
            parent=Path(args.resume);old=json.loads((parent/'manifest.json').read_text())
            if old.get('reward_scale',1.)!=args.reward_scale:raise ValueError('Reward scale change requires fresh initialization')
            if args.audit_quarantine_migration:
                assert file_hash(parent/'manifest.json')=='b9c64457d804a09375dbe9391b9b3c7426a88829b2432cc0616470e8b4b511a0'
                for name,sha in old['implementation_sha'].items():
                    if name not in ('train_sb3_recurrent.py','sb3_battle_env.py'):
                        assert manifest['implementation_sha'][name]==sha,name
                (output/'audit_migration.json').write_text(json.dumps(dict(parent=str(parent),
                    old=old['implementation_sha'],new=manifest['implementation_sha'],
                    scope='Audit failures durable, task quarantine and reject containing rollout; unchanged rules/reward/upstream PPO'),indent=2))
                old['implementation_sha']=manifest['implementation_sha']
            if args.archive_only_migration:
                expected='8dfbb8541150d656d36285624808366d8102272460a9f9001302977f3d087f1d'
                assert old['implementation_sha']['train_sb3_recurrent.py']==expected
                for name,sha in old['implementation_sha'].items():
                    if name=='sb3_battle_env.py':assert sha=='83c1893996710a7f44e64d411b1ca9186db373a13e85e6677001b42f071bcb6c'
                    elif name!='train_sb3_recurrent.py':assert manifest['implementation_sha'][name]==sha,name
                (output/'archive_migration.json').write_text(json.dumps(dict(parent=str(parent),old=old['implementation_sha'],new=manifest['implementation_sha'],scope='streaming archive, lossless uint8 mask, equal shared catalog, nonoverlapping evaluation; no reward or PPO change'),indent=2))
                old['implementation_sha']=manifest['implementation_sha']
            if args.sparse_migration:
                assert old['implementation_sha']['train_sb3_recurrent.py']=='aa97f1cad787a790ea2889e39e0ecbaedd30c72770c9c5704eace5bedf0b154e'
                for name,sha in old['implementation_sha'].items():
                    assert manifest['implementation_sha'][name]==sha,name
                (output/'sparse_migration.json').write_text(json.dumps(dict(parent=str(parent),
                    old=old['implementation_sha'],new=manifest['implementation_sha'],
                    scope='Exact legal support distribution; all parameters/Adam preserved; global IDs unchanged. Sampling RNG mapping changes, not bitwise trajectory equivalence. Native environments remain resident during optimization.'),indent=2))
                old['implementation_sha']=manifest['implementation_sha']
            if file_hash(parent/'manifest.json')!='cbed8bc9eb5b4aa7758ea2c5e1d7cf7b07cc60dc08f645e18c62b02b63761602':raise ValueError('Episode migration parent changed')
            for name,sha in old['implementation_sha'].items():
                if manifest['implementation_sha'][name]!=sha:raise ValueError('Source changed: '+name)
            (output/'episode_migration.json').write_text(json.dumps(dict(parent=str(parent),scope='128 complete valid episodes, 32 workers, horizon128 failure, gamma1 lambda1; prior partial episodes archived and not used; optimizer and weights retained'),indent=2))
            old['implementation_sha']=manifest['implementation_sha']
            for key in ('episode_entry','train_sha','eval_sha','catalog_sha','rules_sha','data_sha','architecture','reward_version','gamma','implementation_sha'):
                if old[key]!=manifest[key]:raise ValueError('Resume binding changed: '+key)
            for key in ('workers','n_steps','batch_size'):
                if old['config'][key]!=vars(args)[key]:raise ValueError('Resume collection configuration changed')
            model=RecordedRecurrentPPO.load(parent/'checkpoint.zip',env=env,device=args.device,force_reset=False)
            if args.sparse_migration:
                model.policy.__class__=BattleRecurrentPolicy
                model.policy_class=BattleRecurrentPolicy
            state=torch.load(parent/'continuation.pt',weights_only=False,map_location='cpu')
            for target,snapshot in zip(env.envs,state['environments']):
                target.rng.bit_generator.state=snapshot['rng'];target.results=snapshot['results']
                target.episode=snapshot['episode'];target.total_steps=snapshot['total_steps']
                target.quarantined_tasks.update(snapshot.get('quarantined_tasks',[]))
            model._last_lstm_states=type(model._last_lstm_states)(*(tuple(t.to(model.device) for t in pair) for pair in state['lstm']))
            model._last_obs=state['last_obs'];model._last_episode_starts=state['episode_starts']
            model.completed_updates=state['completed_updates'];restore_rng(state['rng'])
            # The live environments now own restored state. Do not retain the
            # entire deserialized checkpoint and its duplicate trajectory graph.
            del state,snapshot
            gc.collect()
            for filename,field in (('metrics.jsonl','update'),('performance.jsonl','update')):
                source=parent/filename
                if source.exists():
                    records=[json.loads(line) for line in source.read_text().splitlines()]
                    records=[r for r in records if r[field] <= model.completed_updates] if filename=='metrics.jsonl' else [r for r in records if r[field] < model.completed_updates]
                    (output/filename).write_text(''.join(json.dumps(r)+'\n' for r in records))
        else:
            model=RecordedRecurrentPPO(BattleRecurrentPolicy,env,device=args.device,seed=7112026,
                n_steps=args.n_steps,batch_size=args.batch_size,n_epochs=4,learning_rate=1e-4,
                gamma=1.,gae_lambda=.99,clip_range=.2,ent_coef=.01,vf_coef=.5,max_grad_norm=.5,
                target_kl=.03,policy_kwargs=dict(descriptor_file=str(descriptor_path),
                    descriptor_sha256=manifest['descriptor_sha']),verbose=1)
            model.completed_updates=0
        model.gae_lambda=1.0
        (output/'continuation_loaded.json').write_text(json.dumps(dict(completed_updates=model.completed_updates,
            optimizer_updates=model._n_updates,parameter_sha=parameter_sha(model.policy),
            optimizer_state_entries=len(model.policy.optimizer.state),episodes_per_update=128,episode_horizon=128,gae_lambda=1.)))
        model.output=output
        model.reward_config=reward_config
        render(output,'初始化完成，准备冻结基线评估' if args.baseline else '初始化完成，准备采样')
        model.evaluation_every=args.evaluation_every;model.evaluation_rows=evaluation;model.history_features=history
        if not args.resume:
            model.save(output/'initial_checkpoint')
            torch.save(rng_state(),output/'initial_rng.pt')
        if args.baseline:frozen_evaluation(model,evaluation,history)
        goal_updates=model.completed_updates+args.updates
        first=True
        while model.completed_updates<goal_updates:
            model.learn(total_timesteps=1,
                        reset_num_timesteps=first and not bool(args.resume),callback=EvidenceCallback())
            first=False
        (output/'complete.json').write_text(json.dumps(dict(updates=model.completed_updates,
            timesteps=model.num_timesteps,parameter_sha=parameter_sha(model.policy),device=str(model.device))))
    finally:env.close()


if __name__=='__main__':main()
