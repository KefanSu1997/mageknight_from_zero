"""Exact behavior60 checkpoint vs its next rollout: no optimizer mutation."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
import sys,json,io,sqlite3,hashlib,tarfile,itertools
from pathlib import Path
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
import torch,numpy as np
import train_sb3_atomic_entity_text as base
import hierarchical_option_policy as h
import hierarchical_packed_cache as packed
from entropy_token_value_policy import bind_reference
from rollout_archive import load
torch.set_num_threads(1);root=Path('remote_repaired_trainonly300');out=Path('optimization_stage_20261014/trainonly_gradient60.json');assert not out.exists()
with tarfile.open(root/'checkpoint_bundle_0060.tar.gz') as tar:raw=tar.extractfile('checkpoint.zip').read()
m=base.RecordedRecurrentPPO.load(io.BytesIO(raw),device='cpu');assert m.completed_updates==60
archive=list(root.glob('rollout_0060_*.archive'));assert len(archive)==1
b=load(archive[0]);b.device=torch.device('cpu');m.rollout_buffer=b;bind_reference(m)
db=sqlite3.connect('file:'+str(root/'contexts/packed_vectors/offsets.sqlite')+'?mode=ro',uri=True);stream=(root/'contexts/packed_vectors/vectors.bin').open('rb')
def vectors(path,ctx,node):
 if not ctx:return np.zeros((2,384),np.float32)
 row=db.execute('SELECT metadata FROM entries WHERE context=? AND node=?',(int(ctx),int(node))).fetchone();assert row
 meta=json.loads(row[0]);stream.seek(meta['offset']);payload=stream.read(meta['bytes']);assert hashlib.sha256(payload).hexdigest()==meta['sha256']
 return np.frombuffer(payload,dtype='<f4').reshape(meta['shape']).copy()
h.node_vectors=vectors;packed.prefetch=lambda *args:None
np.random.seed(7112026);batches=list(itertools.islice(b.get(128),4));results=[]
def group(name):
 if name.startswith('features_extractor'):return 'shared_state'
 if 'lstm_actor' in name or 'policy_net' in name:return 'actor_recurrent'
 if 'lstm_critic' in name or 'value_net' in name:return 'critic_recurrent'
 if 'token_value' in name or 'value_attention' in name:return 'text_value'
 return 'action_readout'
for index,batch in enumerate(batches):
 valid=batch.mask>1e-8;m.policy.set_training_mode(False)
 values,logp,entropy=m.policy.evaluate_actions(batch.observations,batch.actions.long().flatten(),batch.lstm_states,batch.episode_starts)
 error=float((logp[valid]-batch.old_log_prob[valid]).abs().max());assert error<.001,error
 adv=batch.advantages;adv=(adv-adv[valid].mean())/(adv[valid].std()+1e-8);ratio=(logp-batch.old_log_prob).exp()
 losses=dict(policy=-torch.min(adv*ratio,adv*ratio.clamp(.8,1.2))[valid].mean(),value=.5*(values.flatten()[valid]-batch.returns[valid]).square().mean(),entropy=-.01*entropy[valid].mean())
 losses['total']=sum(losses.values());named=list(m.policy.named_parameters());params=[p for _,p in named];norms={};shared={}
 for label,loss in losses.items():
  grads=torch.autograd.grad(loss,params,retain_graph=True,allow_unused=True);sums={};parts=[]
  for (name,p),g in zip(named,grads):
   if g is not None:
    assert torch.isfinite(g).all();key=group(name);sums[key]=sums.get(key,0)+float(g.square().sum())
   if group(name)=='shared_state':parts.append((torch.zeros_like(p) if g is None else g).flatten())
  shared[label]=torch.cat(parts);norms[label]={k:v**.5 for k,v in sums.items()};norms[label]['all']=sum(sums.values())**.5
 cosine=float(torch.nn.functional.cosine_similarity(shared['policy'],shared['value'],dim=0))
 results.append(dict(batch=index,samples=int(valid.sum()),logprob_replay_max=error,gradient_norms=norms,shared_policy_value_cosine=cosine,global_clip_multiplier=min(1.,.5/(norms['total']['all']+1e-6))))
report=dict(completed=60,checkpoint_sha=hashlib.sha256(raw).hexdigest(),archive=str(archive[0]),results=results,scope='Four real recurrent minibatches, matching behavior weights and stored memory, CPU SHA-checked vector caches, gradient-only no optimizer/encoder. Clip multiplier describes local loss gradients, not effective Adam parameter displacement or long-run causality.')
out.write_text(json.dumps(report,indent=2));print(json.dumps(report),flush=True)
