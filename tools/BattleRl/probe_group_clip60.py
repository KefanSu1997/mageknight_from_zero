"""Paired one-step Adam diagnostic; independent copies, production untouched."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
import sys,json,io,sqlite3,hashlib,tarfile,itertools,collections
from pathlib import Path
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
import torch,numpy as np
import train_sb3_atomic_entity_text as base
import hierarchical_option_policy as h
import hierarchical_packed_cache as packed
from entropy_token_value_policy import bind_reference
from rollout_archive import load
torch.set_num_threads(1);root=Path('remote_repaired_trainonly300');out=Path('optimization_stage_20261014/group_clip60.json');assert not out.exists()
with tarfile.open(root/'checkpoint_bundle_0060.tar.gz') as tar:raw=tar.extractfile('checkpoint.zip').read()
archive=list(root.glob('rollout_0060_*.archive'));assert len(archive)==1
b=load(archive[0]);b.device=torch.device('cpu')
db=sqlite3.connect('file:'+str(root/'contexts/packed_vectors/offsets.sqlite')+'?mode=ro',uri=True);stream=(root/'contexts/packed_vectors/vectors.bin').open('rb');cache={}
def vectors(path,ctx,node):
 if not ctx:return np.zeros((2,384),np.float32)
 key=(int(ctx),int(node))
 if key not in cache:
  row=db.execute('SELECT metadata FROM entries WHERE context=? AND node=?',key).fetchone();assert row
  meta=json.loads(row[0]);stream.seek(meta['offset']);payload=stream.read(meta['bytes']);assert hashlib.sha256(payload).hexdigest()==meta['sha256']
  cache[key]=np.frombuffer(payload,dtype='<f4').reshape(meta['shape']).copy()
 return cache[key]
h.node_vectors=vectors;packed.prefetch=lambda *args:None
np.random.seed(7112026);batches=list(itertools.islice(b.get(128),4));results=[]
def group(name):
 if name.startswith('features_extractor'):return 'shared'
 if 'lstm_critic' in name or 'value_net' in name or 'token_value' in name or 'value_attention' in name:return 'critic'
 return 'actor'
def forward(m,batch):
 valid=batch.mask>1e-8
 v,lp,e=m.policy.evaluate_actions(batch.observations,batch.actions.long().flatten(),batch.lstm_states,batch.episode_starts)
 adv=batch.advantages;adv=(adv-adv[valid].mean())/(adv[valid].std()+1e-8);ratio=(lp-batch.old_log_prob).exp()
 pg=-torch.min(adv*ratio,adv*ratio.clamp(.8,1.2))[valid].mean();vl=.5*(v.flatten()[valid]-batch.returns[valid]).square().mean();en=-.01*e[valid].mean()
 lr=lp[valid]-batch.old_log_prob[valid]
 return pg+vl+en,dict(policy_loss=float(pg.detach()),value_mse=float((vl*2).detach()),entropy_loss=float(en.detach()),approx_kl=float(((lr.exp()-1)-lr).mean().detach()),logprob_replay_max=float(lr.abs().max().detach()))
for index,batch in enumerate(batches):
 for mode in ('global','partitioned'):
  m=base.RecordedRecurrentPPO.load(io.BytesIO(raw),device='cpu');assert m.completed_updates==60
  m.rollout_buffer=b;bind_reference(m);m.policy.set_training_mode(False)
  named=list(m.policy.named_parameters());before={n:p.detach().clone() for n,p in named}
  m.policy.optimizer.zero_grad();loss,pre=forward(m,batch);assert pre['logprob_replay_max']<.001;loss.backward()
  groups=collections.defaultdict(list)
  for n,p in named:groups[group(n)].append(p)
  norms={k:float(torch.linalg.vector_norm(torch.stack([p.grad.norm() for p in ps if p.grad is not None]))) for k,ps in groups.items()}
  if mode=='global':torch.nn.utils.clip_grad_norm_(m.policy.parameters(),.5)
  else:
   for ps in groups.values():torch.nn.utils.clip_grad_norm_(ps,.5)
  m.policy.optimizer.step()
  assert all(torch.isfinite(p).all() for _,p in named)
  delta={k:sum(float((p.detach()-before[n]).square().sum()) for n,p in named if group(n)==k)**.5 for k in groups}
  with torch.no_grad():
   _,post=forward(m,batch);_,other=forward(m,batches[(index+1)%4])
  row=dict(batch=index,mode=mode,pre=pre,post=post,other_batch=other,preclip_group_norms=norms,actual_parameter_delta=delta)
  results.append(row);print(json.dumps(row),flush=True)
report=dict(checkpoint_sha=hashlib.sha256(raw).hexdigest(),archive=str(archive[0]),results=results,scope='Each arm reloads identical behavior60 weights and saved Adam; one actual optimizer step on identical archived recurrent minibatch. Separate norm limits .5 for shared/actor/critic vs original global .5. No long-run or environment return evidence, no production changes.')
out.write_text(json.dumps(report,indent=2))
