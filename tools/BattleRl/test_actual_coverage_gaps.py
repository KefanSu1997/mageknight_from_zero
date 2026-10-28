import json
from pathlib import Path
import unittest
import copy
from actual_coverage_gaps import coverage,rank_drafts,validate_actual_event_source

ROOT=Path(__file__).resolve().parents[2]


class CoverageGapTests(unittest.TestCase):
    def setUp(self):
        self.inventory=json.loads((ROOT/'AutomationConfigs/rl_full_coverage_inventory.json').read_text())
        self.modes=json.loads((ROOT/'AutomationOutputs/BattleRl/20261007_frozen_original_card_modes.json').read_text())

    def test_actual_native_nonprefix_modes_and_skill_colon_ids(self):
        report=coverage({'card_mode:basic_card_001:heal':1,'card_mode:basic_card_002:red':2,
                         'card_mode:basic_card_001:strong_draw':1,'skill_invocation:hero_skill_pool_001:01:0':1},
                        self.inventory,self.modes)
        self.assertEqual(report['invoked_modes'],['card_mode:basic_card_001:base','card_mode:basic_card_001:strong','card_mode:basic_card_002:base'])
        self.assertEqual(report['invoked_skills'],['own_skill:hero_skill_pool_001:01'])
        self.assertEqual(len(report['source_eligibility_gaps']),4)

    def test_empty_or_unresolved_events_never_inherit_catalog_coverage(self):
        report=coverage({'card_mode:basic_card_001:invented':1,'card_mode:basic_card_001:heal':0},self.inventory,self.modes)
        self.assertFalse(report['invoked_modes'])
        self.assertEqual(len(report['missing_modes']),242)
        self.assertEqual(len(report['unresolved_choice_events']),1)
        rows=[dict(task_id='withheld',split='test',planned_keys=report['missing_modes'],estimated_cost=1,task_definition={}),
              dict(task_id='train',split='train',planned_keys=report['missing_modes'],estimated_cost=2,task_definition={})]
        self.assertEqual([r['draft_id'] for r in rank_drafts(rows,report)],['train'])

    def test_event_history_must_match_actual_saved_checkpoint(self):
        source={'events':{'card_mode:basic_card_001:heal':1}}
        header={'metadata':{'rules_sha256':'frozen'},'replay_state':{'sampler':copy.deepcopy(source)}}
        validate_actual_event_source(source,header,'frozen')
        changed=copy.deepcopy(source)
        changed['events']['card_mode:basic_card_001:heal']+=1
        with self.assertRaises(ValueError):
            validate_actual_event_source(changed,header,'frozen')
        with self.assertRaises(ValueError):
            validate_actual_event_source(source,header,'different')


if __name__=='__main__':
    unittest.main()
