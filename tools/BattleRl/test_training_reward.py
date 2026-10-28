import copy
import json
import unittest
from pathlib import Path

from training_reward import CompletionReward


class CompletionRewardTests(unittest.TestCase):
    def test_kill_without_formal_win_and_terminal_without_kill(self):
        objective = CompletionReward(True)
        before = dict(kills=0, phase='Melee', won=False)
        after = dict(kills=1, phase='Melee', won=False)
        self.assertAlmostEqual(objective.step(before, after, 9.99, False), 1.99)
        self.assertAlmostEqual(objective.step(after, after, -.01, True), -10.01)
        objective.reset()
        self.assertAlmostEqual(objective.step(before, before, -7.01, True), -12.01)

    def test_formal_win_persists_through_cleanup_and_resets_next_episode(self):
        objective = CompletionReward(True)
        before = dict(kills=2, phase='Melee', won=False)
        end = dict(kills=2, phase='End', won=True)
        outside = dict(kills=2, phase='Outside', won=False)
        self.assertEqual(objective.step(before, end, 19.99, False), 19.99)
        objective.step(end, end, -.01, False)
        objective.step(end, outside, -.01, False)
        self.assertEqual(objective.whole_wins, 1)
        self.assertEqual(objective.step(outside, outside, -.01, True), -.01)
        objective.reset()
        self.assertAlmostEqual(objective.step(outside, outside, -.01, True), -10.01)

    def test_non_end_win_flag_is_not_formal_completion(self):
        objective = CompletionReward(True)
        state = dict(kills=1, phase='Melee', won=True)
        objective.step(dict(kills=0, won=False), state, 9.99, False)
        self.assertEqual(objective.whole_wins, 0)

    def test_actual_saved_prefix_no_mutation_and_native_default_unchanged(self):
        root = Path(__file__).resolve().parents[2]
        path = root / 'AutomationOutputs/BattleRl/20261006_v86_continuous32768_ppo/audit_32768_5.json'
        trace = json.loads(path.read_text(encoding='utf8'))['trace']
        original = copy.deepcopy(trace)
        disabled, enabled = CompletionReward(), CompletionReward(True)
        for index, step in enumerate(trace[:24]):
            # This actual prefix has two native kills followed by formal End.
            raw = step['reward']
            self.assertEqual(disabled.step(step['before'], step['after'], raw, False), raw)
            shaped = enabled.step(step['before'], step['after'], raw, False)
            delta = step['after']['kills'] - step['before']['kills']
            self.assertAlmostEqual(shaped, raw - 8 * delta)
            if index == 22:
                self.assertEqual(enabled.whole_wins, 0)
            if index == 23:
                self.assertEqual(enabled.whole_wins, 1)
        self.assertEqual(trace, original)


if __name__ == '__main__':
    unittest.main()
