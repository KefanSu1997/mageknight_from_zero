"""Full green candidate prior with shared contextual option residuals."""
import os
from pathlib import Path
import numpy as np
import torch
from torch import nn
from atomic_entity_text import EntityTextPolicy
from final_token_policy import BattleEnv, Distribution as PathDistribution
from candidate_option_runtime import runtime
from structured_hierarchy_core import StructuredHierarchy
from atomic_action_registry import registry

class CandidateDistribution(PathDistribution):
    def __init__(self,policy,latent,flat):
        self.policy=policy;self.latent=latent;self.flat=flat
        self.contexts=policy._contexts.copy();self.runtime=runtime(policy.context_path)
        self.memo={};self.cores={}
    def distributions(self,rows,nodes):
        requests=[(int(self.contexts[r]),n) for r,n in zip(rows,nodes)]
        vectors=self.runtime.vectors(requests);scores=[]
        for row,node,vec in zip(rows,nodes,vectors):
            tree,doc=self.runtime.tree(int(self.contexts[row]))
            if row not in self.cores:
                support=registry(self.policy.registry_file).get(int(self.policy._supports[row]))
                assert doc['sequences']==support and tree.leaves==len(support)
                self.cores[row]=StructuredHierarchy(tree,self.flat.distribution.logits[row,:tree.leaves])
            prior=self.cores[row].categorical(node).logits
            text=torch.as_tensor(vec[1:],device=self.latent.device)
            assert len(text)==len(prior)
            extra=self.policy.option_residual(text,self.latent[row])
            scores.append(prior+extra)
        width=max(map(len,scores))
        logits=torch.stack([torch.nn.functional.pad(x,(0,width-len(x)),value=-torch.inf) for x in scores])
        return torch.distributions.Categorical(logits=logits)
    def log_prob(self,actions):
        # Parent memo assumes fixed-width rows. Recompute ragged routes together.
        self.memo.clear()
        return super().log_prob(actions)

class CandidateOptionPolicy(EntityTextPolicy):
    def __init__(self,*args,context_path=None,candidate_arm=None,**kwargs):
        self.context_path=str(Path(context_path or os.environ['BATTLE_RL_CONTEXT_STORE']).resolve())
        self.candidate_arm=candidate_arm or os.environ['BATTLE_RL_CANDIDATE_ARM']
        super().__init__(*args,**kwargs)
        self.option_text=nn.Sequential(nn.LayerNorm(384),nn.Linear(384,128),nn.GELU())
        self.option_state=nn.Sequential(nn.LayerNorm(128),nn.Linear(128,128),nn.GELU())
        if self.candidate_arm=='dot':
            self.option_out=nn.Linear(128,128,bias=False)
        elif self.candidate_arm=='interaction':
            self.option_out=nn.Sequential(nn.Linear(512,128),nn.GELU(),nn.Linear(128,1))
        else:raise ValueError(self.candidate_arm)
        # Small nonzero residual allows all text-path layers to learn immediately.
        last=self.option_out if self.candidate_arm=='dot' else self.option_out[-1]
        nn.init.normal_(last.weight,std=.001)
        if last.bias is not None:nn.init.zeros_(last.bias)
        self.optimizer=self.optimizer_class(self.parameters(),lr=self.optimizer.param_groups[0]['lr'],**self.optimizer_kwargs)
    def _get_constructor_parameters(self):
        data=super()._get_constructor_parameters()
        data.update(context_path=self.context_path,candidate_arm=self.candidate_arm)
        return data
    def option_residual(self,text,latent):
        v=self.option_text(text);q=self.option_state(latent).expand_as(v)
        if self.candidate_arm=='dot':return (self.option_out(q)*v).sum(-1)/(128**.5)
        return self.option_out(torch.cat((q,v,q*v,q-v),-1)).squeeze(-1)
    def _bind_mask(self,obs,padded=False):
        super()._bind_mask(obs,padded)
        self._contexts=obs['context_id'].detach().cpu().numpy().reshape(-1).astype('int64')
    def _get_action_dist_from_latent(self,latent_pi):
        return CandidateDistribution(self,latent_pi,super()._get_action_dist_from_latent(latent_pi))
    def evaluate_actions(self,obs,actions,*args,**kwargs):
        values,logp,entropy=super().evaluate_actions(obs,actions,*args,**kwargs)
        reference=getattr(self,'_entropy_reference',None)
        if reference is not None:
            selected=actions.detach().cpu().numpy().reshape(-1).astype('int64')
            old=torch.tensor([reference[(int(c),int(a))] if c else 0. for c,a in zip(self._contexts,selected)],device=logp.device,dtype=logp.dtype)
            entropy=-(logp-old).exp()*logp
        return values,logp,entropy
