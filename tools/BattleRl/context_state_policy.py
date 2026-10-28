"""Feed joint contextual option tokens into BOTH recurrent state pathways.

Warm residual is zero at migration; scalar V remains independent of sampled
action. All tokens are frozen encoder outputs from the complete root context.
"""
from collections import OrderedDict
import numpy as np
import torch
from torch import nn
import hierarchical_option_policy as h
import hierarchical_packed_cache as packed
from entropy_token_value_policy import EntropyTokenValuePolicy

class ContextStateFeatures(h.HierarchicalFeatures):
    def install_context_state(self,device):
        self.state_attention=nn.Sequential(nn.LayerNorm(384),nn.Linear(384,1,bias=False)).to(device)
        self.state_adapter=nn.Sequential(nn.LayerNorm(384),nn.Linear(384,128),nn.GELU(),nn.Linear(128,128)).to(device)
        nn.init.zeros_(self.state_attention[-1].weight)
        nn.init.zeros_(self.state_adapter[-1].weight);nn.init.zeros_(self.state_adapter[-1].bias)
        self._state_root_cache=OrderedDict()
    def forward(self,obs):
        ids=obs['context_id'].detach().cpu().numpy().reshape(-1).astype('int64')
        if not hasattr(self,'_state_root_cache'):self._state_root_cache=OrderedDict()
        cache=self._state_root_cache;requests=[]
        for i in dict.fromkeys(map(int,ids)):
            if (self.context_path,i) not in cache and i:requests.append((i,h.tree(self.context_path,i)[0].branch(0)))
        packed.prefetch(self.context_path,requests)
        for i,node in requests:cache[(self.context_path,i)]=h.node_vectors(self.context_path,i,node)
        arrays=[];bos=[]
        for i in map(int,ids):
            if i:
                key=(self.context_path,i);v=cache[key];cache.move_to_end(key)
            else:v=np.zeros((2,384),np.float32)
            bos.append(v[0]);arrays.append(v[1:])
        while len(cache)>2048:cache.popitem(last=False)
        width=max(map(len,arrays));x=np.zeros((len(arrays),width,384),np.float32);valid=np.zeros((len(arrays),width),bool)
        for j,a in enumerate(arrays):x[j,:len(a)]=a;valid[j,:len(a)]=True
        device=obs['context_id'].device;tokens=torch.as_tensor(x,device=device);mask=torch.as_tensor(valid,device=device)
        weights=self.state_attention(tokens).squeeze(-1).masked_fill(~mask,-torch.inf).softmax(-1)
        contextual=(weights[...,None]*tokens).sum(1)
        return self.projection(torch.as_tensor(np.stack(bos),device=device))+self.state_adapter(contextual)

class ContextStatePolicy(EntropyTokenValuePolicy):
    def __init__(self,*args,**kwargs):
        super().__init__(*args,**kwargs);self.install_context_state()
    def install_context_state(self):
        old=self.optimizer;features=self.features_extractor
        assert self.pi_features_extractor is features and self.vf_features_extractor is features
        features.__class__=ContextStateFeatures;features.install_context_state(self.device)
        self.optimizer=self.optimizer_class(self.parameters(),lr=old.param_groups[0]['lr'],**self.optimizer_kwargs)
        for p,state in old.state.items():self.optimizer.state[p]=state
