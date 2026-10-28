"""Real saved consumers and selection-only negatives for invocation metadata."""
import copy
import json
from pathlib import Path
import unittest
from certified_task_sampler import actual_events

ROOT = Path(__file__).resolve().parents[2]
RUN = ROOT/'AutomationOutputs/BattleRl/20261008_encounter_floor_training_64'


class NativeSkillEventTests(unittest.TestCase):
    def test_actual_payment_consumers_and_puppets(self):
        cases = [(863, 32, 'hero_skill_pool_000:06'),
                 (863, 34, 'hero_skill_pool_000:07'),
                 (863, 77, 'hero_skill_pool_000:06'),
                 (869, 38, 'hero_skill_pool_003:06'),
                 (869, 46, 'hero_skill_pool_003:06')]
        for episode, step, skill in cases:
            with self.subTest(episode=episode, step=step):
                row = json.loads((RUN/f'episode_{episode}.json').read_text())['trace'][step]
                expected = f'skill_invocation:{skill}:{row["action"]["Option"]}'
                self.assertEqual([k for _, k in actual_events([row]) if k.startswith('skill_invocation:')], [expected])
                negative = copy.deepcopy(row)
                negative['after']['usedSkillsTurn'] = negative['before']['usedSkillsTurn'][:]
                self.assertNotIn(expected, [k for _, k in actual_events([negative])])

    def test_payment_selection_is_not_invocation(self):
        trace = json.loads((RUN/'episode_863.json').read_text())['trace']
        selections = [r for r in trace if r['after'].get('paymentSelection') and not r['before'].get('paymentSelection')]
        self.assertTrue(selections)
        for row in selections:
            self.assertFalse([k for _, k in actual_events([row]) if k.startswith('skill_invocation:')])

    def test_seven_real_shared_owner_uses_and_no_selection_count(self):
        source=ROOT/'AutomationOutputs/BattleRl/20261008_shared_seven_consumers_checks_v2/actual_traces.json'
        traces=json.loads(source.read_text())
        events=[key for trace in traces for _,key in actual_events(trace) if key.startswith('skill_invocation:')]
        for pool in ('000','001','002','003','004','005','007'):
            self.assertEqual(sum(key.startswith(f'skill_invocation:hero_skill_pool_{pool}:09:') for key in events),1)
        for trace in traces:
            for row in trace:
                if row['action']['Kind'] in ('sharedArm','sharedCancel','sharedClaim','sharedSourceReturn','sharedHowlResolve'):
                    self.assertFalse([key for _,key in actual_events([row]) if key.startswith('skill_invocation:')])


if __name__ == '__main__':
    unittest.main()
