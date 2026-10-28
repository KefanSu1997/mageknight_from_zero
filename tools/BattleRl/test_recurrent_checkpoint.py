import tempfile
from pathlib import Path
import unittest
import numpy as np
from recurrent_checkpoint import Adam, save_checkpoint, load_checkpoint


class CheckpointTests(unittest.TestCase):
    def test_resume_preserves_actual_updates_momentum_rng_and_replay(self):
        random = np.random.default_rng(91)
        parameters = {"w": random.normal(size=(4, 3)), "b": random.normal(size=3)}
        optimizer = Adam(parameters)
        for _ in range(5):
            optimizer.update({k: random.normal(size=v.shape) for k, v in parameters.items()})
        metadata = dict(rules_sha256="frozen", catalog_sha256="catalog", feature_schema="public",
                        architecture="three-layer-GRU", task_goal="formal-group")
        replay = {"task_id": "test", "actions": [4, 7], "hidden": [[.1, .2]]}
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "checkpoint.npz"
            save_checkpoint(path, optimizer, random, metadata, replay)
            resumed, other, other_random, restored = load_checkpoint(path, metadata)
            self.assertEqual(replay, restored)
            self.assertEqual(optimizer.steps, other.steps)
            for _ in range(3):
                optimizer.update({k: random.normal(size=v.shape) for k, v in parameters.items()})
                other.update({k: other_random.normal(size=v.shape) for k, v in resumed.items()})
            for key in parameters:
                np.testing.assert_array_equal(parameters[key], resumed[key])
                np.testing.assert_array_equal(optimizer.first[key], other.first[key])
                np.testing.assert_array_equal(optimizer.second[key], other.second[key])
            self.assertEqual(random.bit_generator.state, other_random.bit_generator.state)
            with self.assertRaisesRegex(ValueError, "differ"):
                load_checkpoint(path, metadata | {"task_goal": "whole-journey"})


if __name__ == "__main__":
    unittest.main()
