import copy
import json
from pathlib import Path
import unittest
from certified_task_sampler import actual_events


class ShapeshiftEventTests(unittest.TestCase):
    def test_real_material_slot_does_not_count_discarded_donor_as_play(self):
        root=Path(__file__).resolve().parents[2]
        rows=json.loads((root/'AutomationOutputs/BattleRl/20261008_shapeshift_consumer_checks_v2/actual_cases.json').read_text())
        for case in rows:
            step=next(r for r in case['trace'] if r['action']['Kind']=='skillShapeshift')
            events=[key for _,key in actual_events([step])]
            self.assertIn(f'card_mode:{case["card"]}:shapeshift_{"strong" if case["enhanced"] else "base"}',events)
            self.assertIn('skill_invocation:hero_skill_pool_001:06:0',events)
            self.assertEqual(sum(key.startswith('card_mode:') for key in events),1)
            bad=copy.deepcopy(step);bad['after']['zones']['played']=bad['before']['zones']['played'][:]
            self.assertFalse([key for _,key in actual_events([bad]) if key.startswith('card_mode:')])


if __name__=='__main__':unittest.main()
