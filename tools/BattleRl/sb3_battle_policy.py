"""Game-specific inputs/distribution; sequence PPO and autograd belong to SB3/PyTorch.

SB3-Contrib 2.7 RecurrentPPO supplies collection, GAE, padded sequence minibatches,
clipped PPO, value/entropy loss, KL stopping and Adam. No copied training loop.
"""
import numpy as np
import hashlib
from pathlib import Path
import torch
from torch import nn
from stable_baselines3.common.torch_layers import BaseFeaturesExtractor
from sb3_contrib.common.recurrent.policies import RecurrentActorCriticPolicy


class BattleEncoder(BaseFeaturesExtractor):
    def __init__(self, observation_space):
        super().__init__(observation_space, features_dim=128)
        width = observation_space['state'].shape[0]
        self.encoder = nn.Sequential(nn.Linear(width, 256), nn.Tanh(),
                                     nn.Linear(256, 256), nn.Tanh(),
                                     nn.Linear(256, 128), nn.Tanh())

    def forward(self, observations):
        return self.encoder(observations['state'].float())


class BattleRecurrentPolicy(RecurrentActorCriticPolicy):
    """Full global action IDs, no top-k or legal-action truncation.

    Masks travel with observations into the upstream recurrent buffer, so the
    likelihood in optimization uses precisely the mask used in collection.
    Bit packing avoids storing 95k float mask entries per transition.
    """
    def __init__(self, *args, descriptors=None, descriptor_file=None, descriptor_sha256=None, **kwargs):
        kwargs.update(features_extractor_class=BattleEncoder, ortho_init=False,
                      net_arch=dict(pi=[128], vf=[128]), lstm_hidden_size=256,
                      enable_critic_lstm=True, shared_lstm=False)
        super().__init__(*args, **kwargs)
        if descriptor_file is not None:
            path = Path(descriptor_file)
            if hashlib.sha256(path.read_bytes()).hexdigest() != descriptor_sha256:
                raise ValueError('Public action descriptor binding changed')
            descriptors = np.load(path, allow_pickle=False)
        features = torch.as_tensor(np.array(descriptors, dtype=np.float32, copy=True))
        # Immutable public input is shared by checkpoints, bound by SHA. Legacy
        # array-based gate checkpoints still load with their persistent buffer.
        self.register_buffer('descriptors', features, persistent=descriptor_file is None)
        self.argument_encoder = nn.Linear(features.shape[1], 32, bias=False)
        self.action_net = nn.Linear(128, 32)
        self._mask = None
        # Includes LSTMs and the replacement action head; no manual gradients.
        self.optimizer = self.optimizer_class(self.parameters(), lr=kwargs.get('lr_schedule', args[2] if len(args)>2 else lambda _: 1e-4)(1),
                                              **self.optimizer_kwargs)

    def _bind_mask(self, obs, padded=False):
        packed = obs['legal_bits'].to(torch.uint8)
        shifts = torch.arange(8, device=packed.device, dtype=torch.uint8)
        mask = ((packed.unsqueeze(-1) >> shifts) & 1).reshape(packed.shape[0], -1)
        mask = mask[:, :self.action_space.n].bool()
        empty = ~mask.any(dim=1)
        if padded:
            # Upstream loss explicitly excludes sequence padding. Its all-zero
            # observation must nevertheless have a finite dummy distribution.
            mask[empty, 0] = True
        elif empty.any():
            raise ValueError('Live observation has no legal action')
        self._mask = mask

    def _get_action_dist_from_latent(self, latent_pi):
        logits = self.action_net(latent_pi) @ self.argument_encoder(self.descriptors).T
        logits = logits.masked_fill(~self._mask, -torch.inf)
        return self.action_dist.proba_distribution(action_logits=logits)

    def forward(self, obs, lstm_states, episode_starts, deterministic=False):
        self._bind_mask(obs)
        return super().forward(obs, lstm_states, episode_starts, deterministic)

    def evaluate_actions(self, obs, actions, lstm_states, episode_starts):
        self._bind_mask(obs, padded=True)
        return super().evaluate_actions(obs, actions, lstm_states, episode_starts)

    def get_distribution(self, obs, lstm_states, episode_starts):
        self._bind_mask(obs)
        return super().get_distribution(obs, lstm_states, episode_starts)
