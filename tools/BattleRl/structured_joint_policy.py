"""Undeployed structured/entity policy plus contextual hierarchical residual.

The complete green state and action encoders are retained. Zero marker residual
recovers their leaf distribution; no candidates or numeric parameters are lost.
"""
import os
from pathlib import Path
import numpy as np
import gymnasium as gym
import torch
from torch import nn
from atomic_entity_text import BattleEnv as EntityEnv,EntityTextPolicy
from joint_option_policy import store
from structured_option_text import ReferencedOptionRenderer
from atomic_action_registry import registry
import hierarchical_option_policy as h
from structured_hierarchy_core import StructuredHierarchy

class BattleEnv(EntityEnv):
    def __init__(self,*args,**kwargs):
        super().__init__(*args,**kwargs)
        self.context_store=store(os.environ['BATTLE_RL_CONTEXT_STORE'])
        self.renderer=ReferencedOptionRenderer(self.rules.info['featureNames'],os.environ['BATTLE_RL_TEXT_ASSETS'])
        self._context_cache=None
        self.observation_space.spaces['context_id']=gym.spaces.Box(0,2**24-1,(1,),np.float32)

    def _observation(self):
        obs=super()._observation()
        if not hasattr(self,'renderer') or self.closed_episode or not self.choices or self.support_id==0:
            obs['context_id']=np.zeros(1,np.float32);return obs
        key=(id(self.current),self.support_id,id(self.previous))
        if self._context_cache is None or self._context_cache[0]!=key:
            doc=self.renderer.render(self.current['features'],self.previous,self.choices)
            doc.update(support_id=self.support_id,sequences=[[self.key_to_id[a['Key']] for a in seq] for seq in self.choices],operations=self.choices)
            self._context_cache=(key,self.context_store.add(doc),self.current,self.previous)
        obs['context_id']=np.array([self._context_cache[1]],np.float32)
        return obs

class StructuredJointDistribution(h.HierarchicalDistribution):
    def __init__(self,policy,latent,contexts,flat):
        super().__init__(policy,latent,contexts);self.flat=flat;self.cores={}

    def categorical(self,row,node):
        if row not in self.cores:
            tree,doc=h.tree(self.policy.context_path,int(self.contexts[row]))
            support=registry(self.policy.registry_file).get(int(self.policy._supports[row]))
            if doc['sequences']!=support or tree.leaves!=len(support):
                raise ValueError('Structured scores and contextual candidates differ')
            def residual(n):
                vectors=h.node_vectors(self.policy.context_path,int(self.contexts[row]),n)[1:]
                options=self.policy.marker_options(torch.as_tensor(vectors,device=self.latent.device))
                query=self.policy.marker_query(self.latent[row:row+1])
                return (query*options).sum(-1)/(options.shape[-1]**.5)
            self.cores[row]=StructuredHierarchy(tree,self.flat.distribution.logits[row,:len(support)],residual)
        return self.cores[row].categorical(node)

class StructuredJointPolicy(EntityTextPolicy):
    def __init__(self,*args,context_path=None,**kwargs):
        context_path=context_path or os.environ['BATTLE_RL_CONTEXT_STORE']
        self.context_path=str(Path(context_path))
        super().__init__(*args,**kwargs)
        self.install_marker_residual()

    def install_marker_residual(self):
        if hasattr(self,'marker_query'):raise ValueError('Marker residual already installed')
        old=self.optimizer
        self.marker_options=nn.Sequential(nn.LayerNorm(384),nn.Linear(384,128),nn.GELU()).to(self.device)
        self.marker_query=nn.Linear(128,128).to(self.device)
        nn.init.zeros_(self.marker_query.weight);nn.init.zeros_(self.marker_query.bias)
        self.optimizer=self.optimizer_class(self.parameters(),lr=old.param_groups[0]['lr'],**self.optimizer_kwargs)
        for p,state in old.state.items():self.optimizer.state[p]=state

    def _bind_mask(self,obs,padded=False):
        super()._bind_mask(obs,padded)
        self._contexts=obs['context_id'].detach().cpu().numpy().reshape(-1).astype('int64')

    def _get_action_dist_from_latent(self,latent_pi):
        flat=super()._get_action_dist_from_latent(latent_pi)
        return StructuredJointDistribution(self,latent_pi,self._contexts,flat)

    def evaluate_actions(self,obs,actions,*args,**kwargs):
        values,logp,entropy=super().evaluate_actions(obs,actions,*args,**kwargs)
        reference=getattr(self,'_entropy_reference',None)
        if reference is not None:
            selected=actions.detach().cpu().numpy().reshape(-1).astype('int64')
            old=[reference[(int(c),int(a))] if c else 0. for c,a in zip(self._contexts,selected)]
            old=torch.tensor(old,device=logp.device,dtype=logp.dtype)
            # Importance ratio remains differentiable for the full entropy gradient.
            entropy=-(logp-old).exp()*logp
        return values,logp,entropy
