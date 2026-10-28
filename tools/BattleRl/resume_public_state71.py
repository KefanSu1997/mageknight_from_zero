"""Remote-only bounded on-policy continuation; public state residual candidate after a closed review boundary."""
import os,sys,json,hashlib,shutil,time,gc
from pathlib import Path
os.environ.update(OPENBLAS_NUM_THREADS='1',BATTLE_RL_ENCODER_BATCH='4')
os.environ['PATH']='/root/autodl-tmp/dotnet:/root/autodl-tmp/mkvenv/bin:'+os.environ['PATH']
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
import torch,numpy as np,psutil
import train_sb3_atomic_entity_text as base
from hierarchical_option_policy import BattleEnv
from sb3_battle_vec import ThreadedRulesVecEnv
from atomic_transport_fast import install
import hierarchical_packed_cache as packed
from entropy_token_value_policy import bind_reference
from partitioned_policy_clip import installed
from train_ppo import Rules
from action_argument_features import catalog_argument_features
from concurrent.futures import ThreadPoolExecutor
stage=Path('optimization_stage_20261014');parent=Path('remote_partitioned_clip61');out=Path('remote_public_state71')
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
for p in psutil.process_iter(['pid','cmdline']):
 if p.pid==os.getpid():continue
 cmd=p.info['cmdline'] or []
 if cmd and 'python' in Path(cmd[0]).name:
  assert not any(Path(arg).name in ('deploy_repaired_trainonly300.py','resume_partitioned_clip61.py','resume_public_state71.py') for arg in cmd[1:]),('trainer alive',p.pid)
