import unittest
from solo_training_diagnostics import inspect_updates, inspect_evaluation


class DiagnosticsTests(unittest.TestCase):
    def rows(self, n=50):
        return [dict(update=i+1,run_id='fixture',rollout=f'rollout/{i+1}',pre_update_ev=.7,
            mse=.1,bias=0.,target_variance=1.,reward=i*.1,accepted_optimizer_steps=10,valid_samples=100,
            timing_seconds=dict(collection=4.,ppo=3.,save=1.,archive=1.,full_update_wall=8.)) for i in range(n)]

    def test_rising_fixture_does_not_certify_quality(self):
        result=inspect_updates(self.rows())
        self.assertEqual(result['alerts'],[])
        self.assertFalse(result['proves_policy_quality'])

    def test_low_ev_flat_reward_and_slowdown_identify_evidence(self):
        rows=self.rows()
        for r in rows:r['reward']=1.;r['pre_update_ev']=.01
        for r in rows[-5:]:r['timing_seconds']['full_update_wall']=20.
        result=inspect_updates(rows)
        self.assertEqual({a['code'] for a in result['alerts']},
            {'critic_ev_review','reward_growth_review','wall_time_regression'})
        self.assertEqual(result['alerts'][0]['rollouts'][-1],'rollout/50')

    def test_missing_history_and_zero_variance_rejected(self):
        with self.assertRaises(ValueError):inspect_updates(self.rows()[1:])
        rows=self.rows(1);rows[0]['target_variance']=0
        with self.assertRaises(ValueError):inspect_updates(rows)
        rows[0]['pre_update_ev']=None
        self.assertFalse(inspect_updates(rows)['alerts'])

    def test_eval_errors_remain_in_denominator(self):
        record=dict(split='validation',worlds=100,episodes=700,completed=699,errors=1,
                    evaluation_wall=100.,evaluation_queue_wait=30.)
        self.assertEqual(inspect_evaluation(record)['denominator'],700)
        record['episodes']=699
        with self.assertRaises(ValueError):inspect_evaluation(record)


if __name__=='__main__':unittest.main()
