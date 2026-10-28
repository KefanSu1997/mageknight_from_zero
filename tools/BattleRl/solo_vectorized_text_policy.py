"""Undeployed equivalent candidate text projection and finite-input check."""
import torch
from solo_bulk_text_policy import SoloBulkTextPolicy

class SoloVectorizedTextPolicy(SoloBulkTextPolicy):
    def core_step(self,numeric,entity_text,entity_attributes,candidates,candidate_text,memory=None,episode_start=False):
        p=self.policy;sw,aw,cw=p.dimensions
        if numeric.shape!=(sw,) or entity_text.ndim!=2 or entity_text.shape[1]!=384:
            raise ValueError('State schema mismatch')
        if entity_attributes.shape!=(len(entity_text),aw):raise ValueError('Entity schema mismatch')
        if candidates.ndim!=2 or candidates.shape[1]!=cw or not len(candidates) or len(candidate_text)!=len(candidates):
            raise ValueError('Complete nonempty legal support required')
        if any(r.ndim!=2 or r.shape[1]!=384 for r in candidate_text):raise ValueError('Candidate rule shape mismatch')
        all_rules=torch.cat(candidate_text,0)
        # Exactly the same values are checked, with a single host synchronization.
        checked=torch.cat([x.reshape(-1) for x in (numeric,entity_text,entity_attributes,candidates,all_rules)])
        if not torch.isfinite(checked).all():raise ValueError('Nonfinite policy input')
        if len(entity_text):
            tokens=p.entity_norm(p.entity_text(entity_text)+p.entity_attributes(entity_attributes))
            context=p.entity_attention(tokens[None]).mean(1)
        else:context=p.empty_entity[:,0,:]
        fused=p.fusion(torch.cat((p.numeric(numeric)[None],context),dim=-1))[:,None,:]
        if memory is None or episode_start:memory=p.initial_memory()
        actor,actor_memory=p.actor_lstm(fused,memory[0]);critic,critic_memory=p.critic_lstm(fused,memory[1])
        counts=[len(r) for r in candidate_text]
        if len(all_rules):
            # Preserve mean-of-projected-rules, including distinct repeated rules.
            projected=p.candidate_text(all_rules)
            parts=projected.split(counts)
            text_parts=torch.stack([r.mean(0) if len(r) else candidates.new_zeros(128) for r in parts])
        else:text_parts=candidates.new_zeros(len(candidates),128)
        encoded=p.candidate_encoder(p.candidate_numeric(candidates)+text_parts)
        logits=encoded@p.actor_query(actor[0,0])/(32**.5)
        value=p.value_head(critic[0,0]).squeeze(-1)
        return logits,value,(actor_memory,critic_memory)
