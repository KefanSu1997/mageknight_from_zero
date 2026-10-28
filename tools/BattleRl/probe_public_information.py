"""Read-only information control on the existing episode-held-out probe set.

Numeric observations are a diagnostic positive control, never a deployed critic.
No validation task data or production parameters are changed.
"""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
os.environ['OMP_NUM_THREADS']='1'
import sys,json,time
from pathlib import Path
import numpy as np
sys.path[:0]=['optimization_stage_20261014','tools/BattleRl']
from semantic_public_text import public_features

root=Path('optimization_stage_20261014');start=time.perf_counter()
d=np.load(root/'probe_samples.npz')
docs={r['context']:r['document'] for r in map(json.loads,(root/'probe_docs.jsonl').read_text().splitlines())}
x=np.stack([public_features(docs[int(i)]['state']) for i in d['context']]).astype('float64')
y=d['target'].astype('float64');test=d['test'].astype(bool);train=~test
assert train.any() and test.any() and np.var(y[test])>0
def metrics(pred,mask):
 error=y[mask]-pred[mask]
 return dict(ev=float(1-np.var(error)/np.var(y[mask])),mse=float(np.mean(error**2)),bias=float(-error.mean()))
results=[]
variants=[('phase_only',x[:,7:12],True),('public_state',x,True),('first_token',d['cls'],True),('option_mean',d['option'],True)]
for key in ['cls','option']:
 v=d[key].astype('float64');v=(v-v.mean(1,keepdims=True))/np.sqrt(v.var(1,keepdims=True)+1e-5)
 variants.append((key+'_layernorm_global_scale',v,False))
for name,features,per_coordinate in variants:
 features=np.asarray(features,dtype='float64')
 mean=features[train].mean(0)
 scale=np.maximum(features[train].std(0),.01) if per_coordinate else max(float(np.sqrt(features[train].var(0).mean())),1e-6)
 z=np.clip((features-mean)/scale,-10,10)
 active=np.std(z[train],0)>1e-10;z=z[:,active]
 # Dual ridge avoids a 6314-square matrix and gives the same linear solution.
 a=z[train];gram=a@a.T;cross=z@a.T
 center=y[train].mean();target=y[train]-center
 for alpha in [0.1,1.,10.,100.,1000.]:
  weights=np.linalg.solve(gram+alpha*np.eye(len(a)),target)
  pred=center+cross@weights
  results.append(dict(features=name,dimensions=int(active.sum()),alpha=alpha,train=metrics(pred,train),heldout=metrics(pred,test)))
 print(name,flush=True)
baseline=np.full(len(y),y[train].mean())
report=dict(samples=len(y),train=int(train.sum()),heldout=int(test.sum()),constant=metrics(baseline,test),results=results,seconds=time.perf_counter()-start,
 scope='Existing archived training data, held out by episode, NOT unseen tasks or the fixed 1000 evaluation. All predeclared alphas reported; no production change. Linear probe can detect information availability but cannot establish learned policy ability.')
(root/'public_information_probe.json').write_text(json.dumps(report,indent=2));print(json.dumps(report),flush=True)
