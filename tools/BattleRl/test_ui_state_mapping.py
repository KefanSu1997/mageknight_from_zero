import copy
import json
from pathlib import Path
import unittest
from ui_state_mapping import battle_projection,compare_unity_capture

ROOT=Path(__file__).resolve().parents[2]


class UiMappingTests(unittest.TestCase):
    def test_actual_card_payment_projection_and_missing_capture_denied(self):
        witness=json.loads((ROOT/'AutomationOutputs/BattleRl/20261007_policy_actual_gap_discovery/solvable_pool.json').read_text())['tasks'][0]
        trace=json.loads(Path(witness['evidence']['path']).read_text())['trace']
        row=next(r for r in trace if r['action']['Card']=='basic_card_009' and r['action']['Kind']=='play')
        before,after=battle_projection(row['before']),battle_projection(row['after'])
        self.assertEqual(before['tokens.blue'],'1')
        self.assertEqual(after['tokens.blue'],'0')
        self.assertEqual(after['tokens.red'],'3')
        self.assertNotIn('basic_card_009',after['hand.ids'])
        self.assertEqual(after['hero.id'],'Tovak')
        # Only deliberate test capture values, explicitly not Unity evidence.
        missing=compare_unity_capture(row['after'],{})
        self.assertTrue(missing and all(d['missing'] for d in missing))
        wrong=dict(after,**{'tokens.blue':'1'})
        self.assertEqual([d['key'] for d in compare_unity_capture(row['after'],wrong)],['tokens.blue'])

    def test_outside_is_not_mislabelled_as_a_unity_battle(self):
        state={'phase':'Outside','inBattle':False}
        with self.assertRaises(ValueError):
            battle_projection(state)


if __name__=='__main__':
    unittest.main()
