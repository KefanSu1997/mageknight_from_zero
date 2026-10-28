"""Gradient, permutation and padding gates, not PPO/model acceptance."""
import unittest
import numpy as np
from entity_transformer import EntityTransformerBlock


class EntityTransformerTests(unittest.TestCase):
    def setUp(self):
        self.block = EntityTransformerBlock(5, width=8, heads=2, feedforward=11)
        rng = np.random.default_rng(19)
        self.x = rng.normal(size=(2, 4, 5))
        self.mask = np.array([[True, True, False, False], [True, True, True, False]])
        self.dy = rng.normal(size=(2, 8))

    def test_gradients_all_parameter_families_and_input(self):
        _, cache = self.block.forward(self.x, self.mask)
        dx, gradients = self.block.backward(self.dy, cache)
        def objective():
            return float((self.block.forward(self.x, self.mask)[0] * self.dy).sum())
        for key, values in self.block.parameters.items():
            for index in [tuple(0 for _ in values.shape), tuple(s - 1 for s in values.shape)]:
                with self.subTest(parameter=key, index=index):
                    original = values[index]
                    values[index] = original + 1e-5
                    plus = objective()
                    values[index] = original - 1e-5
                    minus = objective()
                    values[index] = original
                    self.assertAlmostEqual((plus - minus) / 2e-5, gradients[key][index], places=6)
        for index in [(0, 0, 0), (1, 2, 4), (0, 3, 2)]:
            original = self.x[index]
            self.x[index] = original + 1e-5
            plus = objective()
            self.x[index] = original - 1e-5
            minus = objective()
            self.x[index] = original
            self.assertAlmostEqual((plus - minus) / 2e-5, dx[index], places=6)
        np.testing.assert_array_equal(dx[~self.mask], 0.)

    def test_permutation_and_arbitrary_padding_keep_outputs_gradients(self):
        value, cache = self.block.forward(self.x, self.mask)
        dx, grad = self.block.backward(self.dy, cache)
        order = [2, 0, 3, 1]
        other, cache2 = self.block.forward(self.x[:, order], self.mask[:, order])
        dx2, grad2 = self.block.backward(self.dy, cache2)
        np.testing.assert_allclose(value, other, atol=1e-12)
        np.testing.assert_allclose(dx[:, order], dx2, atol=1e-12)
        for key in grad:
            np.testing.assert_allclose(grad[key], grad2[key], atol=1e-12)
        padded = np.concatenate([self.x, np.full((2, 3, 5), np.nan)], axis=1)
        mask = np.pad(self.mask, ((0, 0), (0, 3)))
        other, cache3 = self.block.forward(padded, mask)
        dx3, grad3 = self.block.backward(self.dy, cache3)
        np.testing.assert_allclose(value, other, atol=1e-12)
        np.testing.assert_allclose(dx, dx3[:, :4], atol=1e-12)
        for key in grad:
            np.testing.assert_allclose(grad[key], grad3[key], atol=1e-12)

    def test_empty_entities_rejected_and_interaction_nonzero(self):
        with self.assertRaises(ValueError):
            self.block.forward(self.x, np.zeros_like(self.mask))
        _, cache = self.block.forward(self.x, self.mask)
        _, gradients = self.block.backward(self.dy, cache)
        for key in ('q', 'k', 'v', 'project'):
            self.assertGreater(np.linalg.norm(gradients[key]), 1e-6)


if __name__ == '__main__':
    unittest.main()
