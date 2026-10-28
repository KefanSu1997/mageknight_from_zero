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

sha_before=base.parameter_sha(m.policy);reports=[]
named=list(m.policy.named_parameters());params=[p for _,p in named]
for index,batch in enumerate(itertools.islice(b.get(128),4)):
 valid=batch.mask>1e-8
 values,logp,entropy=m.policy.evaluate_actions(batch.observations,batch.actions.long().flatten(),batch.lstm_states,batch.episode_starts)
 values=values.flatten();adv=batch.advantages
 adv=(adv-adv[valid].mean())/(adv[valid].std()+1e-8)
 ratio=(logp-batch.old_log_prob).exp()
 losses=dict(policy=-torch.min(adv*ratio,adv*ratio.clamp(.8,1.2))[valid].mean(),value=.5*(values[valid]-batch.returns[valid]).square().mean(),entropy=-.01*entropy[valid].mean())
 grads={key:torch.autograd.grad(loss,params,retain_graph=True,allow_unused=True) for key,loss in losses.items()}
 norms={key:sum(float(g.square().sum()) for g in gs if g is not None)**.5 for key,gs in grads.items()}
 assert all(g is None or torch.isfinite(g).all() for gs in grads.values() for g in gs)
 total=[]
 for i,p in enumerate(params):total.append(sum((gs[i] for gs in grads.values() if gs[i] is not None),torch.zeros_like(p)))
 totalnorm=sum(float(g.square().sum()) for g in total)**.5
 overlap=[i for i in range(len(params)) if grads['policy'][i] is not None and grads['value'][i] is not None]
 dot=sum(float((grads['policy'][i]*grads['value'][i]).sum()) for i in overlap)
 pn=sum(float(grads['policy'][i].square().sum()) for i in overlap)**.5
 vn=sum(float(grads['value'][i].square().sum()) for i in overlap)**.5
 reports.append(dict(batch=index,valid=int(valid.sum()),gradient_norms=norms,total_norm=totalnorm,clip_multiplier=min(1.,.5/(totalnorm+1e-6)),shared_policy_norm=pn,shared_value_norm=vn,shared_cosine=dot/(pn*vn) if pn*vn else None,shared_parameter_names=[named[i][0] for i in overlap],ratio_min=float(ratio[valid].min().detach()),ratio_max=float(ratio[valid].max().detach())))
assert base.parameter_sha(m.policy)==sha_before
result=dict(checkpoint_update=m.completed_updates,checkpoint_sha=hashlib.sha256(raw).hexdigest(),archive=str(archive),reports=reports,scope='Four actual recurrent batches at saved post-update weights; no optimizer/encoder. Gradient magnitudes are pre-Adam and do not prove proportional learning suppression.')
(stage/f'trainonly_gradient_{m.completed_updates:04d}.json').write_text(json.dumps(result,indent=2));print(json.dumps(result));stream.close();db.close()
