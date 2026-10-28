"""Actual recurrent CPU gate for candidate estimator; no optimizer or encoder."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
import sys,io,json,hashlib,sqlite3,itertools
from pathlib import Path
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
import numpy as np
import torch
import train_sb3_atomic_entity_text as base
import hierarchical_option_policy as h
import hierarchical_packed_cache as packed
from conditioned_context_policy import ConditionedContextPolicy
from root_entropy_conditioned_policy import RootEntropyConditionedPolicy
from entropy_token_value_policy import bind_reference
from rollout_archive import load
torch.set_num_threads(1);root=Path('remote_conditioned300');stage=Path('optimization_stage_20261014')
raw=(root/'checkpoint.zip').read_bytes();m=base.RecordedRecurrentPPO.load(io.BytesIO(raw),device='cpu');m.policy.set_training_mode(False)
db=sqlite3.connect('file:'+str(root/'contexts/packed_vectors/offsets.sqlite')+'?mode=ro',uri=True);stream=(root/'contexts/packed_vectors/vectors.bin').open('rb')
def vectors(path,context,node):
 if not context:return np.zeros((2,384),np.float32)
 assert Path(path).resolve()==(root/'contexts').resolve()
 row=db.execute('SELECT metadata FROM entries WHERE context=? AND node=?',(int(context),int(node))).fetchone()
 if row is None:raise RuntimeError('Missing cache: no encoder allowed')
 meta=json.loads(row[0]);stream.seek(meta['offset']);payload=stream.read(meta['bytes']);assert hashlib.sha256(payload).hexdigest()==meta['sha256']
 return np.frombuffer(payload,dtype='<f4').reshape(meta['shape']).copy()
h.node_vectors=vectors;packed.prefetch=lambda path,requests:None
archive=sorted(root.glob('rollout_*.archive'))[-1];b=load(archive);b.device=torch.device('cpu');m.rollout_buffer=b;bind_reference(m);np.random.seed(7112026)
sha_before=base.parameter_sha(m.policy);reports=[]
for index,batch in enumerate(itertools.islice(b.get(128),4)):
 args=(batch.observations,batch.actions.long().flatten(),batch.lstm_states,batch.episode_starts)
 m.policy.__class__=ConditionedContextPolicy
 with torch.no_grad():old_v,old_p,old_h=m.policy.evaluate_actions(*args)
 m.policy.__class__=RootEntropyConditionedPolicy
 v,p,entropy=m.policy.evaluate_actions(*args);valid=batch.mask>1e-8
 errors=dict(value=float((v-old_v).abs().max()),logprob=float((p-old_p).abs().max()))
 assert max(errors.values())<1e-6
 assert torch.isfinite(entropy).all()
 grads=torch.autograd.grad(-.01*entropy[valid].mean(),list(m.policy.parameters()),allow_unused=True)
 assert all(g is None or torch.isfinite(g).all() for g in grads)
 assert not getattr(m.policy,'_capture_entropy_distribution',False) and '_root_entropy_distribution' not in m.policy.__dict__
 reports.append(dict(batch=index,valid_samples=int(valid.sum()),errors=errors,old_entropy_mean=float(old_h[valid].mean()),candidate_entropy_mean=float(entropy[valid].mean().detach()),weighted_entropy_gradient_norm=sum(float(g.square().sum()) for g in grads if g is not None)**.5))
assert base.parameter_sha(m.policy)==sha_before
# Serialization gate: clear ephemeral rollout reference before saving.
m.policy._entropy_reference=None;m.policy_class=RootEntropyConditionedPolicy;m.save(stage/'root_entropy_cpu_probe')
restored=base.RecordedRecurrentPPO.load(stage/'root_entropy_cpu_probe.zip',device='cpu');assert base.parameter_sha(restored.policy)==sha_before
assert all(torch.equal(t,restored.policy.state_dict()[name]) for name,t in m.policy.state_dict().items())
report=dict(passed=True,checkpoint_update=m.completed_updates,checkpoint_sha=hashlib.sha256(raw).hexdigest(),archive=str(archive),reports=reports,parameters_unchanged=True,reload_all_buffers_equal=True,scope='Actual saved recurrent CPU batches only. No optimizer step, encoder execution or deployment. Does not establish lower variance or learning improvement.')
(stage/'root_entropy_cpu_gate.json').write_text(json.dumps(report,indent=2));print(json.dumps(report));stream.close();db.close()
