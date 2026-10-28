import unittest
import numpy as np
from recurrent_policy import RecurrentPolicy
from train_ppo import Policy, gradient_check


class RecurrentPolicyTests(unittest.TestCase):
    def test_original_default_and_optional_input_gradient_agree(self):
        random = np.random.default_rng(4)
        head = Policy(3, 6)
        head.enable_kind_factor([0, 0, 1, 1, 2, 2])
        x = random.normal(size=(4, 3))
        mask = np.array([[1, 0, 1, 0, 0, 0]] * 4, dtype=bool)
        a = np.array([0, 2, 0, 2])
        _, p, _ = head.forward(x, mask)
        old = np.log(p[np.arange(4), a]) + .05
        args = (x, mask, a, old, np.array([1., -.3, .7, -.4]), np.zeros(4))
        original = head.loss_grad_legal(*args)
        extra = head.loss_grad_legal(*args, return_input_gradient=True)
        self.assertEqual(original[0], extra[0])
        self.assertEqual(original[2], extra[2])
        for key in original[1]:
            np.testing.assert_array_equal(original[1][key], extra[1][key])
        self.assertLess(gradient_check(hierarchical=True), 1e-6)

    def test_full_sequence_ppo_gradient_reaches_memory_and_action_history(self):
        actions = [{"Kind": "play"}, {"Kind": "play"}, {"Kind": "advance"}, {"Kind": "fight"}]
        policy = RecurrentPolicy(4, actions, (5, 4, 3), 3, 2)
        random = np.random.default_rng(13)
        obs = random.normal(size=(4, 1, 4)) * .2
        previous = np.array([[-1], [0], [2], [1]])
        rewards = np.array([[0.], [-.01], [-.01], [2.]])
        legal = [[0, 2, 3], [0, 1, 2], [1, 2, 3], [0, 1, 3]]
        selected = np.array([[0], [2], [1], [3]])
        old = policy.sequence_log_probabilities(obs, previous, rewards, legal, selected) + .04
        advantage = np.array([[.5], [-.3], [1.], [-.7]])
        returns = np.array([[.8], [-.4], [.7], [-.2]])
        args = (obs, previous, rewards, legal, selected, old, advantage, returns)
        _, gradients, _ = policy.loss_grad(*args)
        for key in ("core.encoder_w0", "core.uz", "core.ur", "core.un", "head.wk",
                    "head.wp", "head.wv", "previous_action_embedding"):
            parameter = policy.parameters()[key]
            index = (0, 0)
            original = parameter[index]
            epsilon = 1e-6
            parameter[index] = original + epsilon
            plus = policy.loss_grad(*args)[0]
            parameter[index] = original - epsilon
            minus = policy.loss_grad(*args)[0]
            parameter[index] = original
            self.assertAlmostEqual((plus - minus) / (2 * epsilon), gradients[key][index], places=7, msg=key)
        self.assertGreater(np.linalg.norm(gradients['core.uz']), 0)
        self.assertGreater(np.linalg.norm(gradients['previous_action_embedding']), 0)

    def test_sequential_inference_equals_full_history(self):
        policy = RecurrentPolicy(4, [{"Kind": "play"}, {"Kind": "fight"}], (5, 4, 3), 3, 2)
        obs = np.random.default_rng(8).normal(size=(4, 1, 4))
        previous = np.array([[-1], [0], [1], [0]])
        rewards = np.zeros((4, 1))
        memory = None
        actual = []
        for i in range(4):
            p, _, memory = policy.step(obs[i, 0], [0, 1], previous[i, 0], 0., memory)
            actual.append(np.log(p[0]))
        expected = policy.sequence_log_probabilities(obs, previous, rewards, [[0, 1]] * 4,
                                                     np.zeros((4, 1), dtype=int))
        np.testing.assert_allclose(actual, expected, rtol=0, atol=1e-15)

    def test_episode_boundary_resets_previous_action_and_reward_too(self):
        policy = RecurrentPolicy(4, [{"Kind": "play"}, {"Kind": "fight"}], (5, 4, 3), 3, 2)
        obs = np.ones((2, 1, 4))
        starts = np.array([[True], [False]])
        a = policy._sequence(obs, [[0], [1]], [[10.], [0.]], episode_starts=starts)[0]
        b = policy._sequence(obs, [[1], [1]], [[-10.], [0.]], episode_starts=starts)[0]
        np.testing.assert_array_equal(a, b)


if __name__ == '__main__':
    unittest.main()
