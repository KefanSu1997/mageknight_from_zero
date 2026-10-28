"""Paired offline actual recurrent V architecture, TRAIN50 fit/TRAIN60 holdout."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
import sys,json,hashlib,time,gc
from pathlib import Path
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
import numpy as np
import torch
import train_sb3_atomic_entity_text as base
import hierarchical_option_policy as h
import hierarchical_packed_cache as packed
from hierarchical_batch_encoder import BatchedReader
from critical_public_state_text import document
torch.set_num_threads(1)
root=Path('remote_repaired_trainonly300');out=Path('optimization_stage_20261014/critical_actual_critic50_to60.json');assert not out.exists()
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
initial=root/'initial_checkpoint.zip';initial_sha=sha(initial);sources=[];episodes={50:[],60:[]};wanted={}
for line in (root/'episode_batches.jsonl').read_text().splitlines():
 b=json.loads(line)
 if b['update'] not in episodes:continue
 for e in b['episodes']:
  p=root/'episodes'/f"worker_{e['worker']}_episode_{e['episode']}.json";raw=p.read_bytes();trace=json.loads(raw)['trace']
  rows=[]
  for i,t in enumerate(trace):
   sid=int(t['support_id']);rows.append(dict(support=sid,target=sum(float(z['training_reward']) for z in trace[i:])))
   wanted[sid]=rows[-1]
  episodes[b['update']].append(rows);sources.append(dict(path=str(p),sha256=hashlib.sha256(raw).hexdigest()))
assert all(len(x)==128 for x in episodes.values())
docs={}
with (root/'contexts/contexts.jsonl').open() as f:
 for ctx,line in enumerate(f,1):
  d=json.loads(line);row=wanted.get(int(d['support_id']))
  if row is None:continue
  row['context']=ctx;tree=h.ChoiceTree(d['operations']);d=tree.document(d,tree.branch(0))
  if not d['options']:d['options']=['唯一完整操作，直接执行']
  docs[ctx]=d
assert len(docs)==len(wanted)
reader=BatchedReader();all_vectors={};times={}
for name in ['raw','critical']:
 start=time.perf_counter();vectors={};ids=list(docs)
 for offset in range(0,len(ids),4):
  subset=ids[offset:offset+4];batch=[docs[i] if name=='raw' else document(docs[i]) for i in subset]
  vectors.update(zip(subset,reader.encode_many(batch)))
 all_vectors[name]=vectors;times[name]=time.perf_counter()-start;print('encoded',name,len(vectors),flush=True)
del reader;gc.collect();torch.cuda.empty_cache()
results={};packed.prefetch=lambda *args:None
for name in ['raw','critical']:
 m=base.RecordedRecurrentPPO.load(initial,device='cuda');assert m.completed_updates==0 and not m.policy.optimizer.state
 m.policy.set_training_mode(True);assert sha(initial)==initial_sha
 vectors=all_vectors[name]
 h.node_vectors=lambda path,ctx,node: vectors[int(ctx)] if ctx else np.zeros((2,384),np.float32)
 batches={}
 for update,rows in episodes.items():
  width=max(map(len,rows));n=len(rows)*width
  obs={k:torch.zeros((n,)+space.shape,device='cuda',dtype=torch.float32) for k,space in m.observation_space.spaces.items()}
  target=torch.zeros(n,device='cuda');mask=torch.zeros(n,device='cuda',dtype=torch.bool);starts=torch.zeros(n,device='cuda')
  for j,seq in enumerate(rows):
   starts[j*width]=1
   for k,row in enumerate(seq):
    pos=j*width+k;obs['context_id'][pos]=row['context'];target[pos]=row['target'];mask[pos]=True
  batches[update]=(obs,target,mask,starts)
 def forward(update):
  obs,target,mask,starts=batches[update];zero=lambda:torch.zeros((1,128,256),device='cuda')
  return m.policy.predict_values(obs,(zero(),zero()),starts).ravel()[mask],target[mask]
 def score(update):
  with torch.no_grad():
   pred,target=forward(update);error=target-pred;var=target.var(unbiased=False)
   return dict(n=len(target),mse=float(error.square().mean()),bias=float(-error.mean()),ev=float(1-error.var(unbiased=False)/var))
 opt=torch.optim.Adam(m.policy.parameters(),lr=1e-4);history=[]
 for step in range(31):
  if step in [0,5,10,20,30]:
   row=dict(step=step,train=score(50),future=score(60));history.append(row);print(name,json.dumps(row),flush=True)
  if step==30:break
  opt.zero_grad();pred,target=forward(50);loss=.5*(pred-target).square().mean();loss.backward()
  assert all(torch.isfinite(p.grad).all() for p in m.policy.parameters() if p.grad is not None)
  torch.nn.utils.clip_grad_norm_(m.policy.parameters(),.5);opt.step()
 results[name]=history;del m,opt;gc.collect();torch.cuda.empty_cache()
assert sha(initial)==initial_sha
report=dict(initial_sha=initial_sha,sources=sources,results=results,encoding_seconds=times,scope='Actual initial frozen-encoder recurrent scalar V/attention architecture; full saved episode sequences, 30 critic-only full-batch Adam steps at 1e-4, identical initial buffers. No PPO actor objective or environment sampling. TRAIN50 fit/TRAIN60 future diagnostic, not fixed/task-disjoint validation or policy-improvement proof. No production weights/cache changed.')
out.write_text(json.dumps(report,indent=2));print(str(out),flush=True)
