"""Exact-root / sampled-tail joint entropy estimator, mathematical gate only."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
import json
from pathlib import Path
import torch
torch.set_num_threads(1)
reports=[]
for root_logits in ([0.,0.],[4.,-4.],[8.,-8.]):
 x=torch.tensor([*root_logits,1.,-1.,-.4,.4],dtype=torch.float64,requires_grad=True)
 root=x[:2].log_softmax(0);children=x[2:].reshape(2,2).log_softmax(1)
 logp=(root[:,None]+children).flatten();p=logp.exp();old=p.detach()
 exact=-(p*logp).sum();truth=torch.autograd.grad(exact,x,retain_graph=True)[0]
 root_h=-(root.exp()*root).sum();grads={'leaf_sample':[],'exact_root_sampled_tail':[]}
 for action in range(4):
  ratio=(logp[action]-old[action].log()).exp()
  terms={'leaf_sample':-ratio*logp[action], 'exact_root_sampled_tail':root_h+ratio*(-logp[action]+root[action//2])}
  for name,term in terms.items():grads[name].append(torch.autograd.grad(term,x,retain_graph=True)[0])
 result={}
 for name,rows in grads.items():
  g=torch.stack(rows);expected=(old[:,None]*g).sum(0);error=float((expected-truth).abs().max());assert error<1e-12
  variance=(old[:,None]*(g-expected).square()).sum(0)
  result[name]=dict(expected_gradient_max_error=error,total_gradient_variance=float(variance.sum()),root_gradient_variance=float(variance[:2].sum()))
 reports.append(dict(root_logits=root_logits,estimators=result))
out=dict(reports=reports,scope='Analytic two-level categorical trees only. Does not prove lower variance on every tree or training improvement. Not deployed; rollout estimator remains unchanged.')
Path('optimization_stage_20261014/root_entropy_variance_probe.json').write_text(json.dumps(out,indent=2));print(json.dumps(out))
