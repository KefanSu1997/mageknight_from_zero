"""New actor sequence/gradient/restore gates, not MageKnight success."""
import tempfile
from pathlib import Path
import unittest
import numpy as np
from entity_recurrent_policy import EntityRecurrentPolicy
from recurrent_policy import RecurrentPolicy
from recurrent_checkpoint import Adam, load_checkpoint, save_checkpoint


class NumericFixtureLayout:
    width = 2
    def pack(self, features):
        return features.reshape(-1, 2, 2), np.ones((len(features), 2), dtype=bool), [None] * len(features)
    def scatter_numeric_gradient(self, gradient, identities):
        return gradient.reshape(-1, 4)


class EntityRecurrentPolicyTests(unittest.TestCase):
    actions = [{'Kind': kind} for kind in ('play', 'advance', 'fight', 'play')]
    def policy(self):
        return EntityRecurrentPolicy(4, self.actions, NumericFixtureLayout(),
            encoder_sizes=(6, 5, 4), hidden_size=4, embedding_size=2,
            entity_width=4, entity_heads=2)

    def arguments(self, policy, rng):
        observation = rng.normal(size=(4, 1, 4)) * .3
        previous = np.array([[-1], [0], [1], [2]])
        reward = np.array([[0.], [-.01], [1.], [-.02]])
        legal = [[0, 1, 2, 3]] * 4
        selected = np.array([[0], [1], [2], [3]])
        old = policy.sequence_log_probabilities(observation, previous, reward, legal, selected) + .02
        return observation, previous, reward, legal, selected, old, rng.normal(size=(4, 1)), rng.normal(size=(4, 1))

    def test_sequence_ppo_gradient_reaches_attention_gru_and_history(self):
        policy = self.policy()
        args = self.arguments(policy, np.random.default_rng(17))
        _, gradients, _ = policy.loss_grad(*args)
        keys = ['core.entity.' + key for key in policy.core.entity.parameters]
        keys += ['core.encoder_w0', 'core.uz', 'core.un', 'head.wp', 'previous_action_embedding']
        for key in keys:
            parameter = policy.parameters()[key]
            index = np.unravel_index(np.abs(gradients[key]).argmax(), parameter.shape)
            original = parameter[index]
            parameter[index] = original + 1e-5
            plus = policy.loss_grad(*args)[0]
            parameter[index] = original - 1e-5
            minus = policy.loss_grad(*args)[0]
            parameter[index] = original
            self.assertAlmostEqual((plus - minus) / 2e-5, gradients[key][index], places=6, msg=key)
        for key in ('core.entity.project', 'core.entity.q', 'core.entity.v', 'core.uz', 'previous_action_embedding'):
            self.assertGreater(np.linalg.norm(gradients[key]), 1e-8, key)

    def test_initial_zero_summary_columns_keep_old_policy_and_reset_history(self):
        old = RecurrentPolicy(4, self.actions, (6, 5, 4), 4, 2)
        new = self.policy()
        for key, value in old.parameters().items():
            if key == 'core.encoder_w0':
                new.parameters()[key][:len(value)] = value
                new.parameters()[key][len(value):] = 0.
            else:
                new.parameters()[key][...] = value
        args = self.arguments(old, np.random.default_rng(17))
        np.testing.assert_allclose(old.sequence_log_probabilities(*args[:5]), new.sequence_log_probabilities(*args[:5]), atol=1e-14)
        obs, previous, reward = args[:3]
        memory = None
        sequential = []
        for i in range(len(obs)):
            probabilities, _, memory = new.step(obs[i, 0], args[3][i], previous[i, 0], reward[i, 0], memory)
            sequential.append(np.log(probabilities[args[4][i, 0]]))
        np.testing.assert_allclose(sequential, new.sequence_log_probabilities(*args[:5]), atol=1e-14)
        start = np.array([[True]])
        a = new._sequence(obs[-1:], previous[-1:], reward[-1:], np.ones((1, 4)), start)[0]
        b = new._sequence(obs[-1:], np.array([[-1]]), np.zeros((1, 1)))[0]
        np.testing.assert_array_equal(a, b)

    def test_full_parameter_adam_rng_restore_after_real_sequence_updates(self):
        policy = self.policy()
        optimizer = Adam(policy.parameters(), learning_rate=.0001)
        rng = np.random.default_rng(31)
        for _ in range(2):
            optimizer.update(policy.loss_grad(*self.arguments(policy, rng))[1])
        metadata = dict(rules_sha256='fixture_not_rules', catalog_sha256='fixture_not_catalog',
            feature_schema='numeric_fixture_not_game', architecture='entity_mlp3_gru_fixture', task_goal='gradient_restore_only')
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'checkpoint.npz'
            save_checkpoint(path, optimizer, rng, metadata, {'episode_boundary': True})
            arrays, other_optimizer, other_rng, replay = load_checkpoint(path, metadata)
            other = self.policy()
            other.bind_parameters(arrays)
            for _ in range(2):
                optimizer.update(policy.loss_grad(*self.arguments(policy, rng))[1])
                other_optimizer.update(other.loss_grad(*self.arguments(other, other_rng))[1])
            for key in policy.parameters():
                for a, b in ((policy.parameters()[key], other.parameters()[key]),
                             (optimizer.first[key], other_optimizer.first[key]),
                             (optimizer.second[key], other_optimizer.second[key])):
                    np.testing.assert_array_equal(a, b)
            self.assertEqual(rng.bit_generator.state, other_rng.bit_generator.state)
            self.assertEqual(optimizer.steps, other_optimizer.steps)
            self.assertTrue(replay['episode_boundary'])


if __name__ == '__main__':
    unittest.main()
