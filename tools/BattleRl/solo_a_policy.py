"""Fresh solo-conquest A-family network core; transport/PPO adapter pending.

Inputs must be schema-bound public tensors, not the old 6314 battle projection.
Frozen 384-wide rule vectors are provided by SoloTextAssets. Dynamic entity and
candidate counts have no fixed cap. This is not checkpoint-compatible with A.
"""
import torch
from torch import nn


class SoloAPolicy(nn.Module):
    def __init__(self, state_width, attribute_width, candidate_width):
        super().__init__()
        if min(state_width, attribute_width, candidate_width) <= 0:
            raise ValueError('Explicit complete public schema dimensions required')
        self.dimensions = (state_width, attribute_width, candidate_width)
        self.numeric = nn.Sequential(nn.Linear(state_width,256), nn.Tanh(), nn.Linear(256,128), nn.Tanh())
        self.entity_text = nn.Sequential(nn.LayerNorm(384), nn.Linear(384,128))
        self.entity_attributes = nn.Linear(attribute_width,128)
        layer = nn.TransformerEncoderLayer(128,4,256,dropout=0.,activation='gelu',
                                          batch_first=True,norm_first=True)
        self.entity_attention = nn.TransformerEncoder(layer,2,enable_nested_tensor=False)
        self.entity_norm = nn.LayerNorm(128)
        self.empty_entity = nn.Parameter(torch.zeros(1,1,128))
        self.fusion = nn.Sequential(nn.Linear(256,128),nn.Tanh())
        # Memory persists across encounters/rounds and resets only at episode start.
        self.actor_lstm = nn.LSTM(128,256,batch_first=True)
        self.critic_lstm = nn.LSTM(128,256,batch_first=True)
        self.actor_query = nn.Sequential(nn.Linear(256,128),nn.Tanh(),nn.Linear(128,32))
        self.value_head = nn.Sequential(nn.Linear(256,128),nn.Tanh(),nn.Linear(128,1))
        self.candidate_numeric = nn.Linear(candidate_width,128,bias=False)
        self.candidate_text = nn.Sequential(nn.LayerNorm(384),nn.Linear(384,128,bias=False))
        self.candidate_encoder = nn.Sequential(nn.LayerNorm(128),nn.GELU(),nn.Linear(128,32,bias=False))

    def initial_memory(self, batch=1):
        parameter=next(self.parameters())
        return tuple((parameter.new_zeros(1,batch,256),parameter.new_zeros(1,batch,256)) for _ in range(2))

    def step(self, numeric, entity_text, entity_attributes, candidates, candidate_text,
             memory=None, episode_start=False):
        """One decision; caller batches variable supports without pruning.

        Candidate texts are [K,R,384] with a presence mask handled upstream by
        supplying a list of [R_i,384] tensors; distinct rules remain separate until
        this learned projection. No zero-vector substitute for unknown identities.
        """
        sw,aw,cw=self.dimensions
        if numeric.shape!=(sw,) or entity_text.ndim!=2 or entity_text.shape[1]!=384:
            raise ValueError('State schema mismatch')
        if entity_attributes.shape!=(len(entity_text),aw):
            raise ValueError('Entity schema mismatch')
        if candidates.ndim!=2 or candidates.shape[1]!=cw or len(candidates)==0 or len(candidate_text)!=len(candidates):
            raise ValueError('Complete nonempty legal support required')
        tensors=[numeric,entity_text,entity_attributes,candidates,*candidate_text]
        if any(not torch.isfinite(x).all() for x in tensors):
            raise ValueError('Nonfinite policy input')
        if len(entity_text):
            tokens=self.entity_norm(self.entity_text(entity_text)+self.entity_attributes(entity_attributes))
            context=self.entity_attention(tokens[None]).mean(1)
        else:
            context=self.empty_entity[:,0,:]
        fused=self.fusion(torch.cat((self.numeric(numeric)[None],context),dim=-1))[:,None,:]
        if memory is None or episode_start:
            memory=self.initial_memory()
        actor,actor_memory=self.actor_lstm(fused,memory[0])
        critic,critic_memory=self.critic_lstm(fused,memory[1])
        text_parts=[]
        for rules in candidate_text:
            if rules.ndim!=2 or rules.shape[1]!=384:
                raise ValueError('Candidate rule shape mismatch')
            text_parts.append(self.candidate_text(rules).mean(0) if len(rules)
                              else candidates.new_zeros(128))
        encoded=self.candidate_encoder(self.candidate_numeric(candidates)+torch.stack(text_parts))
        logits=encoded@self.actor_query(actor[0,0])/(32**.5)
        value=self.value_head(critic[0,0]).squeeze(-1)
        return logits,value,(actor_memory,critic_memory)
