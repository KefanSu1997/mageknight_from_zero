"""A's saved-state recurrent minibatches for complete solo-game episodes.

No replay from game start at each minibatch. No round/battle memory reset.
PPO losses and recurrent sequence padding remain in upstream SB3-Contrib.
"""
import numpy as np
import torch
from sb3_contrib.common.recurrent.buffers import RecurrentDictRolloutBuffer
from compact_recurrent_buffer import CompactRecurrentDictRolloutBuffer


class ScenarioRecurrentBuffer(CompactRecurrentDictRolloutBuffer):
    def reset(self):
        # Keep A's sampling/singleton-tail handling, but preserve the declared
        # entity/support-ID dtypes without requiring the obsolete legal bitset.
        RecurrentDictRolloutBuffer.reset(self)
        for key,space in self.observation_space.spaces.items():
            self.observations[key]=np.zeros((self.buffer_size,self.n_envs,*self.obs_shape[key]),dtype=space.dtype)


def complete_scenario_buffer(episodes,observation_space,action_space,*,device='cuda',layers=1,hidden=256):
    if not episodes or any(not e for e in episodes):raise ValueError('Complete nonempty games required')
    total=sum(map(len,episodes))
    if total<2:raise ValueError('PPO advantage normalization needs at least two transitions')
    buffer=ScenarioRecurrentBuffer(total,observation_space,action_space,(total,layers,1,hidden),
                                  device=device,gamma=1.,gae_lambda=1.,n_envs=1)
    for episode in episodes:
        for i,row in enumerate(episode):
            if bool(row['episode_start'])!=(i==0):raise ValueError('Memory may reset only at whole-game start')
            if bool(row['terminated'])!=(i==len(episode)-1):raise ValueError('Incomplete game or premature terminal')
            if set(row['observation'])!=set(observation_space.spaces):raise ValueError('Observation schema mismatch')
            states=row['lstm_before']
            for pair in states:
                for value in pair:
                    if value.shape!=(layers,1,hidden) or not torch.isfinite(value).all():
                        raise ValueError('Invalid archived recurrent state')
                    if i==0 and torch.count_nonzero(value):raise ValueError('Initial memory must be cleared')
            if not np.isfinite([row['reward'],row['value'],row['log_probability']]).all():
                raise ValueError('Nonfinite behavior/target input')
            buffer.add({k:np.asarray(v)[None] for k,v in row['observation'].items()},
                       np.array([[row['action']]]),np.array([row['reward']]),np.array([i==0]),
                       torch.tensor([row['value']]),torch.tensor([row['log_probability']]),lstm_states=states)
    # Every retained game reached the actual scenario terminal: no value bootstrap.
    buffer.compute_returns_and_advantage(torch.zeros(1,device=device),np.ones(1))
    return buffer
