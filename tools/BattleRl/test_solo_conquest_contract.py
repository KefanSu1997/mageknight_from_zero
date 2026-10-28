import unittest
from dataclasses import replace
from solo_conquest_contract import ScoreState, RewardLedger, score, split_worlds


class ConquestContractTests(unittest.TestCase):
    def setUp(self):
        self.empty = ScoreState(0, 0, 0, 0, 0, (), 0, 0, 0, 0)

    def test_physical_inventory_scoring(self):
        s = replace(self.empty, fame=42, spells=2, advanced_actions=3,
                    artifacts=1, crystals=5, units=((1, True), (3, True), (4, False)),
                    fortified_shields=2, adventure_shields=3, deck_wounds=4,
                    conquered_cities=2)
        self.assertEqual(sum(score(s).values()), 95)
        self.assertEqual(sum(score(s, terminal=True, round_index=4,
                                   dummy_cards=3, player_announced=False).values()), 163)

    def test_wound_heal_cycle_does_not_create_return(self):
        ledger = RewardLedger(self.empty)
        wounded = replace(self.empty, deck_wounds=3)
        rewards = [ledger.advance(wounded)[0], ledger.advance(self.empty)[0]]
        self.assertAlmostEqual(sum(rewards), 0)
        self.assertEqual(ledger.advance(self.empty)[0], 0)

    def test_terminal_bonus_once_and_telescoping(self):
        ledger = RewardLedger(self.empty)
        one = replace(self.empty, fame=8, conquered_cities=1)
        two = replace(one, fame=20, conquered_cities=2)
        total = ledger.advance(one)[0] + ledger.advance(two)[0]
        total += ledger.advance(two, terminal=True, round_index=6,
                                dummy_cards=0, player_announced=True)[0]
        self.assertAlmostEqual(total, .55)
        with self.assertRaises(ValueError):
            ledger.advance(two, terminal=True, round_index=6,
                           dummy_cards=0, player_announced=True)

    def test_timeout_is_not_early_completion(self):
        with self.assertRaises(ValueError):
            score(self.empty, terminal=True, round_index=2,
                  dummy_cards=5, player_announced=False)

    def test_seed_only_worlds_rejected(self):
        with self.assertRaises(ValueError):
            split_worlds([{'seed': i} for i in range(1200)])

    def test_family_split_is_disjoint_order_independent(self):
        # Synthetic schema fixtures only: these are NOT original playable maps.
        worlds = [dict(map_tiles=['fixture', i], city_colors=['red', 'blue'],
                       city_levels=[5, 8], enemy_supply=['fixture_enemy'],
                       offer_supply=['fixture_offer'], dummy_setup={'hero': 'fixture'},ruins=['fixture'],initial_mana=['fixture'],
                       rules_sha256='fixture_rules', catalog_sha256='fixture_catalog')
                  for i in range(1200)]
        a, b = split_worlds(worlds), split_worlds(list(reversed(worlds)))
        self.assertEqual(a, b)
        self.assertEqual([len(a[k]) for k in ('train', 'validation', 'test')], [1000, 100, 100])
        ids = [row['family_sha256'] for group in a.values() for row in group]
        self.assertEqual(len(set(ids)), 1200)
        with self.assertRaises(ValueError):
            split_worlds(worlds[:-1] + [worlds[0]])


if __name__ == '__main__':
    unittest.main()
