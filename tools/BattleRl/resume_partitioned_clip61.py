"""Remote-only bounded on-policy continuation; only clipping partition changes."""
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
stage=Path('optimization_stage_20261014');parent=Path('remote_repaired_trainonly300');out=Path('remote_partitioned_clip61')
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
for p in psutil.process_iter(['pid','cmdline']):
 if p.pid==os.getpid():continue
 cmd=p.info['cmdline'] or []
 if cmd and 'python' in Path(cmd[0]).name:
  assert not any(Path(arg).name in ('deploy_repaired_trainonly300.py','resume_partitioned_clip61.py') for arg in cmd[1:]),('trainer alive',p.pid)
assert shutil.disk_usage(parent).free>10*1024**3
old=json.loads((parent/'manifest.json').read_text())
for n,s in old['repair_runtime_sha'].items():assert sha(stage/n)==s,n
for n,s in old['implementation_sha'].items():assert sha(Path('tools/BattleRl')/n)==s,n
preserved=json.loads((stage/'stop61_preservation.json').read_text())['source_sha256']
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
assert state['completed_updates']==61 and len(state['environments'])==32
assert all(s['closed_episode'] for s in state['environments']) and np.count_nonzero(state['last_obs']['context_id'])==0
assert all(torch.count_nonzero(t)==0 for pair in state['lstm'] for t in pair)
r=Rules()
try:history=catalog_argument_features(r.actions)
finally:r.close()
env=ThreadedRulesVecEnv([lambda i=i:BattleEnv(rows,out/'episodes',i,history,reward_config={'version':base.SingleBattleReward.version,'scale':.01}) for i in range(32)])
status=out/'status.json'
def write(**kw):status.write_text(json.dumps(dict(pid=os.getpid(),time=time.time(),**kw),indent=2))
try:
 write(state='restoring',completed=61)
 m=base.RecordedRecurrentPPO.load(parent/'checkpoint.zip',env=env,device='cuda',force_reset=False)
 assert m.completed_updates==61 and base.parameter_sha(m.policy)=='abe1cb46785fb40d31a29aac2ff19dfad4138e10d867d75c5f3d9499a92f2e60'
 assert len(m.policy.optimizer.state)==44
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
  rs=[r for r in rs if r['update']<=61] if name=='metrics.jsonl' else [r for r in rs if r['update']<61]
  (out/name).write_text(''.join(json.dumps(r)+'\n' for r in rs))
 binding=dict(parent=str(parent),completed=61,parent_sha={n:sha(parent/n) for n in ('checkpoint.zip','continuation.pt','manifest.json')},change='shared/actor/critic each L2 clip .5; all other PPO and input/reward settings preserved',source_sha={n:sha(stage/n) for n in ('resume_partitioned_clip61.py','partitioned_policy_clip.py')},evaluation_enabled=False,bounded_review_at=71,overall_goal=300)
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
  write(state='training',completed=self.completed_updates,review_at=71)
 base.RecordedRecurrentPPO.train=train
 write(state='sampling',completed=61,review_at=71)
 while m.completed_updates<71:
  assert shutil.disk_usage(out).free>3*1024**3,'Low disk at complete boundary'
  m.learn(total_timesteps=1,reset_num_timesteps=False,callback=base.EvidenceCallback())
 write(state='review_boundary',completed=m.completed_updates,overall_goal=300)
except Exception as exc:
 write(state='failed',error=repr(exc));raise
finally:env.close()
