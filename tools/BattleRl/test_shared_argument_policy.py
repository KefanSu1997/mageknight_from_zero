import unittest
import numpy as np
from train_ppo import Policy
from shared_argument_policy import SharedArgumentPolicy
from action_argument_features import catalog_argument_features


class SharedArgumentPolicyTests(unittest.TestCase):
    def setUp(self):
        self.features = np.random.default_rng(31).normal(size=(7, 5))
        self.net = SharedArgumentPolicy(3, self.features, rank=4)
        self.net.enable_kind_factor([0, 0, 0, 1, 1, 2, 2])
        self.x = np.random.default_rng(41).normal(size=(4, 3))
        self.mask = np.array([[1, 1, 0, 1, 0, 1, 0], [1, 0, 1, 0, 1, 1, 0],
                              [0, 1, 1, 1, 0, 0, 1], [1, 0, 0, 1, 1, 0, 1]], dtype=bool)
        self.selected = np.array([0, 4, 6, 3])

    def test_zero_residual_exact_original_and_no_parameter_mutation(self):
        original = Policy(3, 7, value_coefficient=.02)
        original.enable_kind_factor([0, 0, 0, 1, 1, 2, 2])
        a = original.forward(self.x, self.mask)
        saved = {key: value.copy() for key, value in self.net.p.items()}
        b = self.net.forward(self.x, self.mask)
        for x, y in zip(a, b):
            np.testing.assert_array_equal(x, y)
        for row in range(4):
            p, _ = self.net.action_forward(self.x[row], np.flatnonzero(self.mask[row]))
            np.testing.assert_allclose(p, b[1][row], atol=1e-14)
        for key in saved:
            np.testing.assert_array_equal(saved[key], self.net.p[key])

    def test_nonzero_residual_ppo_gradient_and_exception_restore(self):
        self.net.p['argument_project'][:] = np.random.default_rng(5).normal(0, .1, (5, 4))
        _, p, _ = self.net.forward(self.x, self.mask)
        old = np.log(p[np.arange(4), self.selected]) + .03
        args = (self.x, self.mask, self.selected, old, np.array([1., -.2, .5, -.6]), np.ones(4))
        saved = self.net.p['wp'].copy()
        _, gradients, _, dx = self.net.loss_grad_legal(*args, return_input_gradient=True)
        for key in ('argument_query', 'argument_project', 'w1', 'wp', 'wk'):
            parameter = self.net.p[key]
            index = np.unravel_index(abs(gradients[key]).argmax(), parameter.shape)
            value = parameter[index]
            parameter[index] = value + 1e-5; plus = self.net.loss_grad_legal(*args)[0]
            parameter[index] = value - 1e-5; minus = self.net.loss_grad_legal(*args)[0]
            parameter[index] = value
            self.assertAlmostEqual((plus - minus) / 2e-5, gradients[key][index], places=6, msg=key)
        value = self.x[0, 1]
        self.x[0, 1] = value + 1e-5; plus = self.net.loss_grad_legal(*args)[0]
        self.x[0, 1] = value - 1e-5; minus = self.net.loss_grad_legal(*args)[0]
        self.x[0, 1] = value
        self.assertAlmostEqual((plus - minus) / 2e-5, dx[0, 1], places=6)
        with self.assertRaises(ValueError):
            self.net.loss_grad_legal(self.x, self.mask, np.array([9, 4, 6, 3]), old, args[4], args[5])
        np.testing.assert_array_equal(saved, self.net.p['wp'])
        self.assertFalse(self.net._residual_active)
        with self.assertRaises(ValueError):
            self.net.loss_grad(*args)
        with self.assertRaises(ValueError):
            self.net.update(gradients)
        current = self.net.forward(self.x, self.mask)[1]
        logs = np.log(current[np.arange(4), self.selected])
        self.assertAlmostEqual(self.net.rollout_kl_legal(self.x, self.mask, self.selected, logs), 0., places=12)

    def test_public_descriptor_ignores_key_and_external_state(self):
        a = dict(Kind='attack', Card='basic_card_007', Choice='strong', Targets=1, Parts=5, Option=0, Input='')
        b = dict(a, Key='different', auditDrawOrder=['secret'], future_state='secret')
        np.testing.assert_array_equal(catalog_argument_features([a]), catalog_argument_features([b]))
        values = catalog_argument_features([a])[0]
        np.testing.assert_array_equal(values[73:81], [1, 0, 1, 0, 0, 0, 0, 0])


if __name__ == '__main__':
    unittest.main()
