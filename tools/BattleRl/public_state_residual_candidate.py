"""Undeployed candidate: public numeric state accompanies contextual options.

Keeps the frozen encoder, full irreversible choice tree and scalar V. Both
recurrent pathways see the same additive state features. Zero final projection
preserves the previous policy/value function at migration; new Adam slots start
empty and existing parameter objects/state remain intact.
"""
import torch
from torch import nn
from context_state_policy import ContextStateFeatures
from root_entropy_conditioned_policy import RootEntropyConditionedPolicy

class PublicStateResidualFeatures(ContextStateFeatures):
    def forward(self,obs):
        text=super().forward(obs)
        public=obs['state'].to(dtype=text.dtype)
        if public.shape[-1]!=self.public_state_width:
            raise ValueError('Public state/history width changed')
        return text+self.public_state_residual(public)

class PublicStateResidualPolicy(RootEntropyConditionedPolicy):
    def __init__(self,*args,**kwargs):
        super().__init__(*args,**kwargs)
        self.install_public_state_residual()

    def install_public_state_residual(self):
        features=self.features_extractor
        assert len(self.optimizer.param_groups)==1,'Explicit migration required for multiple optimizer groups'
        assert self.pi_features_extractor is features and self.vf_features_extractor is features
        assert not hasattr(features,'public_state_residual'),'Migration already applied'
        width=int(self.observation_space.spaces['state'].shape[0])
        assert width>=6314,'Expected original public observation and history'
        features.__class__=PublicStateResidualFeatures
        features.public_state_width=width
        features.public_state_residual=nn.Sequential(nn.Linear(width,128),nn.Tanh(),nn.Linear(128,128)).to(self.device)
        nn.init.zeros_(features.public_state_residual[-1].weight)
        nn.init.zeros_(features.public_state_residual[-1].bias)
        old=self.optimizer
        self.optimizer=self.optimizer_class(self.parameters(),lr=old.param_groups[0]['lr'],**self.optimizer_kwargs)
        for parameter,state in old.state.items():self.optimizer.state[parameter]=state
        assert all(self.optimizer.state[p] is s for p,s in old.state.items())
