import unittest
import numpy as np
from mixed_task_pool import MixedTaskSampler, validate_pool


class MixedPoolTests(unittest.TestCase):
    def test_all_tasks_have_support_and_restore_sampling_history(self):
        rows=[dict(task_id=str(i),hero='Tovak' if i%2 else 'Goldyx',
                   status='solvable_witness' if i<2 else 'legal_reset_unknown',task={}) for i in range(5)]
        sampler=MixedTaskSampler(rows)
        p=sampler.probabilities()
        self.assertTrue(np.all(p>0))
        self.assertAlmostEqual(sum(p[:2]),.5)
        self.assertAlmostEqual(sum(p[2:]),.5)
        random=np.random.default_rng(42)
        for _ in range(100):sampler.select(random)
        restored=MixedTaskSampler(rows,sampler.state())
        np.testing.assert_array_equal(sampler.probabilities(),restored.probabilities())
        self.assertEqual(sampler.state(),restored.state())
        self.assertEqual(set(sampler.attempts),set(str(i) for i in range(5)))

    def test_heldout_partition_rejected_before_any_admission(self):
        with self.assertRaises(ValueError):validate_pool({'goal':'complete_declared_enemy_sequence','partition':'validation'},'unused')


if __name__=='__main__':unittest.main()
