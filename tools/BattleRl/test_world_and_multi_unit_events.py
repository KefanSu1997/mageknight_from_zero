"""Affected collector commits, not game rule or model acceptance."""
import copy
import unittest
from certified_task_sampler import actual_events


class WorldAndMultiUnitEventsTests(unittest.TestCase):
    def state(self):
        return dict(hand=[], units=[], usedSkillsTurn=[], usedSkillsRound=[], flippedSkills=[], inBattle=False)

    def row(self, before, after, kind, card, choice='0'):
        return dict(before=before, after=after, action=dict(Kind=kind, Card=card, Choice=choice, Option=0))

    def test_world_flight_requires_actual_round_marker(self):
        a = self.state(); b = copy.deepcopy(a)
        self.assertEqual(list(actual_events([self.row(a, b, 'worldFly', 'hero_skill_pool_005:06')])), [])
        b['usedSkillsRound'] = ['hero_skill_pool_005:06']
        self.assertEqual(list(actual_events([self.row(a, b, 'worldFly', 'hero_skill_pool_005:06')])), [(0, 'skill_invocation:hero_skill_pool_005:06:0')])
        self.assertEqual(list(actual_events([self.row(b, b, 'worldFly', 'hero_skill_pool_005:06')])), [])

    def test_second_unit_ability_requires_matching_new_actual_marker(self):
        a = self.state(); a['units'] = [dict(id='army_005', ready=False, usedAbilities=[2])]
        b = copy.deepcopy(a); b['units'][0]['usedAbilities'].append(3)
        self.assertEqual(list(actual_events([self.row(a, b, 'unit', 'army_005', '3')])), [(0, 'unit_invocation:army_005:3:0')])
        self.assertEqual(list(actual_events([self.row(a, a, 'unit', 'army_005', '3')])), [])
        self.assertEqual(list(actual_events([self.row(a, b, 'unit', 'army_005', '1')])), [])

    def test_loyalty_selection_not_use_actual_recruit_is_consumer(self):
        a = self.state(); a['world'] = dict(loyaltySelected=True, loyaltyAvailable=True, loyaltyBound=None)
        b = copy.deepcopy(a)
        self.assertEqual(list(actual_events([self.row(a, b, 'worldRecruit', 'army_2_006')])), [])
        b['units'].append(dict(id='army_2_006', ready=True))
        b['world'].update(loyaltyBound='army_2_006', loyaltyAvailable=False, loyaltySelected=False)
        self.assertEqual(list(actual_events([self.row(a, b, 'worldRecruit', 'army_2_006')])), [(0, 'skill_consumer:hero_skill_pool_002:07:loyalty_recruit')])
        self.assertEqual(list(actual_events([self.row(a, b, 'worldLoyaltySelect', 'army_2_006')])), [])


if __name__ == '__main__':
    unittest.main()
