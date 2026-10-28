import copy
import json
from pathlib import Path
import tempfile
import unittest
import numpy as np
from certified_task_sampler import CertifiedTaskSampler, actual_events
from recurrent_checkpoint import Adam, load_checkpoint, save_checkpoint

ROOT = Path(__file__).resolve().parents[2]
POOL = ROOT/'AutomationOutputs/BattleRl/20261007_discovery_cached_candidates/solvable_pool.json'
EPISODE = ROOT/'AutomationOutputs/BattleRl/20261007_recurrent_two_discovered_tasks/episode_0.json'


class CertifiedSamplerTests(unittest.TestCase):
    def test_first_cycle_exposes_both_real_certified_tasks(self):
        certs = json.loads(POOL.read_text())['tasks']
        sampler = CertifiedTaskSampler(certs)
        random = np.random.default_rng(7)
        first, second = sampler.select(random), sampler.select(random)
        self.assertNotEqual(first['task_id'], second['task_id'])
        self.assertNotEqual(first['hero'], second['hero'])

    def test_actual_invocation_requires_commit_not_inventory_or_selection(self):
        episode = json.loads(EPISODE.read_text())
        events = list(actual_events(episode['trace']))
        self.assertIn('card_mode:basic_card_013:strong', [key for _,key in events])
        self.assertNotIn('card_mode:advanced_card_004:base', [key for _,key in events])
        row = copy.deepcopy(next(r for r in episode['trace'] if r['action']['Kind']=='play'))
        row['after']['hand'] = row['before']['hand'][:]
        self.assertFalse(list(actual_events([row])))

    def test_sampler_and_rng_resume_select_identical_tasks_and_events(self):
        certs = json.loads(POOL.read_text())['tasks']
        episode = json.loads(EPISODE.read_text())
        sampler, random = CertifiedTaskSampler(certs), np.random.default_rng(9)
        sampler.select(random)
        sampler.observe(episode, 0)
        metadata = dict(rules_sha256='test', catalog_sha256='catalog', feature_schema='public',
                        architecture='three_layer_GRU', task_goal='complete_declared_enemy_sequence')
        optimizer = Adam({'w': np.zeros(1)})
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)/'checkpoint.npz'
            save_checkpoint(path, optimizer, random, metadata, dict(sampler=sampler.state()))
            _, _, restored_random, replay = load_checkpoint(path, metadata)
            other = CertifiedTaskSampler(certs, replay['sampler'])
            for _ in range(8):
                self.assertEqual(sampler.select(random)['task_id'], other.select(restored_random)['task_id'])
            self.assertEqual(sampler.state(), other.state())
            self.assertEqual(random.bit_generator.state, restored_random.bit_generator.state)
            with self.assertRaises(ValueError):
                CertifiedTaskSampler(certs[:1], replay['sampler'])


if __name__ == '__main__':
    unittest.main()
