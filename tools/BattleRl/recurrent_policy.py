"""Recurrent masked PPO policy sharing the verified original factored PPO loss.

Historical inputs are previous actual action and raw public reward, never audit
zones or hidden deck order. The initial implementation preserves the original
action catalog; a relational Transformer/candidate-action head remains separate.
"""
import numpy as np
from recurrent_core import EncoderGRU
from train_ppo import Policy


class RecurrentPolicy:
    def __init__(self, observation_size, actions, encoder_sizes=(256, 256, 128),
                 hidden_size=256, embedding_size=32, seed=7,
                 entropy_coefficient=.01, value_coefficient=.02):
        self.observation_size = observation_size
        self.actions = actions
        self.embedding_size = embedding_size
        self.core = EncoderGRU(observation_size + embedding_size + 1,
                               encoder_sizes, hidden_size, seed)
        self.head = Policy(hidden_size, len(actions), seed, entropy_coefficient,
                           value_coefficient)
        kinds = sorted({a['Kind'] for a in actions})
        self.head.enable_kind_factor([kinds.index(a['Kind']) for a in actions])
        self.embedding = np.random.default_rng(seed + 1).normal(
            0, .02, (len(actions), embedding_size))

    def parameters(self):
        result = {f"core.{k}": v for k, v in self.core.parameters.items()}
        result.update({f"head.{k}": v for k, v in self.head.p.items()
                       if k != "action_kind_groups"})
        result["previous_action_embedding"] = self.embedding
        return result

    def bind_parameters(self, parameters):
        expected = self.parameters()
        if parameters.keys() != expected.keys() or any(
                parameters[k].shape != v.shape for k, v in expected.items()):
            raise ValueError("Recurrent checkpoint architecture differs")
        self.core.parameters = {k: parameters[f"core.{k}"] for k in self.core.parameters}
        for key in self.head.p:
            if key != "action_kind_groups":
                self.head.p[key] = parameters[f"head.{key}"]
        self.embedding = parameters["previous_action_embedding"]

    def _sequence(self, observations, previous_actions, previous_rewards,
                  initial_memory=None, episode_starts=None):
        observations = np.asarray(observations)
        previous_actions = np.asarray(previous_actions, dtype=np.int64)
        previous_rewards = np.asarray(previous_rewards)
        if observations.ndim != 3 or observations.shape[-1] != self.observation_size:
            raise ValueError("Observations must be [time,batch,public_features]")
        if previous_actions.shape != observations.shape[:2] or previous_rewards.shape != previous_actions.shape:
            raise ValueError("Public history dimensions differ")
        if np.any(previous_actions < -1) or np.any(previous_actions >= len(self.actions)):
            raise ValueError("Previous action must be original catalog index or episode sentinel")
        if episode_starts is not None:
            starts = np.asarray(episode_starts, dtype=bool)
            if starts.shape != previous_actions.shape:
                raise ValueError("Episode-start dimensions differ")
            # Reset history inputs as well as hidden memory. A preceding
            # episode's action/reward must not leak through the fresh encoder.
            previous_actions = previous_actions.copy()
            previous_rewards = previous_rewards.copy()
            previous_actions[starts] = -1
            previous_rewards[starts] = 0.
        embedded = np.zeros((*previous_actions.shape, self.embedding_size))
        present = previous_actions >= 0
        embedded[present] = self.embedding[previous_actions[present]]
        inputs = np.concatenate([observations, embedded, previous_rewards[..., None] / 20.], axis=-1)
        hidden, final, cache = self.core.forward(inputs, initial_memory, episode_starts)
        return hidden, final, cache, previous_actions

    def step(self, observation, legal, previous_action=-1, previous_reward=0., memory=None):
        hidden, final, _, _ = self._sequence(np.asarray(observation)[None, None],
                                             [[previous_action]], [[previous_reward]], memory)
        probabilities, value = self.head.action_forward(hidden[0, 0], legal)
        return probabilities, value, final

    def loss_grad(self, observations, previous_actions, previous_rewards, legal,
                  selected, old_log, advantages, returns, initial_memory=None,
                  episode_starts=None):
        hidden, _, cache, previous = self._sequence(
            observations, previous_actions, previous_rewards, initial_memory, episode_starts)
        rows = int(np.prod(hidden.shape[:2]))
        mask = np.zeros((rows, len(self.actions)), dtype=bool)
        if len(legal) != rows:
            raise ValueError("Each sequence state must supply its formal legal actions")
        for row, indices in enumerate(legal):
            mask[row, indices] = True
        loss, head_gradients, kl, dh = self.head.loss_grad_legal(
            hidden.reshape(rows, -1), mask, np.asarray(selected).reshape(rows),
            np.asarray(old_log).reshape(rows), np.asarray(advantages).reshape(rows),
            np.asarray(returns).reshape(rows), return_input_gradient=True)
        dx, _, core_gradients = self.core.backward(dh.reshape(hidden.shape), cache)
        embedding_gradient = np.zeros_like(self.embedding)
        present = previous >= 0
        np.add.at(embedding_gradient, previous[present],
                  dx[..., self.observation_size:self.observation_size + self.embedding_size][present])
        gradients = {f"core.{k}": v for k, v in core_gradients.items()}
        gradients.update({f"head.{k}": v for k, v in head_gradients.items()})
        gradients["previous_action_embedding"] = embedding_gradient
        return loss, gradients, kl

    def sequence_log_probabilities(self, observations, previous_actions, previous_rewards,
                                   legal, selected, initial_memory=None, episode_starts=None):
        hidden, _, _, _ = self._sequence(observations, previous_actions, previous_rewards,
                                         initial_memory, episode_starts)
        result = []
        for row, indices, action in zip(hidden.reshape(-1, hidden.shape[-1]), legal,
                                        np.asarray(selected).ravel()):
            probabilities, _ = self.head.action_forward(row, indices)
            result.append(np.log(max(probabilities[action], 1e-30)))
        return np.asarray(result)
