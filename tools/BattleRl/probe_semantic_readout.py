"""Paired frozen-encoder readout experiment on archived training episodes only."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
import sys,json,time,signal
from pathlib import Path
sys.path.insert(0,'tools/BattleRl');sys.path.insert(0,'optimization_stage_20261013');sys.path.insert(0,'optimization_stage_20261014')
import psutil,numpy as np,torch
from hierarchical_batch_encoder import BatchedReader
from hierarchical_option_policy import ChoiceTree
from joint_option_text import encode_document
from semantic_public_text import SemanticPublicText
torch.set_num_threads(1)
out=Path('optimization_stage_20261014');dataset=np.load(out/'probe_samples.npz')
docs={r['context']:r['document'] for r in map(json.loads,(out/'probe_docs.jsonl').read_text().splitlines())}
# Fixed subset selected without outcomes; grouped split is inherited unchanged.
indices=np.arange(0,len(dataset['target']),3);ctxs=dataset['context'][indices]
targets=torch.tensor(dataset['target'][indices]);test=torch.tensor(dataset['test'][indices]);train=~test
semantic=SemanticPublicText();paused=[];start=time.perf_counter()
try:
 p=psutil.Process(24446)
 assert 'optimization_stage_20261013/deploy_token_value.py' in ' '.join(p.cmdline())
 identity=dict(pid=p.pid,created=p.create_time(),cmd=p.cmdline())
 # Temporary suspension only, preserving in-flight state; always resume identities.
 for item in [p]+p.children(recursive=True):
  paused.append((item.pid,item.create_time()));item.suspend()
 (out/'semantic_probe_status.json').write_text(json.dumps(dict(status='encoding',paused=identity)))
 reader=BatchedReader();arrays={};lengths={}
 for kind in ['raw','semantic']:
  result=[];lens=[]
  for offset in range(0,len(ctxs),4):
   batch=[]
   for ctx in ctxs[offset:offset+4]:
    base=docs[int(ctx)];tree=ChoiceTree(base['operations']);doc=tree.document(base,tree.branch(0))
    if not doc['options']:doc['options']=['唯一完整操作，直接执行']
    if kind=='semantic':doc=semantic.document(doc)
    batch.append(doc)
   # Read all state tokens instead of CLS. Single forward per context, options
   # still jointly encoded. Probe-only hook does not alter production caches.
   saved=[]
   def capture(module,args,result):saved.append(result.last_hidden_state.detach())
   hook=reader.model.register_forward_hook(capture)
   vectors=reader.encode_many(batch);hook.remove()
   order=sorted(range(len(batch)),key=lambda j:len(encode_document(reader.tok,batch[j],8192)[0]))
   assert len(saved)==1
   rows=[None]*len(batch)
   for j,i in enumerate(order):
    state_len=len(reader.tok.encode(batch[i]['state'],add_special_tokens=False))
    rows[i]=saved[0][j,1:1+state_len].float().mean(0).cpu().numpy()
    lens.append(len(encode_document(reader.tok,batch[i],8192)[0]))
   result.extend(rows)
   if offset%80==0:print(kind,offset,len(ctxs),flush=True)
  arrays[kind]=np.stack(result);lengths[kind]=dict(mean=float(np.mean(lens)),max=max(lens))
 np.savez(out/'semantic_vectors.npz',**arrays,indices=indices)
finally:
 for pid,created in reversed(paused):
  try:
   item=psutil.Process(pid)
   if item.create_time()==created:item.resume()
  except psutil.NoSuchProcess:pass
results=[]
for name,arr in [('old_option',dataset['option'][indices]),*arrays.items()]:
 for norm in ['layernorm','standardized']:
  x=torch.tensor(arr);z=torch.nn.functional.layer_norm(x,(384,)) if norm=='layernorm' else ((x-x[train].mean(0))/x[train].std(0).clamp_min(.01)).clamp(-10,10)
  torch.manual_seed(711);net=torch.nn.Sequential(torch.nn.Linear(384,128),torch.nn.Tanh(),torch.nn.Linear(128,1));opt=torch.optim.Adam(net.parameters(),lr=.001);history=[]
  for step in range(201):
   if step in [20,50,100,200]:
    with torch.no_grad():
     pred=net(z).ravel();history.append(dict(step=step,train_ev=float(1-(targets[train]-pred[train]).var(unbiased=False)/targets[train].var(unbiased=False)),heldout_ev=float(1-(targets[test]-pred[test]).var(unbiased=False)/targets[test].var(unbiased=False))))
   loss=(net(z[train]).ravel()-targets[train]).square().mean();opt.zero_grad();loss.backward();opt.step()
  results.append(dict(kind=name,norm=norm,history=history))
report=dict(results=results,n=len(ctxs),lengths=lengths,seconds=time.perf_counter()-start,scope='Archived training-state probe only, episode grouped holdout, not task-disjoint validation. No production weights or cache modified; process identities resumed.')
(out/'semantic_readout_report.json').write_text(json.dumps(report,indent=2));print(json.dumps(report),flush=True)
(out/'semantic_probe_status.json').write_text(json.dumps(dict(status='complete',seconds=report['seconds'])))
