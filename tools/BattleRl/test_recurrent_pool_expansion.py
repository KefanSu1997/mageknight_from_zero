import copy
import json
from pathlib import Path
import unittest
from expand_recurrent_task_pool import validate_expansion

ROOT=Path(__file__).resolve().parents[2]
DLL=ROOT/'tmp/battle_rl_explicit_build_tasks/BattleRl.dll'


class PoolExpansionTests(unittest.TestCase):
    def setUp(self):
        self.old=json.loads((ROOT/'AutomationOutputs/BattleRl/20261007_discovery_cached_candidates/solvable_pool.json').read_text())['tasks']
        self.new=json.loads((ROOT/'AutomationOutputs/BattleRl/20261007_discovery_two_groups/solvable_pool.json').read_text())['tasks']
        current={c['task_id']:c for c in json.loads((ROOT/'AutomationOutputs/BattleRl/20261007_nine_task_data_bound_pool.json').read_text())['tasks']}
        self.old=[current[c['task_id']] for c in self.old]
        self.new=[current[c['task_id']] for c in self.new]

    def test_current_complete_certificates_append_three_tasks(self):
        self.assertEqual(len(validate_expansion(self.old,self.new,DLL)),3)

    def test_removal_duplicate_or_unproved_addition_rejected(self):
        for pool in (self.new[1:],self.new+[self.new[0]]):
            with self.assertRaises(ValueError):
                validate_expansion(self.old,pool,DLL)
        corrupted=copy.deepcopy(self.new)
        corrupted[-1]['status']='unknown'
        with self.assertRaises(ValueError):
            validate_expansion(self.old,corrupted,DLL)
        changed=copy.deepcopy(self.new)
        changed[0]['task']['definition']['ShuffleSeed']+=1
        with self.assertRaises(ValueError):
            validate_expansion(self.old,changed,DLL)


if __name__=='__main__':
    unittest.main()
