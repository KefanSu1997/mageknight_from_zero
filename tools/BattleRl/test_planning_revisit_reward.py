import unittest
from planning_revisit_reward import PlanningRevisitReward


class PlanningRevisitTests(unittest.TestCase):
    def test_first_choice_and_revisit_then_commit_reset(self):
        empty=dict(phase='AssignDamage',unitDamagePlan=[])
        chosen=dict(phase='AssignDamage',unitDamagePlan=[dict(target=0,attack=0,units=['army_000'])])
        reward=PlanningRevisitReward(.25)
        self.assertEqual(reward.step({'Kind':'unitDamage'},empty,chosen),0)
        self.assertEqual(reward.step({'Kind':'unitDamage'},chosen,empty),-.25)
        self.assertEqual(reward.step({'Kind':'unitDamage'},empty,chosen),-.25)
        self.assertEqual(reward.step({'Kind':'advance'},chosen,dict(phase='Melee')),0)
        self.assertEqual(reward.step({'Kind':'unitDamage'},empty,chosen),0)

    def test_disabled_and_invalid_cost(self):
        self.assertEqual(PlanningRevisitReward().step({'Kind':'unitDamage'}, {},{}),0)
        for cost in (-1,float('nan'),float('inf')):
            with self.assertRaises(ValueError): PlanningRevisitReward(cost)


if __name__=='__main__': unittest.main()
