"""Numerical PPO gates and public-observation isolation; no simulated game."""
import copy
import json
from pathlib import Path
import tempfile
import unittest
import numpy as np
from structured_ui_ppo import TinyPpo, features, candidates


class StructuredUiPpoTests(unittest.TestCase):
    def test_clipped_policy_value_entropy_gradients(self):
        rng = np.random.default_rng(12)
        worst = 0.
        for advantage, ratio in ((.7, 1.), (-.7, 1.), (.7, 1.4), (-.7, .6)):
            model = TinyPpo()
            g, c = rng.normal(size=96), rng.normal(size=(5, 64))
            logprob = float(np.log(model.forward(g, c)[0][2]))
            sample = {'g': g, 'c': c, 'action': 2, 'old_logprob': logprob-np.log(ratio)}
            args = ([sample], np.array([advantage]), np.array([.2]))
            _, grads = model.loss_gradient(*args)
            for key, parameter in model.parameters.items():
                for _ in range(3):
                    index = tuple(int(rng.integers(size)) for size in parameter.shape)
                    previous = parameter[index]
                    parameter[index] = previous+1e-6
                    plus = model.loss_gradient(*args)[0]
                    parameter[index] = previous-1e-6
                    minus = model.loss_gradient(*args)[0]
                    parameter[index] = previous
                    worst = max(worst, abs((plus-minus)/2e-6-grads[key][index]))
        self.assertLess(worst, 1e-7)
        print('PPO maximum finite-difference error:', worst)

    def test_checkpoint_parameters_optimizer_and_rng(self):
        model = TinyPpo()
        rng = np.random.default_rng(4)
        samples = []
        for index in range(4):
            g, c = rng.normal(size=96), rng.normal(size=(3, 64))
            p, v, _ = model.forward(g, c)
            samples.append({'g': g, 'c': c, 'action': 1, 'old_logprob': float(np.log(p[1])),
                            'value': v, 'reward': index*.1, 'done': index==3})
        model.update(samples, 0)
        with tempfile.TemporaryDirectory() as folder:
            model.save(folder)
            restored = TinyPpo.load(folder)
            for group in ('parameters', 'm', 'v'):
                for key, values in getattr(model, group).items():
                    np.testing.assert_array_equal(values, getattr(restored, group)[key])
            np.testing.assert_array_equal(model.rng.random(20), restored.rng.random(20))
            np.testing.assert_array_equal(model.forward(samples[0]['g'], samples[0]['c'])[0],
                                          restored.forward(samples[0]['g'], samples[0]['c'])[0])
            self.assertEqual(restored.adam_steps, model.adam_steps)

    def test_actual_observation_public_isolation_and_mask(self):
        root = Path(__file__).resolve().parents[2]/'AutomationOutputs/StructuredUiPpo/20261011'
        files = list(root.glob('train*/episode_000/exchanges/*.json'))
        self.assertTrue(files, 'Requires a real Unity observation, not a fabricated fixture')
        observations = [json.loads(p.read_text(encoding='utf-8'))['response']['observation'] for p in files]
        observation = next(o for o in observations if candidates(o))
        original = features(observation)
        altered = copy.deepcopy(observation)
        for row in altered['state']:
            if row['key'] not in ('phase', *(__import__('structured_ui_ppo').NUMERIC_KEYS),
                                   *(__import__('structured_ui_ppo').TEXT_KEYS)):
                row['value'] = 'hidden audit mutation'
        new = features(altered)
        np.testing.assert_array_equal(original[0], new[0])
        np.testing.assert_array_equal(original[1], new[1])
        controls = candidates(observation)
        self.assertTrue(controls)
        blocked = copy.deepcopy(observation)
        for control in blocked['controls']:
            control['clickable'] = False
        self.assertEqual(candidates(blocked), [])
        with self.assertRaises(ValueError):
            altered['controls'].append(altered['controls'][0])
            candidates(altered)


if __name__ == '__main__':
    unittest.main()
