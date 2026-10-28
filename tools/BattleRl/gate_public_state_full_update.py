"""Isolated paired upstream PPO update, same rollout/order/Adam; no live sampling."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
import sys,json,io,sqlite3,hashlib,collections,gc,time
from contextlib import nullcontext
from pathlib import Path
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
import torch,numpy as np
import train_sb3_atomic_entity_text as base
import hierarchical_option_policy as h
import hierarchical_packed_cache as packed
from entropy_token_value_policy import bind_reference
from partitioned_policy_clip import installed
from rollout_archive import load
from sb3_contrib import RecurrentPPO
from stable_baselines3.common.logger import configure
torch.set_num_threads(1);root=Path('remote_repaired_trainonly300');out=Path('optimization_stage_20261014/public_state_full_update.json');assert not out.exists()
checkpoint=list(root.glob('behavior_0050_*.zip'));assert len(checkpoint)==1;raw=checkpoint[0].read_bytes()
archives={u:list(root.glob(f'rollout_{u:04d}_*.archive')) for u in (50,60)};assert all(len(x)==1 for x in archives.values())
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
def score(m,b):
 starts=np.flatnonzero(b.episode_starts.ravel());assert len(starts)==128 and starts[0]==0
 ends=np.r_[starts[1:],len(b.returns.ravel())];width=max(ends-starts);n=128*width
 obs={k:torch.zeros((n,)+space.shape,device='cpu',dtype=torch.float32) for k,space in m.observation_space.spaces.items()};target=torch.zeros(n,device='cpu');mask=torch.zeros(n,device='cpu',dtype=torch.bool);flags=torch.zeros(n,device='cpu')
 for j,(a,z) in enumerate(zip(starts,ends)):
  dest=slice(j*width,j*width+z-a);flags[j*width]=1;mask[dest]=True;target[dest]=torch.tensor(b.returns.ravel()[a:z],device='cpu')
  for k in obs:obs[k][dest]=torch.tensor(b.observations[k].reshape((-1,)+m.observation_space.spaces[k].shape)[a:z],device='cpu')
 with torch.no_grad():
  zero=lambda:torch.zeros((1,128,256),device='cpu');pred=m.policy.predict_values(obs,(zero(),zero()),flags).ravel()[mask];t=target[mask];err=t-pred
  return dict(mse=float(err.square().mean()),ev=float(1-err.var(unbiased=False)/t.var(unbiased=False)),bias=float(-err.mean()),states=len(t))
results={}
for mode in ('control','public_state'):
 m=base.RecordedRecurrentPPO.load(io.BytesIO(raw),device='cpu');assert m.completed_updates==50
 b=load(archives[50][0]);future=load(archives[60][0]);b.device=torch.device('cpu');m.rollout_buffer=b;m.set_logger(configure(folder=None,format_strings=[]));bind_reference(m)
 if mode=='public_state':
  from public_state_residual_candidate import PublicStateResidualPolicy
  torch.manual_seed(7112026)
  m.policy.__class__=PublicStateResidualPolicy;m.policy_class=PublicStateResidualPolicy;m.policy.install_public_state_residual()
 before=dict(train=score(m,b),future=score(m,future));records=[];np.random.seed(7112026);torch.manual_seed(7112026)
 original_clip=torch.nn.utils.clip_grad_norm_;start=time.perf_counter()
 with installed(m.policy,records):RecurrentPPO.train(m)
 assert torch.nn.utils.clip_grad_norm_ is original_clip
 assert all(torch.isfinite(p).all() for p in m.policy.parameters())
 assert all(torch.isfinite(v).all() for s in m.policy.optimizer.state.values() for v in s.values() if torch.is_tensor(v))
 after=dict(train=score(m,b),future=score(m,future));payload=io.BytesIO();m.save(payload);payload.seek(0);reloaded=base.RecordedRecurrentPPO.load(payload,device='cpu')
 assert base.parameter_sha(reloaded.policy)==base.parameter_sha(m.policy)
 result=dict(before=before,after=after,logger={k:float(v) for k,v in m.logger.name_to_value.items()},group_clip_calls=len(records),seconds=time.perf_counter()-start,reload_parameters_buffers_equal=True,adam_finite=True)
 results[mode]=result;print(mode,json.dumps(result),flush=True);del m,reloaded,b,future;gc.collect()
report=dict(checkpoint_sha=hashlib.sha256(raw).hexdigest(),results=results,scope='Remote CPU-only full upstream four-epoch PPO update on rollout50: partitioned clip in both arms, zero numeric residual only in candidate. Paired saved weights/Adam and minibatch RNG. Future rollout60 full-sequence zero-at-episode-boundary value prediction; no fixed eval or new policy returns. In-memory models only, no production mutation.')
out.write_text(json.dumps(report,indent=2))


