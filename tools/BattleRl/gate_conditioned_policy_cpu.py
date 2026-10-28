"""Read-only real recurrent policy migration/reload gate, no encoder or training."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
import sys,json,io,hashlib,sqlite3
from pathlib import Path
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
import numpy as np,torch
import train_sb3_atomic_entity_text as base
import hierarchical_option_policy as h
import hierarchical_packed_cache as packed
from conditioned_context_policy import ConditionedContextPolicy
from rollout_archive import load
from sb3_contrib.common.recurrent.type_aliases import RNNStates
torch.set_num_threads(1);root=Path('remote_context_pilot200');stage=Path('optimization_stage_20261014')
raw=(root/'checkpoint.zip').read_bytes();m=base.RecordedRecurrentPPO.load(io.BytesIO(raw),device='cpu')
db=sqlite3.connect('file:'+str(root/'contexts/packed_vectors/offsets.sqlite')+'?mode=ro',uri=True)
stream=(root/'contexts/packed_vectors/vectors.bin').open('rb')
def vectors(path,context,node):
 if not context:return np.zeros((2,384),np.float32)
 assert Path(path).resolve()==(root/'contexts').resolve()
 row=db.execute('SELECT metadata FROM entries WHERE context=? AND node=?',(int(context),int(node))).fetchone()
 if row is None:raise ValueError('Missing cached node; this probe must not run encoder')
 meta=json.loads(row[0]);stream.seek(meta['offset']);payload=stream.read(meta['bytes'])
 assert hashlib.sha256(payload).hexdigest()==meta['sha256']
 return np.frombuffer(payload,dtype='<f4').reshape(meta['shape']).copy()
h.node_vectors=vectors;packed.prefetch=lambda path,requests:None
b=load(sorted(root.glob('rollout_*.archive'))[-1]);ids=b.observations['context_id'].reshape(-1).astype(int)
selected=np.flatnonzero(ids>0)[:4]
probe={k:torch.as_tensor(v.reshape((-1,)+m.observation_space.spaces[k].shape)[selected]) for k,v in b.observations.items()}
actions=torch.as_tensor(b.actions.reshape(-1)[selected],dtype=torch.long)
zeros=torch.zeros(m.policy.lstm_actor.num_layers,4,m.policy.lstm_actor.hidden_size)
recurrent=RNNStates((zeros.clone(),zeros.clone()),(zeros.clone(),zeros.clone()));starts=torch.ones(4)
m.policy.set_training_mode(False)
with torch.no_grad():
 before_v,before_p,_=m.policy.evaluate_actions(probe,actions,recurrent,starts)
 state=[];value=[]
 for context in list(dict.fromkeys(map(int,ids[ids>0])))[:128]:
  t,_=h.tree(m.policy.context_path,context);v=torch.tensor(vectors(m.policy.context_path,context,t.branch(0))[1:])
  state.append((m.policy.features_extractor.state_attention(v).softmax(0)*v).sum(0))
  value.append((m.policy.value_attention(v).softmax(0)*v).sum(0))
old_adam=len(m.policy.optimizer.state)
m.policy.__class__=ConditionedContextPolicy;m.policy.install_conditioning();m.policy_class=ConditionedContextPolicy
reset=m.policy.fit_conditioning(torch.stack(state),torch.stack(value))
assert len(m.policy.optimizer.state)==old_adam-reset['optimizer_states_reset']
with torch.no_grad():
 after_v,after_p,_=m.policy.evaluate_actions(probe,actions,recurrent,starts)
errors=dict(value=float((after_v-before_v).abs().max()),logprob=float((after_p-before_p).abs().max()))
assert max(errors.values())<1e-4,errors
m.save(stage/'conditioned_cpu_probe')
r=base.RecordedRecurrentPPO.load(stage/'conditioned_cpu_probe.zip',device='cpu');r.policy.set_training_mode(False)
assert base.parameter_sha(r.policy)==base.parameter_sha(m.policy)
with torch.no_grad():
 reload_v,reload_p,_=r.policy.evaluate_actions(probe,actions,recurrent,starts)
reload_errors=dict(value=float((reload_v-after_v).abs().max()),logprob=float((reload_p-after_p).abs().max()))
assert max(reload_errors.values())<1e-6,reload_errors
result=dict(parent_update=m.completed_updates,parent_checkpoint_sha=hashlib.sha256(raw).hexdigest(),old_adam=old_adam,new_adam=len(m.policy.optimizer.state),reset=reset,migration_errors=errors,reload_errors=reload_errors,parameter_sha_equal=True,scope='Actual recurrent policy on four real contexts at zero history, CPU only. No optimizer update or encoder invocation; CUDA training gate remains required.')
(stage/'conditioned_policy_cpu_gate.json').write_text(json.dumps(result,indent=2));print(json.dumps(result))
