"""Renderer must retain actual independent judgment, including nulls."""
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from render_combat_focus_review import load_actual_review


class RenderIntegrity(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.path = Path(self.tmp.name)
        self.raw = b'{"score":null,"reason":"actual"}'
        (self.path / 'actual_final.txt').write_bytes(self.raw)
        (self.path / 'review.json').write_bytes(self.raw)
        self.proof = {'verified': True, 'actual_final': {
            'path': str(self.path / 'actual_final.txt'),
            'bytes': len(self.raw), 'sha256': hashlib.sha256(self.raw).hexdigest()}}

    def tearDown(self):
        self.tmp.cleanup()

    def test_serialized_copy_preserves_judgment(self):
        (self.path / 'review.json').write_text(json.dumps(json.loads(self.raw), indent=2))
        self.assertIsNone(load_actual_review(self.path, self.proof)['score'])

    def test_changed_score_rejected(self):
        (self.path / 'review.json').write_text('{"score":5,"reason":"actual"}')
        with self.assertRaises(ValueError): load_actual_review(self.path, self.proof)

    def test_changed_actual_rejected(self):
        (self.path / 'actual_final.txt').write_text('{"score":5}')
        with self.assertRaises(ValueError): load_actual_review(self.path, self.proof)

    def test_legacy_requires_exact_actual_bytes(self):
        proof = {'verified': True, 'actual_final_sha256': hashlib.sha256(self.raw).hexdigest()}
        self.assertEqual(load_actual_review(self.path, proof)['reason'], 'actual')
        (self.path / 'review.json').write_text(json.dumps(json.loads(self.raw), indent=2))
        with self.assertRaises(ValueError): load_actual_review(self.path, proof)


if __name__ == '__main__':
    unittest.main()
