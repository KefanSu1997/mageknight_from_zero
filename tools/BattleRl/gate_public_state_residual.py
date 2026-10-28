"""Remote CPU/cache-only migration gate, isolated from the live trainer."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
import sys,json,io,sqlite3,hashlib
from pathlib import Path
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
import torch,numpy as np
import train_sb3_atomic_entity_text as base
import hierarchical_option_policy as h
import hierarchical_packed_cache as packed
from rollout_archive import load
from public_state_residual_candidate import PublicStateResidualPolicy
torch.set_num_threads(1)
root=Path('remote_repaired_trainonly300');out=Path('optimization_stage_20261014/public_state_residual_gate.json');assert not out.exists()
raw=(root/'checkpoint.zip').read_bytes();m=base.RecordedRecurrentPPO.load(io.BytesIO(raw),device='cpu');assert m.completed_updates==61
b=load(next(root.glob('rollout_0060_*.archive')));b.device=torch.device('cpu');np.random.seed(7112026);batch=next(b.get(128));valid=batch.mask>0
db=sqlite3.connect('file:'+str(root/'contexts/packed_vectors/offsets.sqlite')+'?mode=ro',uri=True);stream=(root/'contexts/packed_vectors/vectors.bin').open('rb')
def vectors(path,ctx,node):
 if not ctx:return np.zeros((2,384),np.float32)
 row=db.execute('SELECT metadata FROM entries WHERE context=? AND node=?',(int(ctx),int(node))).fetchone();assert row
 meta=json.loads(row[0]);stream.seek(meta['offset']);payload=stream.read(meta['bytes']);assert hashlib.sha256(payload).hexdigest()==meta['sha256']
 return np.frombuffer(payload,dtype='<f4').reshape(meta['shape']).copy()
h.node_vectors=vectors;packed.prefetch=lambda *args:None
def outputs(model):return model.policy.evaluate_actions(batch.observations,batch.actions.long().flatten(),batch.lstm_states,batch.episode_starts)
m.policy.set_training_mode(False)
with torch.no_grad():before=outputs(m)
old_values={n:v.clone() for n,v in m.policy.state_dict().items()};old_adam=dict(m.policy.optimizer.state)
m.policy.__class__=PublicStateResidualPolicy;m.policy_class=PublicStateResidualPolicy;m.policy.install_public_state_residual()
assert all(torch.equal(v,m.policy.state_dict()[n]) for n,v in old_values.items())
assert all(m.policy.optimizer.state[p] is v for p,v in old_adam.items())
with torch.no_grad():after=outputs(m)
assert torch.equal(before[0],after[0]) and torch.equal(before[1],after[1])
values,logp,_=outputs(m);target=batch.returns[valid];adv=batch.advantages[valid]
actor_loss=-(logp[valid]*adv).mean();value_loss=(values.flatten()[valid]-target).square().mean()
last=m.policy.features_extractor.public_state_residual[-1].weight
actor_grad=torch.autograd.grad(actor_loss,last,retain_graph=True)[0];critic_grad=torch.autograd.grad(value_loss,last,retain_graph=True)[0]
assert torch.isfinite(actor_grad).all() and torch.isfinite(critic_grad).all()
assert actor_grad.norm()>0 and critic_grad.norm()>0
buf=io.BytesIO();m.save(buf);buf.seek(0);restored=base.RecordedRecurrentPPO.load(buf,device='cpu');restored.policy.set_training_mode(False)
assert base.parameter_sha(restored.policy)==base.parameter_sha(m.policy)
assert len(restored.policy.optimizer.state)==len(old_adam)
with torch.no_grad():reloaded=outputs(restored)
assert torch.equal(after[0],reloaded[0]) and torch.equal(after[1],reloaded[1])
report=dict(passed=True,parent_update=61,checkpoint_sha=hashlib.sha256(raw).hexdigest(),samples=int(valid.sum()),state_width=int(batch.observations['state'].shape[-1]),existing_state_dict_equal=True,existing_adam_objects_preserved=len(old_adam),migration_logprob_max=0.,migration_value_max=0.,reload_logprob_max=0.,reload_value_max=0.,numeric_actor_gradient=float(actor_grad.norm()),numeric_critic_gradient=float(critic_grad.norm()),scope='One real archived recurrent batch; zero-residual migration, gradient reachability and memory save/reload only. No optimizer step, encoder, new sampling, fixed evaluation, or live mutation. No learning benefit claim.')
out.write_text(json.dumps(report,indent=2));print(json.dumps(report))
