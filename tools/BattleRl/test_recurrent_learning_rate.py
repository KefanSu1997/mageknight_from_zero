import unittest
import numpy as np
from recurrent_checkpoint import Adam
from train_recurrent_ppo import override_learning_rate


class LearningRateTests(unittest.TestCase):
    def test_override_preserves_saved_parameters_and_all_momentum(self):
        optimizer=Adam({'w':np.array([1.,2.])})
        optimizer.update({'w':np.array([.2,-.3])})
        saved=[optimizer.parameters['w'].copy(),optimizer.first['w'].copy(),optimizer.second['w'].copy()]
        step=optimizer.steps
        self.assertIsNone(override_learning_rate(optimizer,None))
        change=override_learning_rate(optimizer,.0002)
        self.assertEqual(change['previous'],.00005)
        self.assertEqual(optimizer.learning_rate,.0002)
        for before,after in zip(saved,[optimizer.parameters['w'],optimizer.first['w'],optimizer.second['w']]):
            np.testing.assert_array_equal(before,after)
        self.assertEqual(optimizer.steps,step)

    def test_invalid_override_does_not_mutate_optimizer(self):
        optimizer=Adam({'w':np.zeros(1)})
        for value in (0,-1,float('nan'),float('inf')):
            with self.assertRaises(ValueError):
                override_learning_rate(optimizer,value)
        self.assertEqual(optimizer.learning_rate,.00005)


if __name__=='__main__':
    unittest.main()
