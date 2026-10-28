import os,sys,json,hashlib,shutil,gc,argparse,time
from pathlib import Path
sys.path.insert(0,str(Path('tools/BattleRl').resolve()));sys.path.insert(0,str(Path('optimization_stage_20261013').resolve()))
import torch,numpy as np
import train_sb3_atomic_entity_text as base
sys.path.insert(0,str(Path('optimization_stage_20261014').resolve()))
from token_value_policy import TokenValuePolicy
import hierarchical_packed_cache as packed
from entropy_token_value_policy import bind_reference, gradient_gate
from context_state_policy import ContextStatePolicy
from conditioned_context_policy import ConditionedContextPolicy
from rollout_archive import load as load_archive
import sqlite3
sys.path.insert(0,str(Path('optimization_stage_20261014').resolve()))
from hierarchical_option_policy import BattleEnv
from sb3_battle_vec import ThreadedRulesVecEnv
from atomic_transport_fast import install
from action_argument_features import catalog_argument_features
from train_ppo import Rules
import hierarchical_evaluation as evaluation
import parallel_hierarchical_eval as pe
from stable_baselines3.common.logger import configure
p=argparse.ArgumentParser();p.add_argument('--parent',required=True);p.add_argument('--out',required=True);p.add_argument('--gate',action='store_true');p.add_argument('--goal',type=int,default=300);a=p.parse_args()
parent=Path(a.parent);out=Path(a.out);assert shutil.disk_usage(out.parent).free>3*1024**3,'Insufficient disk reserve';out.mkdir(exist_ok=False)
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
old=json.loads((parent/'manifest.json').read_text());assert old['reward_scale']==.01
for name,v in old['implementation_sha'].items():assert sha(Path('tools/BattleRl')/name)==v,name
assert sha('H:/MageKnight_RL_Runs/atomic_adapter_v2_frozen/BattleRl.dll')==old['rules_sha']
for name,key in [('train.json','train_sha'),('eval.json','eval_sha')]:assert sha(Path('H:/MageKnight_RL_Runs/atomic_dataset')/name)==old[key]
os.environ.update(BATTLE_RL_CONTEXT_STORE=str(out/'contexts'),BATTLE_RL_ATOMIC_REGISTRY=str(out/'atomic_supports.jsonl'),BATTLE_RL_TEXT_ASSETS='H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets',BATTLE_RL_ENCODER_BATCH='4')
(out/'contexts').mkdir();shutil.copyfile(parent/'atomic_supports.jsonl',out/'atomic_supports.jsonl')
torch.set_num_threads(1);base.configure_rules('H:/MageKnight_RL_Runs/atomic_adapter_v2_frozen/BattleRl.dll');install();packed.install()
base.memory_record=lambda output,stage:None
r=Rules();history=catalog_argument_features(r.actions);r.close()
src=json.loads(Path('H:/MageKnight_RL_Runs/atomic_dataset/train.json').read_text());rows=src['tasks']+src.get('unknown_tasks',[]);ev=json.loads(Path('H:/MageKnight_RL_Runs/atomic_dataset/eval.json').read_text())['tasks']
state=torch.load(parent/'continuation.pt',map_location='cpu',weights_only=False)
assert all(s['closed_episode'] for s in state['environments'])
assert all(torch.count_nonzero(t)==0 for pair in state['lstm'] for t in pair)
assert np.count_nonzero(state['last_obs']['context_id'])==0,'Cannot start a separate context journal with live parent context references'
env=ThreadedRulesVecEnv([lambda i=i:BattleEnv(rows,out/'episodes',i,history,reward_config={'version':base.SingleBattleReward.version,'scale':.01}) for i in range(32)])
try:
 m=base.RecordedRecurrentPPO.load(parent/'checkpoint.zip',env=env,device='cuda',force_reset=False)
 assert m.completed_updates==state['completed_updates']
 oldadam=len(m.policy.optimizer.state)
 # Sample only archived training contexts; never fit statistics on fixed eval.
 archived=load_archive(sorted(parent.glob('rollout_*.archive'))[-1])
 from sb3_contrib.common.recurrent.type_aliases import RNNStates
 indices=np.flatnonzero(archived.observations['context_id'].reshape(-1)>0)[:4];assert len(indices)==4
 probe={k:torch.as_tensor(v.reshape((-1,)+m.observation_space.spaces[k].shape)[indices],device='cuda') for k,v in archived.observations.items()}
 actions=torch.as_tensor(archived.actions.reshape(-1)[indices],device='cuda',dtype=torch.long)
 zeros=torch.zeros((m.policy.lstm_actor.num_layers,4,m.policy.lstm_actor.hidden_size),device='cuda')
 recurrent=RNNStates((zeros.clone(),zeros.clone()),(zeros.clone(),zeros.clone()));starts=torch.ones(4,device='cuda')
 m.policy.set_training_mode(False)
 with torch.no_grad():before_values,before_logp,_=m.policy.evaluate_actions(probe,actions,recurrent,starts)
 ids=archived.observations['context_id'].reshape(-1).astype(int)
 ids=list(dict.fromkeys(int(i) for i in ids if i))[:512];assert len(ids)>=128
 db=sqlite3.connect('file:'+str(parent/'contexts/packed_vectors/offsets.sqlite')+'?mode=ro',uri=True)
 pooled_state=[];pooled_value=[]
 with (parent/'contexts/packed_vectors/vectors.bin').open('rb') as stream,torch.no_grad():
  for ctx in ids:
   row=json.loads(db.execute('SELECT metadata FROM entries WHERE context=? ORDER BY node LIMIT 1',(ctx,)).fetchone()[0]);stream.seek(row['offset'])
   v=torch.tensor(np.frombuffer(stream.read(row['bytes']),dtype='<f4').reshape(row['shape']).copy()[1:],device='cuda')
   pooled_state.append((m.policy.features_extractor.state_attention(v).softmax(0)*v).sum(0))
   pooled_value.append((m.policy.value_attention(v).softmax(0)*v).sum(0))
 db.close();pooled_state=torch.stack(pooled_state);pooled_value=torch.stack(pooled_value)
 with torch.no_grad():
  original_state=m.policy.features_extractor.state_adapter(pooled_state).clone();original_value=m.policy.token_value(pooled_value).clone()
 m.policy.__class__=ConditionedContextPolicy;m.policy.install_conditioning();m.policy_class=ConditionedContextPolicy
 conditioning=m.policy.fit_conditioning(pooled_state,pooled_value)
 assert len(m.policy.optimizer.state)==oldadam-conditioning['optimizer_states_reset']
 with torch.no_grad():
  state_error=float((m.policy.features_extractor.state_adapter(pooled_state)-original_state).abs().max())
  value_error=float((m.policy.token_value(pooled_value)-original_value).abs().max())
  after_values,after_logp,_=m.policy.evaluate_actions(probe,actions,recurrent,starts)
  full_value_error=float((before_values-after_values).abs().max());logp_error=float((before_logp-after_logp).abs().max())
 assert max(state_error,value_error)<1e-5
 assert max(full_value_error,logp_error)<1e-4
 conditioning.update(training_contexts=ids,state_max_error=state_error,value_max_error=value_error,full_value_error=full_value_error,full_action_logp_error=logp_error,parent=str(parent),parent_update=m.completed_updates)
 (out/'conditioning_gate.json').write_text(json.dumps(conditioning,indent=2))
 del archived,pooled_state,pooled_value,original_state,original_value
 m.policy.context_path=str(out/'contexts');m.policy.registry_file=str(out/'atomic_supports.jsonl')
 for f in (m.policy.features_extractor,m.policy.pi_features_extractor,m.policy.vf_features_extractor):f.context_path=str(out/'contexts')
 m.policy_kwargs.update(context_path=str(out/'contexts'),registry_file=str(out/'atomic_supports.jsonl'))
 for e,s in zip(env.envs,state['environments']):
  e.rng.bit_generator.state=s['rng'];e.results=s['results'];e.episode=s['episode'];e.total_steps=s['total_steps'];e.quarantined_tasks.update(s.get('quarantined_tasks',[]))
 m._last_lstm_states=type(state['lstm'])(*(tuple(t.to('cuda') for t in pair) for pair in state['lstm']));m._last_obs=state['last_obs'];m._last_episode_starts=state['episode_starts']
 m.output=out;m.history_features=history;m.reward_config={'version':base.SingleBattleReward.version,'scale':.01};m.evaluation_rows=ev;m.goal_updates=m.completed_updates+1 if a.gate else a.goal;m.evaluation_every=0 if a.gate else 20
 base.frozen_evaluation=evaluation.frozen_evaluation
 if a.gate:m.episodes_per_update=32
 for name in ['inherited_quarantine.json','audit_quarantine.jsonl']:
  if (parent/name).exists():
   text=(parent/name).read_text();qs=json.loads(text) if name.endswith('.json') else [json.loads(l) for l in text.splitlines()];BattleEnv.quarantined_tasks.update(q['task_id'] for q in qs)
 for name in ['metrics.jsonl','evaluation_metrics.jsonl','performance.jsonl']:
  if (parent/name).exists():
   rs=[json.loads(s) for s in (parent/name).read_text().splitlines()];rs=[r for r in rs if r['update'] < m.completed_updates] if name=='performance.jsonl' else [r for r in rs if r['update']<=m.completed_updates];(out/name).write_text(''.join(json.dumps(r)+'\n' for r in rs))
 manifest=dict(old);manifest['migration']=dict(parent=str(parent),parent_checkpoint_sha=sha(parent/'checkpoint.zip'),parent_continuation_sha=sha(parent/'continuation.pt'),completed=m.completed_updates,token_value_residual_zero_initialized=False,all_existing_value_parameters_preserved=True,contextual_state_residual_zero_initialized=True,actor_and_critic_lstm_read_contextual_tokens=True,entropy_estimator="importance weighted joint action entropy, differentiable ratio",batched_value=True,actor_parameters_preserved=True,old_adam_states_preserved=oldadam,closed_boundary_workers=32,stage_sha={p.name:sha(p) for p in Path('optimization_stage_20261014').glob('*.py')},value_scope='Independent scalar V head with learned attention over all root option contextual tokens before sampling; no Q/no numeric branch. Existing scalar V retained as warm residual base.',batching='bounded BF16 differences, task RNG replay gate passed; training sampling order changes')
 manifest['migration'].update(contextual_state_residual_zero_initialized=False,actor_parameters_preserved=False,all_existing_value_parameters_preserved=False,old_adam_states_preserved=oldadam-conditioning['optimizer_states_reset'],conditioning=conditioning,context_journal='Fresh local IDs at verified closed/zero-context boundary. Parent journal and archives remain in their original run; never interpret parent IDs in this new journal.')
 (out/'manifest.json').write_text(json.dumps(manifest,indent=2));(out/'migration_loaded.json').write_text(json.dumps(manifest['migration'],indent=2))
 base.restore_rng(state['rng']);del state;gc.collect()
 m.save(out/'migration_initial');start=time.perf_counter()
 gate_parameters=['features_extractor.state_adapter.1.weight','token_value.1.weight']
 before_training={n:p.detach().clone() for n,p in m.policy.named_parameters() if n in gate_parameters} if a.gate else {}
 if a.gate:assert len(before_training)==2
 original_train=base.RecordedRecurrentPPO.train
 def corrected_train(self):
  bind_reference(self)
  try:return original_train(self)
  finally:self.policy._entropy_reference=None
 base.RecordedRecurrentPPO.train=corrected_train
 (out/'entropy_gradient_gate.json').write_text(json.dumps(gradient_gate()))
 while m.completed_updates<m.goal_updates:
  if shutil.disk_usage(out).free<3*1024**3:raise RuntimeError('Disk reserve low at complete checkpoint; preserve and relocate before continuing')
  m.learn(total_timesteps=1,reset_num_timesteps=False,callback=base.EvidenceCallback())
 if a.gate:
  restored=base.RecordedRecurrentPPO.load(out/'checkpoint.zip',device='cuda');assert base.parameter_sha(restored.policy)==base.parameter_sha(m.policy)
  saved_state=restored.policy.state_dict();actual_state=m.policy.state_dict()
  assert saved_state.keys()==actual_state.keys() and all(torch.equal(saved_state[n],v) for n,v in actual_state.items()),'Reload parameter/buffer mismatch'
  finite=all(torch.isfinite(v).all() for s in m.policy.optimizer.state.values() for v in s.values() if torch.is_tensor(v));assert finite
  assert torch.count_nonzero(m.policy.token_value[-1].weight)>0
  assert torch.count_nonzero(m.policy.features_extractor.state_adapter[-1].weight)>0
  changes={n:float((p-before_training[n]).abs().max()) for n,p in m.policy.named_parameters() if n in before_training}
  steps={n:float(m.policy.optimizer.state[p]['step']) for n,p in m.policy.named_parameters() if n in before_training}
  assert all(v>0 for v in changes.values()) and all(v>0 for v in steps.values()),'Conditioned readouts did not optimize'
  pe.BattleEnv=BattleEnv;checks=[];m.policy.set_training_mode(False)
  for w in (1,3):
   folder=out/f'eval_gate_{w}';folder.mkdir();checks.append(pe.collect(m,ev[:4],history,folder,workers=w))
  assert checks[0]==checks[1]
  (out/'gate.json').write_text(json.dumps(dict(passed=True,seconds=time.perf_counter()-start,real_games=32,reload_sha_equal=True,reload_all_buffers_equal=True,finite=True,conditioned_parameter_deltas=changes,conditioned_adam_steps=steps,eval1_3_equal=True,scope='Integration and migration only; not fixed evaluation improvement')))
 else:
  (out/'complete.json').write_text(json.dumps(dict(updates=m.completed_updates)))
finally:env.close()
