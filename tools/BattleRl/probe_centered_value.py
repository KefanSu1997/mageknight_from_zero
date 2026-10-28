"""Test removal of the common embedding component, with episode holdout."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
import torch,numpy as np,json
from pathlib import Path
torch.set_num_threads(1)
p=Path('optimization_stage_20261014');d=np.load(p/'probe_samples.npz');v=np.load(p/'semantic_vectors.npz');idx=v['indices']
y=torch.tensor(d['target'][idx]);test=torch.tensor(d['test'][idx]);train=~test;result=[]
for name,a in [('old_option',d['option'][idx]),('raw',v['raw']),('semantic',v['semantic'])]:
 x=torch.tensor(a);center=x[train].mean(0)
 for mode,z in [('original',torch.nn.functional.layer_norm(x,(384,))),('centered',torch.nn.functional.layer_norm(x-center,(384,)))]:
  for decay in [.01,.1]:
   torch.manual_seed(711);net=torch.nn.Sequential(torch.nn.Linear(384,64),torch.nn.Tanh(),torch.nn.Dropout(.2),torch.nn.Linear(64,1));opt=torch.optim.AdamW(net.parameters(),lr=.001,weight_decay=decay);history=[]
   for step in range(101):
    if step in [20,50,100]:
     net.eval()
     with torch.no_grad():
      pred=net(z).ravel();history.append(dict(step=step,train_ev=float(1-(y[train]-pred[train]).var(unbiased=False)/y[train].var(unbiased=False)),heldout_ev=float(1-(y[test]-pred[test]).var(unbiased=False)/y[test].var(unbiased=False))))
     net.train()
    loss=(net(z[train]).ravel()-y[train]).square().mean();opt.zero_grad();loss.backward();opt.step()
   result.append(dict(input=name,mode=mode,decay=decay,history=history))
(p/'centered_value_report.json').write_text(json.dumps(result,indent=2));print(json.dumps(result))
