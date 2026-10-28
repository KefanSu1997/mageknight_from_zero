from dataclasses import asdict
import unittest
from solo_conquest_contract import ScoreState, score
from solo_trajectory_audit import audit_episode


class TrajectoryAuditTests(unittest.TestCase):
    def row(self, terminal=False):
        s = ScoreState(0, 0, 0, 0, 0, (), 0, 0, 0, 0)
        ending = dict(terminal=True, round_index=6, dummy_cards=0,
                      player_announced=True) if terminal else {}
        return dict(before_score=asdict(s), after_score=asdict(s),
                    before_digest='same', after_digest='same', ending=ending,
                    reward=0.0, score_components=score(s, **ending))

    def test_zero_gain_loop_is_review_not_rules_failure(self):
        result = audit_episode([self.row(), self.row(True)])
        self.assertTrue(result['passed'])
        self.assertEqual(result['warnings'][0]['kind'], 'repeated_full_state_review')

    def test_reward_hack_and_duplicate_terminal_rejected(self):
        row = self.row()
        row['reward'] = 1.0
        result = audit_episode([row, self.row(True), self.row(True)])
        kinds = {e['kind'] for e in result['errors']}
        self.assertIn('positive_reward_same_state_cycle', kinds)
        self.assertIn('reward_mismatch', kinds)
        self.assertIn('action_after_terminal', kinds)

    def test_incomplete_episode_not_silently_accepted(self):
        self.assertFalse(audit_episode([self.row()])['passed'])


if __name__ == '__main__':
    unittest.main()
