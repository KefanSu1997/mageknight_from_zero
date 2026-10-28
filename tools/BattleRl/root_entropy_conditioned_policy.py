"""Candidate only: exact first-branch entropy plus importance-sampled tail.

No policy/value parameter changes. Root encoding is already on the selected path.
This changes the entropy estimator, not the joint entropy objective or coefficient.
"""
import torch
import hierarchical_option_policy as h
from conditioned_context_policy import ConditionedContextPolicy

class RootEntropyConditionedPolicy(ConditionedContextPolicy):
    def _get_action_dist_from_latent(self,latent_pi):
        distribution=super()._get_action_dist_from_latent(latent_pi)
        if getattr(self,'_capture_entropy_distribution',False):
            self._root_entropy_distribution=distribution
        return distribution

    def evaluate_actions(self,obs,actions,*args,**kwargs):
        reference=getattr(self,'_entropy_reference',None)
        if reference is None:return super().evaluate_actions(obs,actions,*args,**kwargs)
        self._capture_entropy_distribution=True
        try:
            values,logprob,_=super().evaluate_actions(obs,actions,*args,**kwargs)
            dist=self._root_entropy_distribution
            selected=actions.detach().cpu().numpy().reshape(-1)
            roots=[];root_logs=[];old=[]
            for row,(ctx,action) in enumerate(zip(dist.contexts,selected)):
                ctx=int(ctx);action=int(action);zero=logprob[row]*0
                if not ctx:
                    roots.append(zero);root_logs.append(zero);old.append(0.);continue
                old.append(reference[(ctx,action)])
                tree,_=h.tree(self.context_path,ctx);node=tree.branch(0)
                if tree.nodes[node]['leaf'] is not None:
                    roots.append(zero);root_logs.append(zero);continue
                categorical=dist.categorical(row,node)
                edge=next(edge for parent,edge in tree.paths[action] if parent==node)
                index=list(tree.nodes[node]['children']).index(edge)
                roots.append(categorical.entropy())
                root_logs.append(categorical.log_prob(torch.tensor(index,device=logprob.device)))
            old=torch.tensor(old,dtype=logprob.dtype,device=logprob.device)
            # E_old[ratio * (log p(root)-log p(leaf))] is conditional tail entropy.
            entropy=torch.stack(roots)+(logprob-old).exp()*(torch.stack(root_logs)-logprob)
            return values,logprob,entropy
        finally:
            self._capture_entropy_distribution=False
            self.__dict__.pop('_root_entropy_distribution',None)
