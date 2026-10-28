"""Same contextual scalar V, vectorized without changing model parameters."""
from collections import OrderedDict
import numpy as np
import torch
import hierarchical_option_policy as h
from token_value_policy import TokenValuePolicy

def pooled_value(attention,value,arrays,device):
    width=max(map(len,arrays));packed=np.zeros((len(arrays),width,384),np.float32)
    mask=np.zeros((len(arrays),width),bool)
    for i,a in enumerate(arrays):packed[i,:len(a)]=a;mask[i,:len(a)]=True
    tokens=torch.as_tensor(packed,device=device);valid=torch.as_tensor(mask,device=device)
    logits=attention(tokens).squeeze(-1).masked_fill(~valid,-torch.inf)
    weights=logits.softmax(-1)
    return value((weights[...,None]*tokens).sum(1))

class BatchedTokenValuePolicy(TokenValuePolicy):
    def contextual_value(self,obs):
        if not hasattr(self,'_root_value_cache'):self._root_value_cache=OrderedDict()
        cache=self._root_value_cache;arrays=[]
        for i in obs['context_id'].detach().cpu().numpy().reshape(-1).astype('int64'):
            key=(self.context_path,int(i))
            if key not in cache:
                if i:
                    t,_=h.tree(self.context_path,int(i));v=h.node_vectors(self.context_path,int(i),t.branch(0))[1:]
                else:v=np.zeros((1,384),np.float32)
                cache[key]=v
            cache.move_to_end(key);arrays.append(cache[key])
        while len(cache)>2048:cache.popitem(last=False)
        return pooled_value(self.value_attention,self.token_value,arrays,self.device)
