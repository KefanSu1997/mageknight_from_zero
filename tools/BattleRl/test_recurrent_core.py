import unittest
import numpy as np
from recurrent_core import EncoderGRU


class RecurrentCoreTests(unittest.TestCase):
    def test_history_changes_output_and_episode_reset_removes_history(self):
        net = EncoderGRU(4, (5, 4, 3), 3)
        x = np.random.default_rng(3).normal(size=(4, 1, 4))
        changed = x.copy()
        changed[0] += 2
        a, _, _ = net.forward(x)
        b, _, _ = net.forward(changed)
        self.assertGreater(np.abs(a[1] - b[1]).max(), 1e-7)
        starts = np.array([[True], [False], [True], [False]])
        a, _, _ = net.forward(x, episode_starts=starts)
        b, _, _ = net.forward(changed, episode_starts=starts)
        np.testing.assert_array_equal(a[2:], b[2:])

    def test_chunk_carry_equals_unbroken_sequence(self):
        net = EncoderGRU(4, (5, 4, 3), 3)
        x = np.random.default_rng(5).normal(size=(6, 2, 4))
        full, final, _ = net.forward(x)
        left, memory, _ = net.forward(x[:3])
        right, carried, _ = net.forward(x[3:], initial_memory=memory)
        np.testing.assert_array_equal(full, np.concatenate([left, right]))
        np.testing.assert_array_equal(final, carried)

    def test_all_parameter_groups_and_initial_memory_sequence_gradient(self):
        net = EncoderGRU(4, (5, 4, 3), 3)
        random = np.random.default_rng(11)
        x = random.normal(size=(4, 2, 4)) * .3
        h = random.normal(size=(2, 3)) * .2
        starts = np.zeros((4, 2), dtype=bool)
        starts[2, 0] = True
        weights = random.normal(size=(4, 2, 3))
        _, _, cache = net.forward(x, h, starts)
        dx, dh, grads = net.backward(weights, cache)
        def loss():
            return (net.forward(x, h, starts)[0] * weights).sum()
        epsilon = 1e-6
        for name, parameter in net.parameters.items():
            index = tuple(0 for _ in parameter.shape)
            original = parameter[index]
            parameter[index] = original + epsilon
            plus = loss()
            parameter[index] = original - epsilon
            minus = loss()
            parameter[index] = original
            self.assertAlmostEqual((plus - minus) / (2 * epsilon), grads[name][index], places=7, msg=name)
        for array, gradient in ((x, dx), (h, dh)):
            index = tuple(0 for _ in array.shape)
            original = array[index]
            array[index] = original + epsilon
            plus = loss()
            array[index] = original - epsilon
            minus = loss()
            array[index] = original
            self.assertAlmostEqual((plus - minus) / (2 * epsilon), gradient[index], places=7)


if __name__ == "__main__":
    unittest.main()
