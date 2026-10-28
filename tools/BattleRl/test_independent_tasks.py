"""Independent factors exercised through the actual original-rule adapter."""
import copy
import json
import os
from pathlib import Path
import unittest

from train_ppo import Rules

ROOT = Path(__file__).resolve().parents[2]
NEW_DLL = Path(os.environ.get('BATTLE_RL_INDEPENDENT_TEST_DLL',
                              str(ROOT / 'tmp/battle_rl_independent_tasks/BattleRl.dll')))
OLD_DLL = ROOT / 'tmp/battle_rl_v94_effective_fortification/BattleRl.dll'


def configure(path):
    os.environ.update(BATTLE_RL_DLL=str(path), BATTLE_RL_PROTOCOL='--serve-journey-artifacts',
                      BATTLE_RL_NATIVE_PAYMENTS='1', BATTLE_RL_NATIVE_CONVERSIONS='1',
                      BATTLE_RL_NATIVE_ENERGY_TARGETS='1', BATTLE_RL_ORIGINAL_OPENINGS='1')


def task():
    return dict(HeroId='Arythea', OriginalOpening=False, RandomSeed=7, InventorySeed=1,
                ShuffleSeed=11, ManaSeed=13, SupplySeed=17, ProgressionSeed=19,
                EffectSeed=23, SummonSeed=29, EnemyGroups=[['monster_032'], ['monster_016']])


def bag(state, prefixes=None):
    cards = [c['id'] for zone in state['zones'].values() for c in zone]
    return sorted(c for c in cards if prefixes is None or any(c.startswith(p) for p in prefixes))


class IndependentTaskTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        configure(NEW_DLL)
        cls.rules = Rules()

    @classmethod
    def tearDownClass(cls):
        cls.rules.close()

    def test_legacy_original_prefix_features_masks_and_rules_unchanged(self):
        configure(OLD_DLL)
        old = Rules()
        try:
            source = json.loads((ROOT / 'AutomationOutputs/BattleRl/20261006_v95_continuous_ppo/audit_18432_3.json').read_text())
            self.assertEqual(old.actions, self.rules.actions)
            a, b = old.reset(source['seed']), self.rules.reset(source['seed'])
            keys = {v['Key']: i for i, v in enumerate(old.actions)}
            def compare(original, extended):
                self.assertEqual(original['features'], extended['features'][:-3])
                self.assertEqual(extended['features'][-3:], [0, 0, 0])
                self.assertEqual({k:v for k,v in original.items() if k!='features'},
                                 {k:v for k,v in extended.items() if k!='features'})
            for row in source['trace'][:12]:
                compare(a, b)
                a, b = old.step(keys[row['action']['Key']]), self.rules.step(keys[row['action']['Key']])
            compare(a, b)
            with self.assertRaisesRegex(ValueError, 'do not fall back'):
                old.reset_task(task())
        finally:
            old.close()
            configure(NEW_DLL)

    def test_enemy_change_keeps_actual_inventory_random_streams(self):
        base = task()
        changed = base | {'EnemyGroups': [['monster_033'], ['monster_039']]}
        a, b = self.rules.reset_task(base)['state'], self.rules.reset_task(changed)['state']
        for key in ['hero', 'hand', 'zones', 'units', 'skills', 'crystals', 'auditSourceOrder', 'advancedOffer']:
            self.assertEqual(a[key], b[key], key)
        self.assertEqual(b['enemies'][0]['id'], 'monster_033')

    def test_inventory_and_hero_vary_without_enemy_coupling(self):
        base = task()
        a = self.rules.reset_task(base)['state']
        b = self.rules.reset_task(base | {'InventorySeed': 2})['state']
        self.assertEqual(a['enemies'], b['enemies'])
        self.assertNotEqual(bag(a), bag(b))
        c = self.rules.reset_task(base | {'HeroId': 'Tovak'})['state']
        self.assertEqual(a['enemies'], c['enemies'])
        self.assertEqual(bag(a, ['advanced_', 'magic_', 'items_']), bag(c, ['advanced_', 'magic_', 'items_']))
        self.assertEqual(a['units'], c['units'])
        self.assertEqual(c['hero'], 'Tovak')

    def test_shuffle_changes_order_not_owned_card_composition(self):
        a = self.rules.reset_task(task())['state']
        b = self.rules.reset_task(task() | {'ShuffleSeed': 37})['state']
        self.assertEqual(bag(a), bag(b))
        self.assertEqual(a['units'], b['units'])
        self.assertEqual(a['enemies'], b['enemies'])
        self.assertNotEqual(a['hand'], b['hand'])

    def test_invalid_or_unsupported_factors_rejected(self):
        for changed in [task() | {'HeroId': 'invented'}, task() | {'ManaSeed': -1},
                        task() | {'EnemyGroups': [['monster_032', 'monster_032']]},
                        task() | {'EnemyGroups': [['invented']]}, task() | {'DayPart': 'Night'}]:
            with self.assertRaises(RuntimeError):
                self.rules.reset_task(changed)

    def test_formal_next_turn_advances_group_without_rebuilding_player(self):
        seed = 488901
        source = json.loads((ROOT / 'AutomationOutputs/BattleRl/20261006_v95_continuous_ppo/audit_18432_3.json').read_text())
        definition = dict(HeroId=source['trace'][0]['before']['hero'], OriginalOpening=False, RandomSeed=seed,
                          InventorySeed=seed, ShuffleSeed=seed, ManaSeed=seed, SupplySeed=seed,
                          ProgressionSeed=seed, EffectSeed=seed ^ 0x192abc,
                          SummonSeed=seed ^ 0x49b023, EnemyGroups=[['monster_016'], ['monster_003']])
        keys = {a['Key']: i for i, a in enumerate(self.rules.actions)}
        current = self.rules.reset_task(definition)
        self.assertEqual(current['features'][-3:], [1, 2/16, 0])
        for row in source['trace'][:12]:
            current = self.rules.step(keys[row['action']['Key']])
        before = current['state']
        self.assertTrue(before['won'])
        current = self.rules.step(keys['nextTurn:::0:0:0'])
        after = current['state']
        self.assertEqual(after['taskProgress']['encounterIndex'], 1)
        self.assertEqual(current['features'][-3:], [1, 2/16, 1/16])
        self.assertEqual(after['enemies'][0]['id'], 'monster_003')
        self.assertEqual(bag(before), bag(after))
        self.assertEqual(before['units'], after['units'])
        self.assertEqual(before['totalWounds'], after['totalWounds'])
        self.assertEqual(before['fame'], after['fame'])
        self.assertFalse(current['done'])
        single = definition | {'EnemyGroups': [['monster_016']]}
        current = self.rules.reset_task(single)
        for row in source['trace'][:12]:
            current = self.rules.step(keys[row['action']['Key']])
        current = self.rules.step(keys['nextTurn:::0:0:0'])
        self.assertTrue(current['done'])
        self.assertEqual(current['state']['stopReason'], 'task_sequence_complete')
        self.assertTrue(current['state']['taskProgress']['sequenceCompleted'])


if __name__ == '__main__':
    unittest.main()
