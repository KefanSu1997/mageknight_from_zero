"""Exact collector checkpoint, saved recurrent states, separate scalar V branches."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
import sys,io,json,sqlite3,hashlib
from pathlib import Path
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
import numpy as np
import torch
import train_sb3_atomic_entity_text as base
import hierarchical_option_policy as h
import hierarchical_packed_cache as packed
from rollout_archive import load
torch.set_num_threads(1);root=Path('remote_repaired_trainonly300')
paths=list(root.glob('behavior_0050_*.zip'));assert len(paths)==1
raw=paths[0].read_bytes();m=base.RecordedRecurrentPPO.load(io.BytesIO(raw),device='cpu');assert m.completed_updates==50;m.policy.set_training_mode(False)
archives=list(root.glob('rollout_0050_*.archive'));assert len(archives)==1;b=load(archives[0]);target=b.returns.ravel();n=len(target)
db=sqlite3.connect('file:'+str(root/'contexts/packed_vectors/offsets.sqlite')+'?mode=ro',uri=True);stream=(root/'contexts/packed_vectors/vectors.bin').open('rb')
def vectors(path,ctx,node):
 if not ctx:return np.zeros((2,384),np.float32)
 row=db.execute('SELECT metadata FROM entries WHERE context=? AND node=?',(int(ctx),int(node))).fetchone();assert row
 meta=json.loads(row[0]);stream.seek(meta['offset']);payload=stream.read(meta['bytes']);assert hashlib.sha256(payload).hexdigest()==meta['sha256']
 return np.frombuffer(payload,dtype='<f4').reshape(meta['shape']).copy()
h.node_vectors=vectors;packed.prefetch=lambda *x:None
total=[];direct=[];zero_memory=[]
with torch.no_grad():
 for start in range(0,n,128):
  end=min(start+128,n);obs={k:torch.tensor(v.reshape((-1,)+m.observation_space.spaces[k].shape)[start:end]) for k,v in b.observations.items()}
  memory=tuple(torch.tensor(x[start:end,:,0,:].transpose(1,0,2).copy()) for x in (b.hidden_states_vf,b.cell_states_vf));flags=torch.tensor(b.episode_starts.ravel()[start:end])
  total.append(m.policy.predict_values(obs,memory,flags).ravel().numpy());direct.append(m.policy.contextual_value(obs).ravel().numpy())
  zero=tuple(torch.zeros_like(x) for x in memory)
  zero_memory.append(m.policy.predict_values(obs,zero,torch.ones_like(flags)).ravel().numpy())
total=np.concatenate(total);direct=np.concatenate(direct);recurrent=total-direct;error=float(abs(total-b.values.ravel()).max());ids=b.observations['context_id'].ravel().astype(int)
rows=[dict(context=int(ids[i]),target=float(target[i]),total=float(total[i]),direct=float(direct[i]),recurrent=float(recurrent[i]),archived=float(b.values.ravel()[i])) for i in np.argsort((total-target)**2)[-20:][::-1]]
def stats(x):return dict(mean=float(x.mean()),std=float(x.std()),quantiles=np.quantile(x,[0,.05,.5,.95,1]).tolist())
r=dict(checkpoint=str(paths[0]),checkpoint_sha=hashlib.sha256(raw).hexdigest(),archive=str(archives[0]),states=n,episodes=int(b.episode_starts.sum()),replay_max_error=error,replay_tolerance=1e-4,replay_passed=error<1e-4,direct=stats(direct),recurrent=stats(recurrent),total=stats(total),largest_errors=rows,scope='Collector50 on its original next batch, stored behavior LSTM state per sample, CPU cached vectors only. Branch values are additive components, not independently trained predictors. No optimizer/encoder execution.')
zero_memory=np.concatenate(zero_memory)
def score(pred):return dict(mse=float(np.mean((pred-target)**2)),ev=float(1-np.var(pred-target)/np.var(target)))
r['memory_ablation']=dict(original=score(total),zero_each_state=score(zero_memory),scope='Inference ablation changes the history input distribution. Does not authorize resetting live critic or prove a trained feedforward model would improve.')
Path('optimization_stage_20261014/value_components50_memory.json').write_text(json.dumps(r,indent=2));print(json.dumps(r));assert error<1e-4
