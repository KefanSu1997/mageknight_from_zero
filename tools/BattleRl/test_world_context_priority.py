"""Context priority tests, not real task solvability proofs."""
import unittest
import numpy as np
from targeted_skill_floor_sampler import TargetedSkillFloorSampler, eligible_target


class WorldContextPriorityTests(unittest.TestCase):
    def task(self, world=False, units=2, skill='hero_skill_pool_002:07'):
        return dict(OriginalWorld=world, EnemyGroups=[['monster_007']],
            Build=dict(SkillIds=[skill], UnitIds=['army_000', 'army_001', 'army_002'][:units], AdditionalCards=[]))

    def test_direct_loyalty_slot_and_fluorescence_world_context_required(self):
        self.assertFalse(eligible_target(self.task(), 'hero_skill_pool_002:07'))
        self.assertFalse(eligible_target(self.task(True, 3), 'hero_skill_pool_002:07'))
        self.assertTrue(eligible_target(self.task(True, 2), 'hero_skill_pool_002:07'))
        self.assertFalse(eligible_target(self.task(skill='hero_skill_pool_005:05'), 'hero_skill_pool_005:05'))
        self.assertTrue(eligible_target(self.task(True, 3, 'hero_skill_pool_005:05'), 'hero_skill_pool_005:05'))

    def test_priority_preserves_all_roots_and_finishes_only_after_consumer(self):
        tasks = [self.task(), self.task(True, 3), self.task(True, 2)]
        rows = [dict(task_id=str(i), hero='Norowas', status='legal_reset_unknown',
                     task=dict(definition=task)) for i, task in enumerate(tasks)]
        sampler = TargetedSkillFloorSampler(rows, floor_mass=0., skill_floor_mass=.25,
            targets=['hero_skill_pool_002:07'])
        probability = sampler.probabilities()
        self.assertTrue(np.all(probability > 0))
        self.assertAlmostEqual(probability.sum(), 1.)
        self.assertGreater(probability[2], probability[0])
        self.assertEqual(probability[0], probability[1])
        sampler.events['skill_consumer:hero_skill_pool_002:07:loyalty_recruit'] = 1
        np.testing.assert_allclose(sampler.probabilities(), [1 / 3] * 3)

    def test_declared_focal_root_only_changes_priority_keeps_global_support(self):
        rows = [dict(task_id=str(i), hero='Norowas', status='legal_reset_unknown',
                     task=dict(definition=self.task(True, 2)), world_consumer_priority=(i == 0)) for i in range(4)]
        sampler = TargetedSkillFloorSampler(rows, floor_mass=0., skill_floor_mass=.25,
            targets=['hero_skill_pool_002:07'])
        p = sampler.probabilities()
        np.testing.assert_allclose(p, [.4375, .1875, .1875, .1875])
        rows[0]['task']['definition']['OriginalWorld'] = False
        self.assertLess(sampler.probabilities()[0], sampler.probabilities()[1])


if __name__ == '__main__':
    unittest.main()
