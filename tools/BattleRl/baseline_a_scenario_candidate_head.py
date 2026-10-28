"""A's candidate dot prior and contextual option residual, with ragged supports."""
import numpy as np
import torch
from torch import nn
from atomic_entity_text import ActionTextEncoder


class ScenarioCandidateHead(nn.Module):
    def __init__(self):
        super().__init__()
        self.argument_encoder=ActionTextEncoder(96)
        self.action_net=nn.Linear(128,32)
        self.option_text=nn.Sequential(nn.LayerNorm(384),nn.Linear(384,128),nn.GELU())
        self.option_state=nn.Sequential(nn.LayerNorm(128),nn.Linear(128,128),nn.GELU())
        self.option_out=nn.Linear(128,128,bias=False)
        nn.init.normal_(self.option_out.weight,std=.001)

    def load_reference(self,policy_state):
        state={k:policy_state[k] for k in self.state_dict()}
        self.load_state_dict(state,strict=True)

    def option_residual(self,text,latent):
        v=self.option_text(text);q=self.option_state(latent).expand_as(v)
        return (self.option_out(q)*v).sum(-1)/(128**.5)

    def flat_logits(self,latent,supports):
        if latent.shape!=(len(supports),128) or not supports:raise ValueError('Latent/support batch mismatch')
        descriptors=[];owners=[];weights=[];row_indices=[];lengths=[];total=0
        for row,support in enumerate(supports):
            count=len(support['commands']);lengths.append(count)
            if count<1 or len(support['sequences'])!=count:raise ValueError('Invalid complete support')
            for sequence,weight in zip(support['sequences'],support['weights']):
                if not sequence or len(sequence)!=len(weight):raise ValueError('Missing operation components')
                descriptors.extend(support['descriptors'][sequence])
                owners.extend([total]*len(sequence));weights.extend(weight);row_indices.append(row);total+=1
        device=latent.device
        tensor=torch.as_tensor(np.asarray(descriptors,np.float32),device=device)
        encoded=self.argument_encoder(tensor)
        joint=encoded.new_zeros(total,32).index_add(0,torch.tensor(owners,device=device),
                                                 encoded*encoded.new_tensor(weights)[:,None])
        query=self.action_net(latent)
        scores=(query[torch.tensor(row_indices,device=device)]*joint).sum(-1)
        if not torch.isfinite(scores).all():raise ValueError('Nonfinite complete-action scores')
        return list(scores.split(lengths))
