import copy
import json
from pathlib import Path
import unittest
from coverage_tasks import drafts, requirements, greedy_cover, split, planned_keys


class CoverageTaskDesignTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.inventory = json.loads((Path(__file__).resolve().parents[2] /
                                    'AutomationConfigs/rl_full_coverage_inventory.json').read_text())

    def test_related_random_and_order_variations_do_not_cross_splits(self):
        task = drafts(self.inventory)[0]['task_definition']
        changed = copy.deepcopy(task)
        changed['RandomSeed'] += 1
        changed['ShuffleSeed'] += 2
        changed['Build']['AdditionalCards'].reverse()
        changed['EnemyGroups'].reverse()
        self.assertEqual(split(task), split(changed))

    def test_all_hero_enemy_pairs_are_planned_but_not_certified(self):
        candidates = drafts(self.inventory)
        planned = set().union(*(set(c['planned_keys']) for c in candidates))
        pairs = {k for k in requirements(self.inventory) if k.startswith('hero_enemy:')}
        self.assertEqual(len(pairs), 483)
        self.assertTrue(pairs <= planned)
        self.assertTrue(all(c['status']=='unknown' and c['excluded_from_victory_pool'] for c in candidates))
        self.assertEqual(candidates, drafts(self.inventory))

    def test_greedy_selection_keeps_missing_and_withheld_compositions_visible(self):
        rows = [c for c in drafts(self.inventory) if c['split']=='train']
        desired = requirements(self.inventory)
        selected, missing = greedy_cover(rows, desired)
        covered = set().union(*(set(c['planned_keys']) for c in selected))
        self.assertEqual(set(missing), desired - covered)
        self.assertTrue(all(c['split']=='train' for c in selected))
        # Current original hero starters do not contain these definitions: do not
        # silently assign another hero's basic card to fabricate coverage.
        self.assertIn('card_mode:basic_card_011:base', missing)
        self.assertIn('card_mode:basic_card_025:strong', missing)
        self.assertFalse(any(k.startswith('hero_enemy:') for k in missing))


if __name__ == '__main__':
    unittest.main()
