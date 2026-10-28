import copy
import json
from pathlib import Path
import unittest
from shared_planning_revisit_reward import SharedPlanningRevisitReward


class SharedPlanningTests(unittest.TestCase):
    def test_real_failed_sequence_only_changes_training_cost(self):
        root=Path(__file__).resolve().parents[2]
        trace=json.loads((root/'AutomationOutputs/BattleRl/20261008_shared_skill_floor_training_16/episode_953.json').read_text())['trace']
        original=copy.deepcopy(trace);enabled=SharedPlanningRevisitReward(.25);disabled=SharedPlanningRevisitReward()
        penalties=[enabled.step(r['action'],r['before'],r['after']) for r in trace]
        self.assertEqual(penalties[0],0)
        self.assertTrue(any(x==-.25 for x in penalties))
        self.assertTrue(all(disabled.step(r['action'],r['before'],r['after'])==0 for r in trace))
        self.assertEqual(trace,original)

    def test_new_color_and_actual_consumer_reset_not_penalized(self):
        def state(color):return dict(phase='End',sharedSkills=dict(enhancementColor=color))
        reward=SharedPlanningRevisitReward(.25)
        self.assertEqual(reward.step(dict(Kind='sharedArm'),state(None),state('Blue')),0)
        self.assertEqual(reward.step(dict(Kind='sharedArm'),state('Blue'),state('Red')),0)
        self.assertEqual(reward.step(dict(Kind='sharedCancel'),state('Red'),state(None)),-.25)
        self.assertEqual(reward.step(dict(Kind='play'),state('Blue'),state(None)),0)
        self.assertEqual(reward.step(dict(Kind='sharedArm'),state(None),state('Blue')),0)


if __name__=='__main__':unittest.main()
