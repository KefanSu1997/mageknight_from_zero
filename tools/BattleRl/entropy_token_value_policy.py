"""Joint-action entropy with correct importance-weighted gradients under PPO data."""
import numpy as np
import torch
from batched_token_value_policy import BatchedTokenValuePolicy

class EntropyTokenValuePolicy(BatchedTokenValuePolicy):
    def evaluate_actions(self,obs,actions,*args,**kwargs):
        values,logprob,entropy=super().evaluate_actions(obs,actions,*args,**kwargs)
        reference=getattr(self,'_entropy_reference',None)
        if reference is not None:
            contexts=obs['context_id'].detach().cpu().numpy().reshape(-1).astype('int64')
            selected=actions.detach().cpu().numpy().reshape(-1).astype('int64')
            old=[]
            for context,action in zip(contexts,selected):
                if context==0:old.append(0.);continue # masked recurrent padding
                old.append(reference[(int(context),int(action))])
            old=torch.tensor(old,device=logprob.device,dtype=logprob.dtype)
            # E_old[-(pi_new/pi_old)*log(pi_new)] = H(pi_new).
            # Do not detach the ratio: both terms are needed for dH/dtheta.
            entropy=-(logprob-old).exp()*logprob
        return values,logprob,entropy

def bind_reference(model):
    b=model.rollout_buffer
    contexts=b.observations['context_id'].reshape(-1).astype('int64')
    actions=b.actions.reshape(-1).astype('int64');probs=b.log_probs.reshape(-1)
    assert len(contexts)==len(actions)==len(probs)
    rows={}
    for c,a,p in zip(contexts,actions,probs):
        if not c:continue
        key=(int(c),int(a))
        if key in rows:assert abs(rows[key]-float(p))<1e-6
        rows[key]=float(p)
    assert rows and all(np.isfinite(list(rows.values())))
    model.policy._entropy_reference=rows

def gradient_gate():
    reports=[]
    for old in ([.8,.15,.05],[.2,.3,.5]):
        x=torch.tensor([1.7,.2,-1.3],dtype=torch.float64,requires_grad=True)
        p=x.softmax(0);logp=x.log_softmax(0);exact=-(p*logp).sum();g=torch.autograd.grad(exact,x,retain_graph=True)[0]
        q=torch.tensor(old,dtype=torch.float64)
        estimated=(q*(-(logp-q.log()).exp()*logp)).sum();eg=torch.autograd.grad(estimated,x)[0]
        error=float((g-eg).abs().max());assert error<1e-12
        reports.append(dict(old=old,gradient_error=error))
    return reports
