import copy
import json
from pathlib import Path
import unittest
from unittest.mock import patch
from task_feasibility import eligible,SEQUENCE_GOAL

ROOT=Path(__file__).resolve().parents[2]
DLL=ROOT/'tmp/battle_rl_explicit_build_tasks/BattleRl.dll'


class DataBoundCertificateTests(unittest.TestCase):
    def test_current_recertification_admitted_old_unbound_and_changed_data_denied(self):
        fresh=json.loads((ROOT/'AutomationOutputs/BattleRl/20261007_nine_task_data_bound_pool.json').read_text())['tasks'][0]
        old=json.loads((ROOT/'AutomationOutputs/BattleRl/20261007_nine_task_certified_pool.json').read_text())['tasks'][0]
        self.assertTrue(eligible(fresh,DLL,SEQUENCE_GOAL))
        self.assertFalse(eligible(old,DLL,SEQUENCE_GOAL))
        changed=copy.deepcopy(fresh['data_signature'])
        changed['files']['basic_card.json']='changed'
        with patch('task_feasibility.data_signature',return_value=changed):
            self.assertFalse(eligible(fresh,DLL,SEQUENCE_GOAL))


if __name__=='__main__':
    unittest.main()
