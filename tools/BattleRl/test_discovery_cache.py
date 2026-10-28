import copy
import json
from pathlib import Path
import unittest
from discovery_cache import read_prior, same_budget_unknown
from task_feasibility import digest, file_hash

ROOT = Path(__file__).resolve().parents[2]
DLL = ROOT/'tmp/battle_rl_explicit_build_tasks/BattleRl.dll'
POOL = ROOT/'AutomationOutputs/BattleRl/20261007_nine_task_data_bound_pool.json'


class DiscoveryCacheTests(unittest.TestCase):
    def test_current_actual_certificate_reused_and_deduplicated(self):
        certs, attempts, rejected = read_prior([POOL, POOL], DLL)
        self.assertEqual(len(certs), 9)
        self.assertFalse(rejected)
        c = next(iter(certs.values()))
        self.assertIn(digest(c['task']['definition']), certs)

    def test_unknown_only_defers_identical_rules_task_code_and_budget(self):
        task = next(iter(read_prior([POOL], DLL)[0].values()))['task']['definition']
        budget = dict(rule_steps=1200, branch_limit=4, beam_width=4, seconds=30)
        attempt = dict(status='unknown', task_definition=task, rules_sha256=file_hash(DLL),
                       search_code_sha256='search-v1', budget=budget, impossibility_proved=False)
        self.assertTrue(same_budget_unknown([attempt], task, DLL, 'search-v1', budget))
        self.assertFalse(same_budget_unknown([attempt], task, DLL, 'search-v2', budget))
        self.assertFalse(same_budget_unknown([attempt], task, DLL, 'search-v1', dict(budget, seconds=60)))
        self.assertFalse(same_budget_unknown([dict(attempt,initial_prefix_operations=12)],task,DLL,'search-v1',budget))
        changed = copy.deepcopy(task)
        changed['ShuffleSeed'] += 1
        self.assertFalse(same_budget_unknown([attempt], changed, DLL, 'search-v1', budget))
        for field in ('rules_sha256', 'task_definition', 'impossibility_proved'):
            incomplete = dict(attempt)
            incomplete.pop(field)
            self.assertFalse(same_budget_unknown([incomplete], task, DLL, 'search-v1', budget))


if __name__ == '__main__':
    unittest.main()