assert shutil.disk_usage(parent).free>10*1024**3
old=json.loads((parent/'manifest.json').read_text())
parent_status=json.loads((parent/'status.json').read_text())
assert parent_status['state']=='review_boundary' and parent_status['completed']==71
for n,s in old['continuation']['source_sha'].items():assert sha(stage/n)==s,n
for n,s in old['repair_runtime_sha'].items():assert sha(stage/n)==s,n
for n,s in old['implementation_sha'].items():assert sha(Path('tools/BattleRl')/n)==s,n
preserved=json.loads((parent/'latest_checkpoint_bundle.json').read_text())['source_sha']
for n in ('checkpoint.zip','continuation.pt','manifest.json'):assert sha(parent/n)==preserved[n],n
assert json.loads((stage/'partitioned_full_update.json').read_text())['results']['partitioned']['adam_finite']
out.mkdir(exist_ok=False);(out/'contexts').mkdir()
shutil.copyfile(parent/'atomic_supports.jsonl',out/'atomic_supports.jsonl')
os.environ.update(BATTLE_RL_CONTEXT_STORE=str(out/'contexts'),BATTLE_RL_ATOMIC_REGISTRY=str(out/'atomic_supports.jsonl'),BATTLE_RL_TEXT_ASSETS='H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets')
torch.set_num_threads(1);base.configure_rules('H:/MageKnight_RL_Runs/atomic_adapter_v2_frozen/BattleRl.dll');install();packed.install();base.memory_record=lambda *args:None
assert sha(os.environ['BATTLE_RL_DLL'])==old['rules_sha']
data=Path('H:/MageKnight_RL_Runs/atomic_dataset')
assert sha(data/'train.json')==old['train_sha'] and sha(data/'eval.json')==old['eval_sha']
src=json.loads((data/'train.json').read_text());rows=src['tasks']+src.get('unknown_tasks',[])
state=torch.load(parent/'continuation.pt',map_location='cpu',weights_only=False)
assert state['completed_updates']==71 and len(state['environments'])==32
assert all(s['closed_episode'] for s in state['environments']) and np.count_nonzero(state['last_obs']['context_id'])==0
assert all(torch.count_nonzero(t)==0 for pair in state['lstm'] for t in pair)
r=Rules()
try:history=catalog_argument_features(r.actions)
finally:r.close()
env=ThreadedRulesVecEnv([lambda i=i:BattleEnv(rows,out/'episodes',i,history,reward_config={'version':base.SingleBattleReward.version,'scale':.01}) for i in range(32)])
status=out/'status.json'
def write(**kw):status.write_text(json.dumps(dict(pid=os.getpid(),time=time.time(),**kw),indent=2))
try:
 write(state='restoring',completed=71)
 m=base.RecordedRecurrentPPO.load(parent/'checkpoint.zip',env=env,device='cuda',force_reset=False)
 assert m.completed_updates==71
 assert len(m.policy.optimizer.state)==44
 from public_state_residual_candidate import PublicStateResidualPolicy
 from rollout_archive import load as load_rollout
 assert json.loads((stage/'public_state_residual_gate.json').read_text())['passed']
 assert json.loads((stage/'public_state_full_update.json').read_text())['results']['public_state']['adam_finite']
 archived=load_rollout(next(parent.glob('rollout_0070_*.archive')));archived.device=torch.device('cuda')
 batch=next(archived.get(128));m.policy.set_training_mode(False)
 def probe():return m.policy.evaluate_actions(batch.observations,batch.actions.long().flatten(),batch.lstm_states,batch.episode_starts)
 with torch.no_grad():before=probe()
 old_values={n:v.clone() for n,v in m.policy.state_dict().items()};old_adam=dict(m.policy.optimizer.state)
 torch.manual_seed(7112026)
 m.policy.__class__=PublicStateResidualPolicy;m.policy_class=PublicStateResidualPolicy;m.policy.install_public_state_residual()
 assert all(torch.equal(v,m.policy.state_dict()[n]) for n,v in old_values.items())
 assert all(m.policy.optimizer.state[p] is v for p,v in old_adam.items())
 with torch.no_grad():after=probe()
 value_error=float((before[0]-after[0]).abs().max());logp_error=float((before[1]-after[1]).abs().max())
 assert value_error==0 and logp_error==0
 m.save(out/'migration_gate_checkpoint')
 reloaded=base.RecordedRecurrentPPO.load(out/'migration_gate_checkpoint.zip',device='cuda')
 reloaded.policy.set_training_mode(False)
 assert base.parameter_sha(reloaded.policy)==base.parameter_sha(m.policy)
 assert len(reloaded.policy.optimizer.state)==len(old_adam)
 with torch.no_grad():check=reloaded.policy.evaluate_actions(batch.observations,batch.actions.long().flatten(),batch.lstm_states,batch.episode_starts)
 assert torch.equal(check[0],after[0]) and torch.equal(check[1],after[1])
 migration_gate=dict(value_error=value_error,logp_error=logp_error,old_adam_preserved=len(old_adam),real_samples=int((batch.mask>0).sum()),device='cuda',public_state_width=int(batch.observations['state'].shape[-1]))
 migration_gate.update(reload_parameters_buffers_equal=True,reload_outputs_equal=True)
 (out/'migration_gate.json').write_text(json.dumps(migration_gate,indent=2))
 del archived,batch,before,after,old_values,old_adam,reloaded,check
 m.policy.context_path=str(out/'contexts');m.policy.registry_file=str(out/'atomic_supports.jsonl')
 for f in (m.policy.features_extractor,m.policy.pi_features_extractor,m.policy.vf_features_extractor):f.context_path=str(out/'contexts')
 m.policy_kwargs.update(context_path=str(out/'contexts'),registry_file=str(out/'atomic_supports.jsonl'))
 def restore(i):
  e=env.envs[i];s=state['environments'][i];e.restore(s)
  assert e.current==s['current'] and e.results==s['results'] and e.rng.bit_generator.state==s['rng']
  for k,v in e._observation().items():np.testing.assert_array_equal(v,state['last_obs'][k][i])
 with ThreadPoolExecutor(max_workers=4) as pool:list(pool.map(restore,range(32)))
 m._last_lstm_states=type(state['lstm'])(*(tuple(t.to('cuda') for t in pair) for pair in state['lstm']))
 m._last_obs=state['last_obs'];m._last_episode_starts=state['episode_starts']
 m.output=out;m.goal_updates=300;m.history_features=history;m.evaluation_every=0;m.evaluation_rows=[];m.episodes_per_update=128
 m.reward_config={'version':base.SingleBattleReward.version,'scale':.01}
 for name in ('metrics.jsonl','performance.jsonl'):
  rs=[json.loads(s) for s in (parent/name).read_text().splitlines()]
  rs=[r for r in rs if r['update']<=71] if name=='metrics.jsonl' else [r for r in rs if r['update']<71]
  (out/name).write_text(''.join(json.dumps(r)+'\n' for r in rs))
 binding=dict(parent=str(parent),completed=71,parent_sha={n:sha(parent/n) for n in ('checkpoint.zip','continuation.pt','manifest.json')},change='Add zero-initialized public state/history residual to shared actor/critic features; retain hierarchical token options and partitioned clip',migration_gate=migration_gate,source_sha={n:sha(stage/n) for n in ('resume_public_state71.py','partitioned_policy_clip.py','public_state_residual_candidate.py')},evaluation_enabled=False,bounded_review_at=91,overall_goal=300)
 manifest=dict(old);manifest['continuation']=binding;manifest['fresh']=False
 (out/'manifest.json').write_text(json.dumps(manifest,indent=2))
 (out/'continuation_loaded.json').write_text(json.dumps(dict(**binding,parameter_sha=base.parameter_sha(m.policy),adam_states=44,native_workers_restored=32),indent=2))
 torch.cuda.synchronize();base.restore_rng(state['rng']);del state;gc.collect()
 m.save(out/'migration_initial')
 original=base.RecordedRecurrentPPO.train
 def train(self):
  records=[];bind_reference(self)
  try:
   with installed(self.policy,records):original(self)
  finally:self.policy._entropy_reference=None
  with (out/'gradient_groups.jsonl').open('a') as f:f.write(json.dumps(dict(update=self.completed_updates,minibatches=records))+'\n')
  assert all(torch.isfinite(p).all() for p in self.policy.state_dict().values())
  write(state='training',completed=self.completed_updates,review_at=91)
 base.RecordedRecurrentPPO.train=train
 write(state='sampling',completed=71,review_at=91)
 while m.completed_updates<91:
  assert shutil.disk_usage(out).free>3*1024**3,'Low disk at complete boundary'
  m.learn(total_timesteps=1,reset_num_timesteps=False,callback=base.EvidenceCallback())
 write(state='review_boundary',completed=m.completed_updates,overall_goal=300)
except Exception as exc:
 write(state='failed',error=repr(exc));raise
finally:env.close()

