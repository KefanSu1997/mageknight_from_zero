import os,sys,json,hashlib,shutil,gc,argparse,time
from pathlib import Path
sys.path.insert(0,str(Path('tools/BattleRl').resolve()));sys.path.insert(0,str(Path('optimization_stage_20261013').resolve()))
import torch,numpy as np
import train_sb3_atomic_entity_text as base
from token_value_policy import TokenValuePolicy
import hierarchical_packed_cache as packed
from hierarchical_option_policy import BattleEnv
from sb3_battle_vec import ThreadedRulesVecEnv
from atomic_transport_fast import install
from action_argument_features import catalog_argument_features
from train_ppo import Rules
import hierarchical_evaluation as evaluation
import parallel_hierarchical_eval as pe
from stable_baselines3.common.logger import configure
p=argparse.ArgumentParser();p.add_argument('--parent',required=True);p.add_argument('--out',required=True);p.add_argument('--gate',action='store_true');a=p.parse_args()
parent=Path(a.parent);out=Path(a.out);out.mkdir(exist_ok=False)
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
old=json.loads((parent/'manifest.json').read_text());assert old['reward_scale']==.01
for name,v in old['implementation_sha'].items():assert sha(Path('tools/BattleRl')/name)==v,name
assert sha('H:/MageKnight_RL_Runs/atomic_adapter_v2_frozen/BattleRl.dll')==old['rules_sha']
for name,key in [('train.json','train_sha'),('eval.json','eval_sha')]:assert sha(Path('H:/MageKnight_RL_Runs/atomic_dataset')/name)==old[key]
os.environ.update(BATTLE_RL_CONTEXT_STORE=str(out/'contexts'),BATTLE_RL_ATOMIC_REGISTRY=str(out/'atomic_supports.jsonl'),BATTLE_RL_TEXT_ASSETS='H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets',BATTLE_RL_ENCODER_BATCH='4')
(out/'contexts').mkdir();shutil.copyfile(parent/'contexts/contexts.jsonl',out/'contexts/contexts.jsonl');shutil.copyfile(parent/'atomic_supports.jsonl',out/'atomic_supports.jsonl')
torch.set_num_threads(1);base.configure_rules('H:/MageKnight_RL_Runs/atomic_adapter_v2_frozen/BattleRl.dll');install();packed.install()
base.memory_record=lambda output,stage:None
r=Rules();history=catalog_argument_features(r.actions);r.close()
src=json.loads(Path('H:/MageKnight_RL_Runs/atomic_dataset/train.json').read_text());rows=src['tasks']+src.get('unknown_tasks',[]);ev=json.loads(Path('H:/MageKnight_RL_Runs/atomic_dataset/eval.json').read_text())['tasks']
state=torch.load(parent/'continuation.pt',map_location='cpu',weights_only=False)
assert all(s['closed_episode'] for s in state['environments'])
assert all(torch.count_nonzero(t)==0 for pair in state['lstm'] for t in pair)
env=ThreadedRulesVecEnv([lambda i=i:BattleEnv(rows,out/'episodes',i,history,reward_config={'version':base.SingleBattleReward.version,'scale':.01}) for i in range(32)])
try:
 m=base.RecordedRecurrentPPO.load(parent/'checkpoint.zip',env=env,device='cuda',force_reset=False)
 assert m.completed_updates==state['completed_updates']
 previous={n:p.detach().clone() for n,p in m.policy.named_parameters()};oldadam=len(m.policy.optimizer.state)
 m.policy.__class__=TokenValuePolicy;m.policy.install_token_value();m.policy_class=TokenValuePolicy
 assert all(torch.equal(previous[n],p) for n,p in m.policy.named_parameters() if n in previous)
 assert len(m.policy.optimizer.state)==oldadam
 del previous
 m.policy.context_path=str(out/'contexts');m.policy.registry_file=str(out/'atomic_supports.jsonl')
 for f in (m.policy.features_extractor,m.policy.pi_features_extractor,m.policy.vf_features_extractor):f.context_path=str(out/'contexts')
 m.policy_kwargs.update(context_path=str(out/'contexts'),registry_file=str(out/'atomic_supports.jsonl'))
 for e,s in zip(env.envs,state['environments']):
  e.rng.bit_generator.state=s['rng'];e.results=s['results'];e.episode=s['episode'];e.total_steps=s['total_steps'];e.quarantined_tasks.update(s.get('quarantined_tasks',[]))
 m._last_lstm_states=type(state['lstm'])(*(tuple(t.to('cuda') for t in pair) for pair in state['lstm']));m._last_obs=state['last_obs'];m._last_episode_starts=state['episode_starts']
 m.output=out;m.history_features=history;m.reward_config={'version':base.SingleBattleReward.version,'scale':.01};m.evaluation_rows=ev;m.goal_updates=m.completed_updates+1 if a.gate else 300;m.evaluation_every=0 if a.gate else 20
 base.frozen_evaluation=evaluation.frozen_evaluation
 if a.gate:m.episodes_per_update=32
 for name in ['inherited_quarantine.json','audit_quarantine.jsonl']:
  if (parent/name).exists():
   text=(parent/name).read_text();qs=json.loads(text) if name.endswith('.json') else [json.loads(l) for l in text.splitlines()];BattleEnv.quarantined_tasks.update(q['task_id'] for q in qs)
 for name in ['metrics.jsonl','evaluation_metrics.jsonl','performance.jsonl']:
  if (parent/name).exists():
   rs=[json.loads(s) for s in (parent/name).read_text().splitlines()];rs=[r for r in rs if r['update'] < m.completed_updates] if name=='performance.jsonl' else [r for r in rs if r['update']<=m.completed_updates];(out/name).write_text(''.join(json.dumps(r)+'\n' for r in rs))
 manifest=dict(old);manifest['migration']=dict(parent=str(parent),parent_checkpoint_sha=sha(parent/'checkpoint.zip'),parent_continuation_sha=sha(parent/'continuation.pt'),completed=m.completed_updates,token_value_residual_zero_initialized=True,actor_parameters_preserved=True,old_adam_states_preserved=oldadam,closed_boundary_workers=32,stage_sha={p.name:sha(p) for p in Path('optimization_stage_20261013').glob('*.py')},value_scope='Independent scalar V head with learned attention over all root option contextual tokens before sampling; no Q/no numeric branch. Existing scalar V retained as warm residual base.',batching='bounded BF16 differences, task RNG replay gate passed; training sampling order changes')
 (out/'manifest.json').write_text(json.dumps(manifest,indent=2));(out/'migration_loaded.json').write_text(json.dumps(manifest['migration'],indent=2))
 base.restore_rng(state['rng']);del state;gc.collect()
 m.save(out/'migration_initial');start=time.perf_counter()
 while m.completed_updates<m.goal_updates:m.learn(total_timesteps=1,reset_num_timesteps=False,callback=base.EvidenceCallback())
 if a.gate:
  restored=base.RecordedRecurrentPPO.load(out/'checkpoint.zip',device='cuda');assert base.parameter_sha(restored.policy)==base.parameter_sha(m.policy)
  finite=all(torch.isfinite(v).all() for s in m.policy.optimizer.state.values() for v in s.values() if torch.is_tensor(v));assert finite
  assert torch.count_nonzero(m.policy.token_value[-1].weight)>0
  pe.BattleEnv=BattleEnv;checks=[];m.policy.set_training_mode(False)
  for w in (1,3):
   folder=out/f'eval_gate_{w}';folder.mkdir();checks.append(pe.collect(m,ev[:4],history,folder,workers=w))
  assert checks[0]==checks[1]
  (out/'gate.json').write_text(json.dumps(dict(passed=True,seconds=time.perf_counter()-start,real_games=32,reload_sha_equal=True,finite=True,token_value_updated=True,eval1_3_equal=True,scope='Integration and migration only; not fixed evaluation improvement')))
 else:
  (out/'complete.json').write_text(json.dumps(dict(updates=m.completed_updates)))
finally:env.close()
