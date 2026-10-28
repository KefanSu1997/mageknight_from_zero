import json
import os
from pathlib import Path
import tempfile
import unittest
from discover_task_witnesses import discover
from task_feasibility import SEQUENCE_GOAL, eligible
from train_ppo import Rules

ROOT = Path(__file__).resolve().parents[2]
DLL = ROOT / 'tmp/battle_rl_explicit_build_tasks/BattleRl.dll'


class DiscoveryBudgetTests(unittest.TestCase):
    def test_actual_discovered_witness_is_complete_and_not_teacher_data(self):
        pool = json.loads((ROOT / 'AutomationOutputs/BattleRl/20261007_nine_task_data_bound_pool.json').read_text())
        certificate = next(c for c in pool['tasks'] if '20261007_discovery_complete_priority' in c['evidence']['path'])
        self.assertTrue(eligible(certificate, DLL, SEQUENCE_GOAL))
        source = json.loads(Path(certificate['evidence']['path']).read_text())
        self.assertEqual(source['trace'][-1]['after']['stopReason'], 'task_sequence_complete')
        result = json.loads((Path(certificate['evidence']['path']).parent/'result.json').read_text())
        self.assertFalse(result['teacher_training_data'])

    def test_zero_search_budget_is_unknown_not_impossible(self):
        os.environ.update(BATTLE_RL_DLL=str(DLL), BATTLE_RL_PROTOCOL='--serve-journey-artifacts',
                          BATTLE_RL_NATIVE_PAYMENTS='1', BATTLE_RL_NATIVE_CONVERSIONS='1',
                          BATTLE_RL_NATIVE_ENERGY_TARGETS='1', BATTLE_RL_ORIGINAL_OPENINGS='1')
        rules = Rules()
        rows = json.loads((ROOT/'AutomationOutputs/BattleRl/20261007_coverage_task_drafts/tasks.json').read_text())
        try:
            with tempfile.TemporaryDirectory() as directory:
                result = discover(rules, rows['candidates'][0]['task_definition'], DLL, directory,
                                  rule_step_budget=0, seconds=0)
                self.assertEqual(result['status'], 'unknown')
                self.assertFalse(result['impossibility_proved'])
                self.assertTrue(result['excluded_from_victory_pool'])
                self.assertNotIn('certificate', result)
        finally:
            rules.close()


if __name__ == '__main__':
    unittest.main()
