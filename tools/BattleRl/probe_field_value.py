"""Preserve contextual field positions instead of collapsing state to one vector."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
import sys,json,time
from pathlib import Path
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
import numpy as np,torch,psutil
from semantic_public_text import SemanticPublicText,public_features
from hierarchical_batch_encoder import BatchedReader
from hierarchical_option_policy import ChoiceTree
from joint_option_text import encode_document
torch.set_num_threads(1);out=Path('optimization_stage_20261014');d=np.load(out/'probe_samples.npz');idx=np.load(out/'semantic_vectors.npz')['indices'];ids=d['context'][idx]
docs={r['context']:r['document'] for r in map(json.loads,(out/'probe_docs.jsonl').read_text().splitlines())};sem=SemanticPublicText();paused=[];vectors=[]
try:
 p=psutil.Process(24446);assert 'deploy_token_value.py' in ' '.join(p.cmdline())
 for q in [p]+p.children(recursive=True):paused.append((q.pid,q.create_time()));q.suspend()
 reader=BatchedReader()
 for off in range(0,len(ids),4):
  batch=[];spans=[]
  for ctx in ids[off:off+4]:
   doc=docs[int(ctx)];tree=ChoiceTree(doc['operations']);doc=sem.document(tree.document(doc,tree.branch(0)))
   if not doc['options']:doc['options']=['唯一完整操作，直接执行']
   text=doc['state'];lines=sem.summary(public_features(text)).splitlines();ranges=[];cursor=0
   for line in lines:
    begin=text.index(line,cursor);end=begin+len(line);ranges.append((begin,end));cursor=end
   prefix=text+'\n原始规则：\n'+doc['rules']+'\n问题：'+doc['question']+'\n完整候选：\n'
   offsets=reader.tok(prefix,add_special_tokens=False,return_offsets_mapping=True)['offset_mapping']
   fields=[[i+1 for i,(a,b) in enumerate(offsets) if b>start and a<end] for start,end in ranges]
   assert all(fields) and len(fields)>=15
   fields=fields[:15]+[[j for field in fields[15:] for j in field] or fields[0]]
   batch.append(doc);spans.append(fields)
  captured=[]
  hook=reader.model.register_forward_hook(lambda m,a,r:captured.append(r.last_hidden_state.detach()))
  reader.encode_many(batch);hook.remove();assert len(captured)==1
  order=sorted(range(len(batch)),key=lambda i:len(encode_document(reader.tok,batch[i],8192)[0]));rows=[None]*len(batch)
  for j,i in enumerate(order):rows[i]=torch.stack([captured[0][j,f].float().mean(0) for f in spans[i]]).cpu().numpy()
  vectors.extend(rows)
 np.savez(out/'field_vectors.npz',vectors=np.stack(vectors),indices=idx)
finally:
 for pid,created in reversed(paused):
  try:
   p=psutil.Process(pid)
   if p.create_time()==created:p.resume()
  except psutil.NoSuchProcess:pass
y=torch.tensor(d['target'][idx]);test=torch.tensor(d['test'][idx]);train=~test;x=torch.tensor(np.stack(vectors));results=[]
for centered in [False,True]:
 z=x-x[train].mean(0) if centered else x
 torch.manual_seed(711)
 net=torch.nn.Sequential(torch.nn.LayerNorm(384),torch.nn.Linear(384,16),torch.nn.Tanh(),torch.nn.Flatten(),torch.nn.Linear(256,64),torch.nn.Tanh(),torch.nn.Dropout(.2),torch.nn.Linear(64,1))
 opt=torch.optim.AdamW(net.parameters(),lr=.001,weight_decay=.1);history=[]
 for step in range(101):
  if step in [20,50,100]:
   net.eval()
   with torch.no_grad():
    pred=net(z).ravel();history.append(dict(step=step,train_ev=float(1-(y[train]-pred[train]).var(unbiased=False)/y[train].var(unbiased=False)),heldout_ev=float(1-(y[test]-pred[test]).var(unbiased=False)/y[test].var(unbiased=False))))
   net.train()
  loss=(net(z[train]).ravel()-y[train]).square().mean();opt.zero_grad();loss.backward();opt.step()
 results.append(dict(centered=centered,history=history))
(out/'field_value_report.json').write_text(json.dumps(results,indent=2));print(json.dumps(results),flush=True)
