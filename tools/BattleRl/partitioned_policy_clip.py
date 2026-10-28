"""Opt-in, identity-scoped clipping for isolated upstream PPO candidate gates."""
from contextlib import contextmanager
import torch

def partition(policy):
 groups={'shared':[],'actor':[],'critic':[]}
 for name,p in policy.named_parameters():
  if name.startswith('features_extractor'):key='shared'
  elif any(x in name for x in ('lstm_critic','value_net','token_value','value_attention')):key='critic'
  else:key='actor'
  groups[key].append(p)
 identities=[id(p) for ps in groups.values() for p in ps]
 assert all(groups.values()) and len(identities)==len(set(identities))
 assert set(identities)=={id(p) for p in policy.parameters()}
 return groups

@contextmanager
def installed(policy,records):
 """Use only in a single-threaded isolated trainer; other parameter sets fallback."""
 original=torch.nn.utils.clip_grad_norm_;groups=partition(policy);expected={id(p) for p in policy.parameters()}
 def clip(parameters,max_norm,norm_type=2.0,error_if_nonfinite=False,foreach=None):
  params=list(parameters)
  if {id(p) for p in params}!=expected:
   return original(params,max_norm,norm_type,error_if_nonfinite,foreach)
  if norm_type!=2.0:raise ValueError('Candidate only validated for L2 norm')
  if len(params)!=len(expected):raise ValueError('Repeated parameters')
  norms={name:original(ps,max_norm,norm_type,True,foreach) for name,ps in groups.items()}
  records.append({name:float(value.detach()) for name,value in norms.items()})
  return torch.stack(list(norms.values())).square().sum().sqrt()
 torch.nn.utils.clip_grad_norm_=clip
 try:yield
 finally:torch.nn.utils.clip_grad_norm_=original
