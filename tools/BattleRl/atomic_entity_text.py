"""Frozen pretrained rule text, explicit public entity roles/attributes and attention."""
import hashlib
import os
from pathlib import Path
import gymnasium as gym
import numpy as np
import torch
from torch import nn
from stable_baselines3.common.torch_layers import BaseFeaturesExtractor
from atomic_battle_env import BattleEnv as AtomicEnv
from atomic_battle_policy import AtomicBattlePolicy
from atomic_frozen_text import assets
from public_entity_tokens import KINDS

ENTITY_CAPACITY=256
ATTRIBUTE_WIDTH=54  # original six public slot pointers plus48 public numeric fields

def extend_descriptors(actions, ordinary):
    from task_feasibility import digest
    meta,_,vectors,_=assets(os.environ['BATTLE_RL_TEXT_ASSETS'])
    import json
    keys=hashlib.sha256(json.dumps([a['Key'] for a in actions],separators=(',',':')).encode()).hexdigest()
    if keys!=meta['action_keys_sha256']:raise ValueError('Text action catalog binding changed')
    # Replace the old character-bigram branch with actual pretrained text.
    return np.concatenate((ordinary,vectors),axis=1),dict(frozen_mmbert=meta,
        numeric_action_width=ordinary.shape[1],representation='numeric arguments plus frozen pretrained operation/mode/card/material text')

class BattleEnv(AtomicEnv):
    def __init__(self,*args,**kwargs):
        self.text_meta,self.entity_vectors,_,self.text_layout=assets(os.environ['BATTLE_RL_TEXT_ASSETS'])
        super().__init__(*args,**kwargs)
        spaces=self.observation_space.spaces
        spaces['entity_ids']=gym.spaces.Box(0,len(self.text_layout.ids)-1,(ENTITY_CAPACITY,),np.int16)
        spaces['entity_roles']=gym.spaces.Box(0,len(KINDS)-1,(ENTITY_CAPACITY,),np.uint8)
        spaces['entity_attributes']=gym.spaces.Box(-np.inf,np.inf,(ENTITY_CAPACITY,ATTRIBUTE_WIDTH),np.float32)
        spaces['entity_present']=gym.spaces.Box(0,1,(ENTITY_CAPACITY,),np.uint8)

    def _observation(self):
        obs=super()._observation()
        tokens,entries=self.text_layout._one(np.asarray(self.current['features'],np.float64))
        count=len(entries)
        if count>ENTITY_CAPACITY:raise ValueError('Public entity capacity exceeded; no truncation allowed')
        ids=np.zeros(ENTITY_CAPACITY,np.int16);roles=np.zeros(ENTITY_CAPACITY,np.uint8)
        attributes=np.zeros((ENTITY_CAPACITY,ATTRIBUTE_WIDTH),np.float32);present=np.zeros(ENTITY_CAPACITY,np.uint8)
        for i,(identity,kind,_) in enumerate(entries):
            ids[i]=self.text_layout.id_index[identity];roles[i]=KINDS.index(kind)
        # The verified producer already encodes count, armor, attack, readiness,
        # abilities and real slot pointers at documented public coordinates.
        attributes[:count]=tokens[:,len(self.text_layout.ids)+len(KINDS):]
        present[:count]=1
        obs.update(entity_ids=ids,entity_roles=roles,entity_attributes=attributes,entity_present=present)
        return obs

class EntityTextEncoder(BaseFeaturesExtractor):
    def __init__(self,space,entity_vectors):
        super().__init__(space,features_dim=128)
        self.register_buffer('frozen_text',torch.as_tensor(np.array(entity_vectors,copy=True)),persistent=False)
        self.text_projection=nn.Linear(384,128)
        self.attribute_projection=nn.Linear(ATTRIBUTE_WIDTH,128)
        self.role_embedding=nn.Embedding(len(KINDS),128)
        layer=nn.TransformerEncoderLayer(128,4,dim_feedforward=256,dropout=0.,
            activation='gelu',batch_first=True,norm_first=True)
        self.entity_attention=nn.TransformerEncoder(layer,2,enable_nested_tensor=False)
        self.token_norm=nn.LayerNorm(128)
        width=space['state'].shape[0]
        # All original public numeric fields and action/reward history remain;
        # the verified entity projection supplements them, never drops obligations.
        self.numeric=nn.Sequential(nn.Linear(width,256),nn.Tanh(),nn.Linear(256,128),nn.Tanh())
        self.fusion=nn.Sequential(nn.Linear(256,128),nn.Tanh())

    def forward(self,obs):
        present=obs['entity_present'].bool().clone()
        empty=~present.any(dim=1);present[empty,0]=True  # upstream padding only
        width=max(1,int(present.sum(dim=1).max().item()))
        positions=torch.argsort(present.to(torch.int8),dim=1,descending=True,stable=True)[:,:width]
        present=present.gather(1,positions)
        ids=obs['entity_ids'].long().gather(1,positions)
        roles=obs['entity_roles'].long().gather(1,positions)
        attributes=obs['entity_attributes'].float().gather(1,positions[...,None].expand(-1,-1,ATTRIBUTE_WIDTH))
        tokens=self.text_projection(self.frozen_text[ids])+self.attribute_projection(attributes)+self.role_embedding(roles)
        encoded=self.entity_attention(self.token_norm(tokens),src_key_padding_mask=~present)
        context=(encoded*present[...,None]).sum(dim=1)/present.sum(dim=1,keepdim=True)
        return self.fusion(torch.cat((self.numeric(obs['state'].float()),context),dim=1))

class ActionTextEncoder(nn.Module):
    def __init__(self,numeric_width):
        super().__init__();self.numeric_width=numeric_width
        self.numeric=nn.Linear(numeric_width,128,bias=False)
        self.text=nn.Linear(384,128,bias=False)
        self.combine=nn.Sequential(nn.LayerNorm(128),nn.GELU(),nn.Linear(128,32,bias=False))
    def forward(self,x):return self.combine(self.numeric(x[:,:self.numeric_width])+self.text(x[:,self.numeric_width:]))

class EntityTextPolicy(AtomicBattlePolicy):
    def __init__(self,*args,**kwargs):
        super().__init__(*args,**kwargs)
        _,vectors,_,_=assets(os.environ['BATTLE_RL_TEXT_ASSETS'])
        encoder=EntityTextEncoder(self.observation_space,vectors)
        self.features_extractor=encoder
        self.pi_features_extractor=encoder;self.vf_features_extractor=encoder
        self.argument_encoder=ActionTextEncoder(self.descriptors.shape[1]-384)
        schedule=kwargs.get('lr_schedule',args[2] if len(args)>2 else lambda _:1e-4)
        self.optimizer=self.optimizer_class(self.parameters(),lr=schedule(1),**self.optimizer_kwargs)
