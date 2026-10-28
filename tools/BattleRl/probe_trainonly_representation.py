import os
os.environ['OPENBLAS_NUM_THREADS']='1';os.environ['OMP_NUM_THREADS']='1'
import sys,json,re,sqlite3,hashlib,gc
from pathlib import Path
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
import numpy as np
from rollout_archive import load
from hierarchical_option_policy import ChoiceTree
root=Path('remote_repaired_trainonly300');wanted={};sources=[]
archives=sorted(root.glob('rollout_*.archive'))[:8];assert len(archives)==8
for group,path in enumerate(archives):
 b=load(path);ids=b.observations['context_id'].ravel();y=b.returns.ravel();ix=np.linspace(0,len(ids)-1,128,dtype=int)
 for i in ix:wanted[int(ids[i])]=(group,float(y[i]))
 sources.append(str(path));del b;gc.collect()
db=sqlite3.connect('file:'+str(root/'contexts/packed_vectors/offsets.sqlite')+'?mode=ro',uri=True);stream=(root/'contexts/packed_vectors/vectors.bin').open('rb');numeric=[];text=[];target=[];groups=[]
with (root/'contexts/contexts.jsonl').open(encoding='utf-8') as f:
 for ctx,line in enumerate(f,1):
  if ctx not in wanted:continue
  doc=json.loads(line);tree=ChoiceTree(doc['operations']);node=tree.branch(0)
  meta=json.loads(db.execute('SELECT metadata FROM entries WHERE context=? AND node=?',(ctx,node)).fetchone()[0]);stream.seek(meta['offset']);payload=stream.read(meta['bytes']);assert hashlib.sha256(payload).hexdigest()==meta['sha256']
  v=np.frombuffer(payload,dtype='<f4').reshape(meta['shape'])[1:].mean(0);text.append(v)
  x=np.zeros(6314);pairs=re.findall(r'state\.(\d+)=([-+\d.eE]+)',doc['state']);assert pairs
  for k,val in pairs:x[int(k)]=float(val)
  numeric.append(x);g,y=wanted[ctx];groups.append(g);target.append(y)
stream.close();db.close();groups=np.array(groups);target=np.array(target);fit=groups<6;held=~fit;assert fit.sum()==768 and held.sum()==256
reports=[]
def score(pred,mask):
 err=pred-target[mask];var=target[mask].var();return dict(ev=float(1-err.var()/var),mse=float(np.mean(err**2)),bias=float(err.mean()))
for name,rows in [('text_option_mean',text),('numeric_positive_control',numeric)]:
 x=np.asarray(rows,dtype=np.float64)
 if name.startswith('text'):
  x=(x-x.mean(1,keepdims=True))/np.sqrt(x.var(1,keepdims=True)+1e-5)
 center=x[fit].mean(0);scale=x[fit].std(0);keep=scale>1e-6;x=(x[:,keep]-center[keep])/np.maximum(scale[keep],.01)
 a=x[fit];z=x[held];width=a.shape[1];kernel=a@a.T/width;cross=z@a.T/width;ym=target[fit].mean();yc=target[fit]-ym
 for alpha in [.001,.01,.1,1.]:
  w=np.linalg.solve(kernel+alpha*np.eye(len(a)),yc)
  reports.append(dict(input=name,alpha=alpha,dimensions=width,train=score(kernel@w+ym,fit),later_training_batches=score(cross@w+ym,held)))
report=dict(sources=sources,fit_batches=[1,6],heldout_batches=[7,8],samples=len(target),fit_samples=int(fit.sum()),heldout_samples=int(held.sum()),results=reports,scope='Read-only cached frozen option means versus public numeric positive control, identical sampled states and return targets. Ridge coefficients fitted only on training batches1-6; later batches7-8 not task-disjoint or fixed validation, four prespecified penalties all reported, no production model change. Linear probe does not bound nonlinear model capacity.')
Path('optimization_stage_20261014/trainonly_representation_probe.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
