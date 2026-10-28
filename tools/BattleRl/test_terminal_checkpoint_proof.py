import os
from pathlib import Path
import unittest
from terminal_checkpoint_proof import certify_terminal_checkpoint
from train_ppo import Rules

ROOT = Path(__file__).resolve().parents[2]
DLL = ROOT/'tmp/battle_rl_explicit_build_tasks/BattleRl.dll'


class TerminalCheckpointTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        os.environ.update(BATTLE_RL_DLL=str(DLL),BATTLE_RL_PROTOCOL='--serve-journey-artifacts',
                          BATTLE_RL_NATIVE_PAYMENTS='1',BATTLE_RL_NATIVE_CONVERSIONS='1',
                          BATTLE_RL_NATIVE_ENERGY_TARGETS='1',BATTLE_RL_ORIGINAL_OPENINGS='1')
        cls.rules=Rules()

    @classmethod
    def tearDownClass(cls):
        cls.rules.close()

    def test_actual_shaman_dead_end_does_not_condemn_initial_task(self):
        source=ROOT/'AutomationOutputs/BattleRl/20261006_v102_continuous_ppo/audit_24576_2.json'
        proof=certify_terminal_checkpoint(self.rules,source,DLL)
        self.assertEqual(proof['operations'],90)
        self.assertEqual(proof['legal_successor_count'],0)
        self.assertFalse(proof['initial_task_unsolvable'])
        self.assertTrue(proof['excluded_from_continuation_victory_pool'])
        import json
        (ROOT/'AutomationOutputs/BattleRl/20261007_terminal_shaman_continuation_proof.json').write_text(
            json.dumps(proof,indent=2),encoding='utf-8')

    def test_completed_success_is_not_an_unsolvable_continuation(self):
        source=ROOT/'AutomationOutputs/BattleRl/20261007_independent_sequence_gate/witness_0.json'
        with self.assertRaisesRegex(ValueError,'Only a nonwinning'):
            certify_terminal_checkpoint(self.rules,source,DLL)


if __name__=='__main__':
    unittest.main()
