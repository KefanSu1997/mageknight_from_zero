"""Staged Transformer + three-layer public-state encoder + GRU PPO actor.

The normal actor, native rules, reward and checkpoints are not replaced.
Public flat observation/history remain intact; an entity summary is appended
inside the encoder. Real training and signature migration require their own gate.
"""
import numpy as np
from entity_transformer import EntityTransformerBlock
from recurrent_core import EncoderGRU
from recurrent_policy import RecurrentPolicy


class EntityEncoderGRU(EncoderGRU):
    def __init__(self, ordinary_input_size, observation_size, layout,
                 encoder_sizes=(256, 256, 128), hidden_size=256, entity_width=32,
                 entity_heads=4, seed=7):
        self.ordinary_input_size = ordinary_input_size
        self.observation_size = observation_size
        self.layout = layout
        self.entity_width = entity_width
        super().__init__(ordinary_input_size + entity_width, encoder_sizes, hidden_size, seed)
        self.entity = EntityTransformerBlock(layout.width, entity_width, entity_heads, entity_width * 2, seed + 2)
        self.parameters.update({f'entity.{key}': value for key, value in self.entity.parameters.items()})

    def forward(self, inputs, initial_memory=None, episode_starts=None):
        inputs = np.asarray(inputs)
        if inputs.ndim != 3 or inputs.shape[-1] != self.ordinary_input_size:
            raise ValueError('Ordinary public observation/action/raw-reward history required')
        # Rebinding a complete saved policy must also rebind attention arrays.
        self.entity.parameters = {key: self.parameters[f'entity.{key}'] for key in self.entity.parameters}
        summaries, entity_cache = [], []
        for row in inputs:
            tokens, present, identities = self.layout.pack(row[:, :self.observation_size])
            summary, cache = self.entity.forward(tokens, present)
            summaries.append(summary)
            entity_cache.append((cache, identities))
        augmented = np.concatenate([inputs, np.stack(summaries)], axis=-1)
        outputs, final, cache = super().forward(augmented, initial_memory, episode_starts)
        return outputs, final, (cache, entity_cache)

    def backward(self, output_gradients, cache):
        recurrent_cache, entity_cache = cache
        dx, initial_gradient, gradients = super().backward(output_gradients, recurrent_cache)
        ordinary = dx[..., :self.ordinary_input_size].copy()
        for time, (attention_cache, identities) in enumerate(entity_cache):
            token_gradient, attention_gradient = self.entity.backward(dx[time, :, self.ordinary_input_size:], attention_cache)
            ordinary[time, :, :self.observation_size] += self.layout.scatter_numeric_gradient(token_gradient, identities)
            for key, value in attention_gradient.items():
                gradients[f'entity.{key}'] += value
        return ordinary, initial_gradient, gradients


class EntityRecurrentPolicy(RecurrentPolicy):
    def __init__(self, observation_size, actions, layout, encoder_sizes=(256, 256, 128),
                 hidden_size=256, embedding_size=32, seed=7,
                 entropy_coefficient=.01, value_coefficient=.02,
                 entity_width=32, entity_heads=4):
        super().__init__(observation_size, actions, encoder_sizes, hidden_size,
                         embedding_size, seed, entropy_coefficient, value_coefficient)
        self.core = EntityEncoderGRU(observation_size + embedding_size + 1,
            observation_size, layout, encoder_sizes, hidden_size, entity_width, entity_heads, seed)
