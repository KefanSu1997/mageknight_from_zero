"""Independent scalar V from pre-action contextual option tokens, not Q or numeric state."""
import numpy as np
import torch
from torch import nn
import hierarchical_option_policy as h
class TokenValuePolicy(h.HierarchicalOptionPolicy):
    def __init__(self,*args,**kwargs):
        super().__init__(*args,**kwargs);self.install_token_value()
    def install_token_value(self):
        old=self.optimizer
        self.value_attention=nn.Sequential(nn.LayerNorm(384),nn.Linear(384,1,bias=False)).to(self.device)
        self.token_value=nn.Sequential(nn.LayerNorm(384),nn.Linear(384,128),nn.Tanh(),nn.Linear(128,1)).to(self.device)
        nn.init.zeros_(self.value_attention[-1].weight)
        nn.init.zeros_(self.token_value[-1].weight);nn.init.zeros_(self.token_value[-1].bias)
        self.optimizer=self.optimizer_class(self.parameters(),lr=old.param_groups[0]['lr'],**self.optimizer_kwargs)
        for p,state in old.state.items():self.optimizer.state[p]=state
    def contextual_value(self,obs):
        values=[]
        for i in obs['context_id'].detach().cpu().numpy().reshape(-1).astype('int64'):
            if i:
                t,_=h.tree(self.context_path,int(i));v=h.node_vectors(self.context_path,int(i),t.branch(0))[1:]
            else:v=np.zeros((1,384),np.float32)
            tokens=torch.as_tensor(v,device=self.device)
            weights=self.value_attention(tokens).softmax(dim=0)
            values.append(self.token_value((weights*tokens).sum(0,keepdim=True)))
        return torch.cat(values,dim=0)
    def forward(self,obs,*args,**kwargs):
        a,v,l,s=super().forward(obs,*args,**kwargs)
        return a,v+self.contextual_value(obs),l,s
    def evaluate_actions(self,obs,*args,**kwargs):
        v,l,e=super().evaluate_actions(obs,*args,**kwargs)
        return v+self.contextual_value(obs),l,e
    def predict_values(self,obs,*args,**kwargs):
        return super().predict_values(obs,*args,**kwargs)+self.contextual_value(obs)
