"""Matched offline text-readout optimization controls; not production PPO."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
os.environ['OMP_NUM_THREADS']='1'
import json,time
from pathlib import Path
import numpy as np
import torch
from torch import nn
torch.set_num_threads(1)
root=Path('optimization_stage_20261014');d=np.load(root/'probe_samples.npz')
x=torch.tensor(d['option']);y=torch.tensor(d['target']);held=torch.tensor(d['test']).bool();train=~held
ln=torch.nn.functional.layer_norm(x,(384,));mean=ln[train].mean(0);std=ln[train].std(0,unbiased=False)
variants={'layernorm':ln,'center_global':(ln-mean)/std.square().mean().sqrt().clamp_min(.0001),'center_coordinate':((ln-mean)/std.clamp_min(.01)).clamp(-10,10)}
records=[];start=time.perf_counter()
def score(pred,mask):
 error=pred[mask]-y[mask]
 return dict(ev=float(1-error.var(unbiased=False)/y[mask].var(unbiased=False)),mse=float(error.square().mean()),bias=float(error.mean()))
indices=train.nonzero().flatten()
for seed in [711,712]:
 for rate in [.0001,.001]:
  for name,z in variants.items():
   torch.manual_seed(seed);net=nn.Sequential(nn.Linear(384,128),nn.Tanh(),nn.Linear(128,1))
   opt=torch.optim.Adam(net.parameters(),lr=rate);rng=torch.Generator().manual_seed(seed+1);history=[]
   for epoch in range(41):
    if epoch in [0,4,12,40]:
     with torch.no_grad():
      pred=net(z).ravel();history.append(dict(epoch=epoch,train=score(pred,train),heldout=score(pred,held)))
    if epoch==40:break
    for batch in indices[torch.randperm(len(indices),generator=rng)].split(128):
     loss=(net(z[batch]).ravel()-y[batch]).square().mean();opt.zero_grad();loss.backward();nn.utils.clip_grad_norm_(net.parameters(),.5);opt.step()
   records.append(dict(seed=seed,learning_rate=rate,normalization=name,history=history))
   print(seed,rate,name,history[-1],flush=True)
report=dict(seconds=time.perf_counter()-start,records=records,scope='Same 1920 archived training states and episode-held-out split as information probe. Frozen option-mean features; 384-128-1 tanh head only, no LSTM or PPO. Train-only normalization stats, identical initialized head and batch order within each seed/rate. All settings reported, no production mutation or validation-task training.')
(root/'readout_conditioning_probe.json').write_text(json.dumps(report,indent=2))
