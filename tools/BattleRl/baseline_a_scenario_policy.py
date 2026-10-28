"""A topology with solo inputs and unmodified upstream recurrent sequencing.

Runtime must bind immutable complete supports and joint-context marker vectors.
It is intentionally external to weight serialization; a reloaded policy refuses
to act until its SHA-bound runtime is reattached.
"""
import numpy as np
import gymnasium as gym
import torch
from sb3_contrib.common.recurrent.policies import RecurrentActorCriticPolicy
from final_token_policy import Distribution as PathDistribution
from structured_hierarchy_core import StructuredHierarchy
from baseline_a_world_entities import ScenarioEntityTextEncoder
from baseline_a_scenario_candidate_head import ScenarioCandidateHead


class ScenarioDistribution(PathDistribution):
    def __init__(self,policy,latent,flat):
        self.policy=policy;self.latent=latent;self.flat=flat
        self.contexts=policy._contexts.copy();self.runtime=policy.runtime
        self.memo={};self.cores={}

    def distributions(self,rows,nodes):
        requests=[(int(self.contexts[r]),int(n)) for r,n in zip(rows,nodes)]
        vectors=self.runtime.vectors(requests);scores=[]
        for row,node,vector in zip(rows,nodes,vectors):
            tree,_=self.runtime.tree(int(self.contexts[row]))
            if row not in self.cores:self.cores[row]=StructuredHierarchy(tree,self.flat[row])
            prior=self.cores[row].categorical(node).logits
            text=torch.as_tensor(np.asarray(vector[1:]),device=self.latent.device,dtype=self.latent.dtype)
            if text.shape!=(len(prior),384):raise ValueError('Joint marker rows do not match complete branch')
            scores.append(prior+self.policy.head.option_residual(text,self.latent[row]))
        width=max(map(len,scores))
        logits=torch.stack([torch.nn.functional.pad(s,(0,width-len(s)),value=-torch.inf) for s in scores])
        return torch.distributions.Categorical(logits=logits)

    def log_prob(self,actions):
        self.memo.clear()  # Branch widths differ; recompute ragged paths together.
        return super().log_prob(actions)


class BaselineAScenarioPolicy(RecurrentActorCriticPolicy):
    def __init__(self,observation_space,action_space,lr_schedule,*,entity_vectors,**kwargs):
        self.entity_vectors=np.array(entity_vectors,dtype=np.float32,copy=True)
        kwargs.update(features_extractor_class=ScenarioEntityTextEncoder,
            features_extractor_kwargs=dict(entity_vectors=self.entity_vectors),ortho_init=False,
            net_arch=dict(pi=[128],vf=[128]),lstm_hidden_size=256,
            enable_critic_lstm=True,shared_lstm=False)
        # Native action indices are dynamic. Avoid allocating a fixed output
        # matrix for their integer transport range; the actual head scores every
        # member of the current complete support instead.
        super().__init__(observation_space,gym.spaces.Discrete(1),lr_schedule,**kwargs)
        self.action_space=action_space
        del self.action_net
        self.head=ScenarioCandidateHead()
        self.runtime=None;self.context_path=None;self._contexts=None
        self.optimizer=self.optimizer_class(self.parameters(),lr=lr_schedule(1),**self.optimizer_kwargs)

    def _get_constructor_parameters(self):
        data=super()._get_constructor_parameters();data['entity_vectors']=self.entity_vectors
        return data

    def bind_runtime(self,runtime):
        for name in ('support','tree','vectors'):
            if not callable(getattr(runtime,name,None)):raise ValueError('Incomplete scenario runtime')
        self.runtime=runtime;self.context_path=str(runtime.path)

    def load_reference(self,state):
        """Diagnostic migration only; formal fresh training does not call this."""
        encoder={k.removeprefix('features_extractor.'):v for k,v in state.items()
                 if k.startswith('features_extractor.')}
        self.features_extractor.load_reference(encoder)
        self.head.load_reference(state)
        for name in ('lstm_actor','lstm_critic','mlp_extractor','value_net'):
            prefix=name+'.'
            getattr(self,name).load_state_dict({k[len(prefix):]:v for k,v in state.items()
                                              if k.startswith(prefix)},strict=True)

    def _bind(self,obs,padded=False):
        if self.runtime is None:raise ValueError('Reattach the verified scenario runtime before inference')
        values=obs['context_id'].detach().cpu().numpy().reshape(-1)
        if not np.isfinite(values).all() or np.any(values!=values.astype(np.int64)):
            raise ValueError('Corrupted immutable context ID')
        self._contexts=values.astype(np.int64)
        if np.any(self._contexts<0) or (not padded and np.any(self._contexts==0)):
            raise ValueError('Live state requires a bound context')

    def _get_action_dist_from_latent(self,latent_pi):
        supports=[self.runtime.support(int(c)) for c in self._contexts]
        return ScenarioDistribution(self,latent_pi,self.head.flat_logits(latent_pi,supports))

    def forward(self,obs,lstm_states,episode_starts,deterministic=False):
        self._bind(obs)
        return super().forward(obs,lstm_states,episode_starts,deterministic)

    def get_distribution(self,obs,lstm_states,episode_starts):
        self._bind(obs)
        return super().get_distribution(obs,lstm_states,episode_starts)

    def evaluate_actions(self,obs,actions,lstm_states,episode_starts):
        self._bind(obs,padded=True)
        values,logs,entropy=super().evaluate_actions(obs,actions,lstm_states,episode_starts)
        reference=getattr(self,'_entropy_reference',None)
        if reference is not None:
            selected=actions.detach().cpu().numpy().reshape(-1).astype(np.int64)
            old=logs.new_tensor([reference[(int(c),int(a))] if c else 0. for c,a in zip(self._contexts,selected)])
            entropy=-(logs-old).exp()*logs
        return values,logs,entropy
