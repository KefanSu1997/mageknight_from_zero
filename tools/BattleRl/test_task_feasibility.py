"""Meaningful gate checks using one real original-rule witness, never mock rules."""
import copy
import json
import os
from pathlib import Path
import tempfile
import unittest

from task_feasibility import GOAL, certify_witness, eligible

ROOT = Path(__file__).resolve().parents[2]
DLL = ROOT / "tmp/battle_rl_v94_effective_fortification/BattleRl.dll"
WITNESS = ROOT / "AutomationOutputs/BattleRl/20261006_v95_continuous_ppo/audit_18432_3.json"


class FeasibilityGateTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        os.environ.update(BATTLE_RL_DLL=str(DLL), BATTLE_RL_PROTOCOL="--serve-journey-artifacts",
                          BATTLE_RL_NATIVE_PAYMENTS="1", BATTLE_RL_NATIVE_CONVERSIONS="1",
                          BATTLE_RL_NATIVE_ENERGY_TARGETS="1", BATTLE_RL_ORIGINAL_OPENINGS="1")
        from train_ppo import Rules
        cls.rules = Rules()
        cls.certificate = certify_witness(cls.rules, WITNESS, DLL)

    @classmethod
    def tearDownClass(cls):
        cls.rules.close()

    def test_actual_win_is_eligible_with_narrow_goal(self):
        self.assertTrue(eligible(self.certificate, DLL))
        self.assertEqual(self.certificate["evidence"]["operations"], 12)
        self.assertEqual(self.certificate["task"]["goal"], GOAL)
        self.assertIn("whole_journey_success", self.certificate["scope_gaps"])

    def test_unknown_impossible_and_changed_goal_stay_out(self):
        for status in ("unknown", "proven_unsolvable", "PPO_lost"):
            item = copy.deepcopy(self.certificate)
            item["status"] = status
            self.assertFalse(eligible(item, DLL))
        self.assertFalse(eligible(self.certificate, DLL, goal="whole_journey_success"))

    def test_changed_rule_task_or_evidence_stays_out(self):
        for category, key in (("task", "rules_sha256"), ("evidence", "sha256")):
            item = copy.deepcopy(self.certificate)
            item[category][key] = "edited"
            self.assertFalse(eligible(item, DLL))
        item = copy.deepcopy(self.certificate)
        item["task"]["seed"] += 1
        self.assertFalse(eligible(item, DLL))

    def test_truncation_and_corrupted_public_state_are_not_witnesses(self):
        original = json.loads(WITNESS.read_text(encoding="utf-8"))
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "invalid.json"
            truncated = copy.deepcopy(original)
            truncated["trace"] = truncated["trace"][:11]  # Kill, but no formal End.
            path.write_text(json.dumps(truncated), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "No formal group victory"):
                certify_witness(self.rules, path, DLL)
            corrupted = copy.deepcopy(original)
            corrupted["trace"][0]["before"]["fame"] += 1
            path.write_text(json.dumps(corrupted), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "before-state mismatch"):
                certify_witness(self.rules, path, DLL)


if __name__ == "__main__":
    unittest.main()
