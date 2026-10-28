import json
import unittest
from solo_trajectory_audit import audit_public_revisits


class PublicRevisitTests(unittest.TestCase):
    def observation(self, step, mana=0, round_index=1):
        return json.dumps(dict(Step=step, Terminal=False,
                              World=dict(Round=round_index), Mana=mana))

    def row(self, before, after, reward=0, terminal=False):
        return dict(ObservationBefore=before, ObservationAfter=after,
                    Transition=dict(Reward=reward, Terminal=terminal))

    def test_counter_does_not_hide_resource_cycle(self):
        rows = [self.row(self.observation(0), self.observation(1, 2), .2),
                self.row(self.observation(1, 2), self.observation(2), -.2)]
        result = audit_public_revisits(rows)
        self.assertEqual(len(result), 1)
        self.assertEqual(result[0]['cycle_steps'], 2)
        self.assertAlmostEqual(result[0]['cycle_return'], 0)
        self.assertFalse(result[0]['proves_exploit'])

    def test_positive_public_cycle_is_review_not_proof(self):
        result = audit_public_revisits([self.row(self.observation(0), self.observation(1), 1)])
        self.assertEqual(result[0]['cycle_return'], 1)
        self.assertFalse(result[0]['proves_exploit'])

    def test_round_progress_is_not_removed(self):
        self.assertEqual(audit_public_revisits([self.row(self.observation(0),
            self.observation(1, round_index=2))]), [])

    def test_terminal_not_flagged(self):
        self.assertEqual(audit_public_revisits([self.row(self.observation(0),
            self.observation(1), terminal=True)]), [])

    def test_bad_observation_rejected(self):
        with self.assertRaises(ValueError):
            audit_public_revisits([self.row('{}', '{}')])


if __name__ == '__main__':
    unittest.main()
