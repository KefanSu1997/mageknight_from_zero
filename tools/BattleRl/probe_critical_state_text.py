"""Archived TRAIN representation probe; never modifies production weights/cache."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
import sys,json,hashlib,time,argparse
from pathlib import Path
sys.path.insert(0,'tools/BattleRl')
sys.path.insert(0,'optimization_stage_20261013')
import numpy as np
import torch
from critical_public_state_text import document,decode_critical
from hierarchical_batch_encoder import BatchedReader
from hierarchical_option_policy import ChoiceTree
from joint_option_text import encode_document
torch.set_num_threads(1)
p=argparse.ArgumentParser();p.add_argument('--future-update',type=int);p.add_argument('--resources',action='store_true');args=p.parse_args()
root=Path('remote_repaired_trainonly300');out=Path('optimization_stage_20261014')/('critical_state_probe50.json' if args.future_update is None else f'critical_state_probe50_to{args.future_update}.json')
if args.resources:out=out.with_name(out.stem+'_resources.json')
assert not out.exists()
owners={};sources=[];target_by_support={};future_groups=set()
for line in (root/'episode_batches.jsonl').read_text().splitlines():
 b=json.loads(line)
 if b['update'] not in {50,args.future_update}:continue
 for e in b['episodes']:
  path=root/'episodes'/f"worker_{e['worker']}_episode_{e['episode']}.json";raw=path.read_bytes()
  owner=len(sources);sources.append(dict(path=str(path),sha256=hashlib.sha256(raw).hexdigest()))
  if b['update']==args.future_update:future_groups.add(owner)
  trace=json.loads(raw)['trace']
  for index,t in enumerate(trace):
   if index%8==0:
    sid=int(t['support_id']);owners[sid]=owner;target_by_support[sid]=sum(float(tail['training_reward']) for tail in trace[index:])
assert len(sources)==(256 if args.future_update is not None else 128)
docs=[];groups=[];labels=[];contexts=[];returns=[]
with (root/'contexts/contexts.jsonl').open() as f:
 for ctx,line in enumerate(f,1):
  d=json.loads(line);owner=owners.get(int(d['support_id']))
  if owner is None:continue
  returns.append(target_by_support[int(d['support_id'])])
  phase,enemies=decode_critical(d['state']);tree=ChoiceTree(d['operations']);d=tree.document(d,tree.branch(0))
  if not d['options']:d['options']=['唯一完整操作，直接执行']
  docs.append(d);groups.append(owner);labels.append([phase,int(any(not dead for _,dead in enemies))]);contexts.append(ctx)
reader=BatchedReader();results={};test=(np.asarray(groups)%4==0 if args.future_update is None else np.asarray([g in future_groups for g in groups]));labels=np.asarray(labels);returns=np.asarray(returns)
arms=[('raw',docs),('critical',[document(d) for d in docs])]
if args.resources:
 from resource_public_state_text import document as resource_document
 arms.append(('resources',[resource_document(d) for d in docs]))
for name,items in arms:
 start=time.perf_counter();vectors=[];lengths=[]
 for i in range(0,len(items),4):
  batch=items[i:i+4];v=reader.encode_many(batch)
  vectors.extend(np.concatenate([x[0],x[1:].mean(0)]) for x in v)
  lengths.extend(len(encode_document(reader.tok,d,8192)[0]) for d in batch)
 torch.cuda.synchronize();elapsed=time.perf_counter()-start;x=np.asarray(vectors,dtype=np.float64)
 mean=x[~test].mean(0);std=x[~test].std(0).clip(.01);x=np.clip((x-mean)/std,-10,10);x=np.column_stack([x,np.ones(len(x))])
 scores={}
 for col,key,classes in [(0,'phase',5),(1,'enemies_remaining',2)]:
  y=np.eye(classes)[labels[:,col]];a=x[~test];weights=a.T@np.linalg.solve(a@a.T+np.eye(len(a)),y[~test]);pred=(x@weights).argmax(1)
  scores[key]=dict(train_accuracy=float((pred[~test]==labels[~test,col]).mean()),heldout_episode_accuracy=float((pred[test]==labels[test,col]).mean()),heldout_count=int(test.sum()))
 return_scores=[]
 for penalty in [1.,10.,100.]:
  a=x[~test];weights=a.T@np.linalg.solve(a@a.T+np.eye(len(a))*penalty,returns[~test]);pred=x@weights
  row=dict(ridge=penalty)
  for part,mask in [('train',~test),('heldout',test),('heldout_enemies_remaining',test&(labels[:,1]==1))]:
   err=returns[mask]-pred[mask];var=float(returns[mask].var());row[part]=dict(n=int(mask.sum()),mse=float(np.mean(err**2)),bias=float(-err.mean()),target_variance=var,ev=float(1-err.var()/var) if var>1e-12 else None)
  return_scores.append(row)
 results[name]=dict(seconds=elapsed,tokens_mean=float(np.mean(lengths)),tokens_max=max(lengths),scores=scores,return_readout=return_scores)
 print(name,json.dumps(results[name]),flush=True)
assert all(p.grad is None and not p.requires_grad for p in reader.model.parameters())
report=dict(results=results,states=len(docs),contexts=contexts,groups=groups,source_files=sources,future_update=args.future_update,scope='Archived TRAIN representation and undiscounted realized-return ridge probe. Train50, holdout future batch when specified, otherwise grouped episodes. Three prespecified penalties reported, no test-driven selection. No recurrent history; not PPO, task-disjoint fixed validation, or proof of policy improvement. No production cache or weights changed.')
out.write_text(json.dumps(report,ensure_ascii=False,indent=2));print(str(out),flush=True)
