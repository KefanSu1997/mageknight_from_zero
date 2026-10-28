import copy
import os
from pathlib import Path
import unittest
from train_ppo import Rules

ROOT = Path(__file__).resolve().parents[2]


def definition():
    return dict(HeroId='Arythea', OriginalOpening=False, RandomSeed=7, InventorySeed=1,
                ShuffleSeed=11, ManaSeed=13, SupplySeed=17, ProgressionSeed=19, EffectSeed=23,
                SummonSeed=29, EnemyGroups=[['monster_032']], Build=dict(Family='elemental', Fame=8,
                    AdditionalCards=['advanced_card_002', 'magic_009', 'items_006'],
                    UnitIds=['army_005', 'army_2_005'], SkillIds=['hero_skill_pool_000:06'],
                    Crystals={'Red': 1, 'Blue': 2, 'Green': 1, 'White': 1}, InitialWounds=3))


class ExplicitBuildTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        os.environ.update(BATTLE_RL_DLL=str(ROOT / 'tmp/battle_rl_explicit_build_tasks/BattleRl.dll'),
                          BATTLE_RL_PROTOCOL='--serve-journey-artifacts', BATTLE_RL_NATIVE_PAYMENTS='1',
                          BATTLE_RL_NATIVE_CONVERSIONS='1', BATTLE_RL_NATIVE_ENERGY_TARGETS='1', BATTLE_RL_ORIGINAL_OPENINGS='1')
        cls.rules = Rules()

    @classmethod
    def tearDownClass(cls):
        cls.rules.close()

    def test_actual_original_inventory_owned_skills_slots_and_wounds(self):
        s = self.rules.reset_task(definition())['state']
        bag = [c['id'] for cards in s['zones'].values() for c in cards]
        self.assertEqual(len(bag), 22)  # Actual starting16 + originalextras3 + wounds3.
        for original in definition()['Build']['AdditionalCards']:
            self.assertEqual(bag.count(original), 1)
        self.assertEqual(s['skills'], ['hero_skill_pool_000:06'])
        self.assertEqual([u['id'] for u in s['units']], ['army_005', 'army_2_005'])
        self.assertEqual(s['level'], 3)
        self.assertEqual(s['commandSlots'], 2)
        self.assertEqual(s['totalWounds'], 3)
        self.assertEqual(s['crystals'], definition()['Build']['Crystals'])

    def test_explicit_cards_do_not_depend_on_inventory_seed_or_enemy(self):
        a = self.rules.reset_task(definition())['state']
        b = self.rules.reset_task(definition() | {'InventorySeed': 1001, 'EnemyGroups': [['monster_033']]})['state']
        for field in ('hand', 'zones', 'units', 'skills', 'crystals', 'level'):
            self.assertEqual(a[field], b[field], field)
        self.assertEqual(b['enemies'][0]['id'], 'monster_033')

    def test_formal_familiar_round_upkeep_consumes_actual_crystal(self):
        d = definition()
        d['Build'].update(UnitIds=['army_2_004'], Crystals={'Green': 2}, InitialWounds=0)
        s = self.rules.reset_task(d)['state']
        self.assertEqual(s['crystals'], {'Green': 1})
        self.assertEqual(s['units'][0]['attunement'], 'Green')
        self.assertFalse(s['units'][0]['upkeep'])

    def test_foreign_skills_unique_cards_slots_level_and_mana_rejected(self):
        for field, value in [('SkillIds', ['hero_skill_pool_007:00']),
                             ('AdditionalCards', ['magic_009', 'magic_009']),
                             ('AdditionalCards', ['basic_card_027']),
                             ('Crystals', {'Gold': 1}), ('Crystals', {'Red': 4}), ('Crystals', {'0': 1}),
                             ('UnitIds', ['army_001', 'army_002', 'army_003'])]:
            d = definition()
            d['Build'][field] = value
            with self.assertRaises(RuntimeError, msg=str((field, value))):
                self.rules.reset_task(d)
        d = definition()
        d['Build']['Fame'] = 0
        with self.assertRaises(RuntimeError):
            self.rules.reset_task(d)  # Level1 cannot own the declared skill.
        with self.assertRaises(RuntimeError):
            self.rules.reset_task(definition() | {'OriginalOpening': True})


if __name__ == '__main__':
    unittest.main()
