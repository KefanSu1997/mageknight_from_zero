import copy
import json
from pathlib import Path
import unittest
from compare_unity_state_report import compare_report

ROOT=Path(__file__).resolve().parents[2]
REPORT=ROOT/'AutomationOutputs/BattleRl/20261007_unity_phase_reset/report.json'


class ActualUnityReportTests(unittest.TestCase):
    def test_actual_capture_matches_and_corrupt_ui_missing_raw_failed_click_rejected(self):
        actual=json.loads(REPORT.read_text())
        self.assertEqual(compare_report(actual)['status'],'passed')
        changed=copy.deepcopy(actual)
        row=next(r for r in changed['steps'][-1]['afterState'] if r['key']=='hand')
        row['value']='999'
        self.assertEqual(compare_report(changed)['status'],'failed')
        changed=copy.deepcopy(actual)
        changed['steps'][0]['beforeState']=[r for r in changed['steps'][0]['beforeState'] if r['key']!='rl.sharedRuleState']
        self.assertEqual(compare_report(changed)['status'],'failed')
        changed=copy.deepcopy(actual)
        changed['steps'][0]['success']=False
        self.assertEqual(compare_report(changed)['status'],'failed')


if __name__=='__main__':
    unittest.main()
