"""Read-only current gradient interference audit; no optimizer or encoder."""
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
torch.set_num_threads(1);root=Path('remote_repaired_trainonly300');stage=Path('optimization_stage_20261014')
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
archive=next(root.glob(f'rollout_{m.completed_updates-1:04d}_*.archive'));assert int(archive.name.split('_')[1])==m.completed_updates-1,'Checkpoint/archive boundary changed; rerun later';b=load(archive);b.device=torch.device('cpu');m.rollout_buffer=b;bind_reference(m);np.random.seed(7112026)


sha_before=base.parameter_sha(m.policy)
batch=next(b.get(128));valid=batch.mask>1e-8;target=batch.returns[valid]
records=[]
for step in range(41):
 values=m.policy.predict_values(batch.observations,batch.lstm_states.vf,batch.episode_starts).flatten()[valid]
 error=values-target;loss=.5*error.square().mean()
 if step in [0,4,12,40]:
  records.append(dict(optimizer_steps=step,ev=float((1-error.var(unbiased=False)/target.var(unbiased=False)).detach()),mse=float(error.square().mean().detach()),bias=float(error.mean().detach())))
 if step==40:break
 m.policy.optimizer.zero_grad();loss.backward();torch.nn.utils.clip_grad_norm_(m.policy.parameters(),.5)
 assert all(p.grad is None or torch.isfinite(p.grad).all() for p in m.policy.parameters())
 m.policy.optimizer.step()
assert base.parameter_sha(m.policy)!=sha_before
result=dict(checkpoint_update=m.completed_updates,checkpoint_sha=hashlib.sha256(raw).hexdigest(),archive=str(archive),valid_samples=int(valid.sum()),target_variance=float(target.var(unbiased=False)),history=records,scope='Isolated CPU copy, existing Adam/lr, repeated critic-only fitting of one archived recurrent minibatch using recorded initial hidden states; no policy objective, encoder, live mutation, checkpoint save, fixed evaluation or generalization evidence.')
(stage/f'trainonly_value_fit_{m.completed_updates:04d}.json').write_text(json.dumps(result,indent=2));print(json.dumps(result));stream.close();db.close()
