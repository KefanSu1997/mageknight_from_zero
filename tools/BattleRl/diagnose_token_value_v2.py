"""Read-only archived-training probe; no validation tasks and no live model mutation."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
import sys,json,sqlite3,hashlib,time,gc
from pathlib import Path
sys.path.insert(0,'tools/BattleRl')
import numpy as np
import torch
from rollout_archive import load
torch.set_num_threads(1)
root=Path('remote_tokenvalue300');out=Path('optimization_stage_20261014');out.mkdir(exist_ok=True)
db=sqlite3.connect('file:'+str(root/'contexts/packed_vectors/offsets.sqlite')+'?mode=ro',uri=True)
data=(root/'contexts/packed_vectors/vectors.bin').open('rb')
paths=sorted(root.glob('rollout_*.archive'))[-6:-1]
samples=[];audits=[]
for path in paths:
 b=load(path);ids=b.observations['context_id'].ravel().astype(int);starts=np.flatnonzero(b.episode_starts.ravel());ends=np.r_[starts[1:],len(ids)]
 rewards=b.rewards.ravel().astype(float);expected=np.empty_like(rewards)
 for start,end in zip(starts,ends):expected[start:end]=np.cumsum(rewards[start:end][::-1])[::-1]
 audits.append(dict(path=str(path),steps=len(ids),episodes=len(starts),return_error=float(abs(expected-b.returns.ravel()).max())))
 for ep,(start,end) in enumerate(zip(starts,ends)):
  for index in sorted(set([int(start),int((start+end-1)//2),int(end-1)])):
   ctx=int(ids[index]);row=db.execute('SELECT metadata FROM entries WHERE context=? ORDER BY node LIMIT 1',(ctx,)).fetchone()
   assert row,ctx
   meta=json.loads(row[0]);data.seek(meta['offset']);raw=data.read(meta['bytes']);assert hashlib.sha256(raw).hexdigest()==meta['sha256']
   v=np.frombuffer(raw,dtype='<f4').reshape(meta['shape']).copy()
   samples.append((ctx,v[0],v[1:].mean(0),float(b.returns.ravel()[index]),float(b.values.ravel()[index]),str(path),ep))
 del b;gc.collect()
# Task identity is used ONLY to split diagnostic data, never as model input.
wanted={s[0] for s in samples};docs={}
with (root/'contexts/contexts.jsonl').open() as f:
 for i,line in enumerate(f,1):
  if i in wanted:docs[i]=json.loads(line)
  if len(docs)==len(wanted):break
# Group by initial public state within each episode, so related states stay together.
keys={}
for s in samples:keys.setdefault((s[5],s[6]),hashlib.sha256(docs[s[0]]['state'].encode()).hexdigest())
test=torch.tensor([int(keys[(s[5],s[6])][:8],16)%5==0 for s in samples]);train=~test
y=torch.tensor([s[3] for s in samples]);results=[]
for name,arr in [('cls',np.stack([s[1] for s in samples])),('option_mean',np.stack([s[2] for s in samples]))]:
 x=torch.tensor(arr);unit=x/x.norm(dim=1,keepdim=True);sim=unit[:256]@unit[:256].T
 for norm in ['layernorm','train_standardized']:
  z=torch.nn.functional.layer_norm(x,(384,)) if norm=='layernorm' else ((x-x[train].mean(0))/x[train].std(0).clamp_min(.01)).clamp(-10,10)
  torch.manual_seed(711);net=torch.nn.Sequential(torch.nn.Linear(384,128),torch.nn.Tanh(),torch.nn.Linear(128,1));optim=torch.optim.Adam(net.parameters(),lr=.001)
  history=[]
  for step in range(201):
   if step in [0,20,50,100,200]:
    with torch.no_grad():
     pred=net(z).ravel();record={'step':step}
     for tag,m in [('train',train),('heldout_episode',test)]:record[tag]={'ev':float(1-(y[m]-pred[m]).var(unbiased=False)/y[m].var(unbiased=False)),'mse':float((y[m]-pred[m]).square().mean())}
     history.append(record)
   loss=(net(z[train]).ravel()-y[train]).square().mean();optim.zero_grad();loss.backward();optim.step()
  results.append(dict(representation=name,normalization=norm,coordinate_std_mean=float(x.std(0).mean()),cosine_median=float(sim[torch.triu(torch.ones_like(sim),1).bool()].median()),history=history))
report=dict(audits=audits,samples=len(samples),train=int(train.sum()),heldout=int(test.sum()),results=results,scope='Diagnostic archived training states only, grouped by episode initial public-state hash; not task-disjoint or fixed validation. Existing frozen caches read only. Standardization is a probe, not deployment.')
(out/'representation_report.json').write_text(json.dumps(report,indent=2));print(json.dumps(report),flush=True)
np.savez(out/'probe_samples.npz',context=[s[0] for s in samples],cls=np.stack([s[1] for s in samples]),option=np.stack([s[2] for s in samples]),target=y.numpy(),test=test.numpy())
with (out/'probe_docs.jsonl').open('w') as f:
 for context,doc in docs.items():f.write(json.dumps(dict(context=context,document=doc))+'\n')
