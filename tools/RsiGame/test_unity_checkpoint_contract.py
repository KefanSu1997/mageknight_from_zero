"""Offline fixtures only; never instantiate the live Unity adapter."""
import json
import hashlib
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

from PIL import Image
from run_unity_experiment import snapshot_round
import unity_adapter
from unity_source_view import EditorPngScope


class CheckpointContractTest(unittest.TestCase):
    def test_snapshot_preserves_checklist_and_does_not_backfill_history(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            project = root / 'project'
            project.mkdir()
            (project / 'source.cs').write_text('source')
            game = SimpleNamespace(dir=root / 'run')
            game.dir.mkdir()
            state = game.dir / 'checklist_state.json'
            state.write_text('{"items": ["before"]}')
            snapshot_round(game, 1, project, root / 'blobs')
            stored = game.dir / 'round_01/checklist_state.json'
            self.assertEqual(stored.read_bytes(), state.read_bytes())
            state.write_text('{"items": ["after"]}')
            snapshot_round(game, 1, project, root / 'blobs')
            self.assertIn('before', stored.read_text())
            # Existing historical manifests cannot acquire a current checklist.
            stored.unlink()
            snapshot_round(game, 1, project, root / 'blobs')
            self.assertFalse(stored.exists())

    def test_actual_frame_copy_discovery_pairing_and_historical_identity(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            trace = root / 'fixture.json'
            trace.write_text(json.dumps({'actions': [{'action': 'click', 'args': {}}]}))
            live = root / 'mutable_live.png'
            Image.new('RGB', (2, 2), 'red').save(live)
            adapter = SimpleNamespace(observe=lambda: {'screenshot': str(live)})

            class FixtureSession:
                def __init__(self, *a, **kw):
                    pass
                def __enter__(self):
                    return self
                def __exit__(self, *a):
                    pass
                def do(self, *a):
                    Image.new('RGB', (2, 2), 'blue').save(live)
                    return {'frame': str(live)}

            out = root / 'checkpoint'
            with patch.object(unity_adapter, 'session_class', return_value=FixtureSession):
                receipt = unity_adapter.replay_one(adapter, root, trace, out)
            self.assertNotIn('replay_error', receipt)
            # This is the unchanged loop.compare_checkpoints discovery contract.
            ids = sorted(p.name[len('frames_'):] for p in out.glob('frames_*'))
            self.assertEqual(ids, ['fixture'])
            before = Path(receipt['events'][0]['before'])
            self.assertEqual(Image.open(before).getpixel((0, 0)), (255, 0, 0))
            self.assertEqual(before.parent, (out / 'frames_fixture').resolve())
            self.assertEqual(Path(receipt['final_frame']).parent, before.parent)
            self.assertEqual(unity_adapter.recorded_replay(out, 'fixture'), receipt)
            upstream = Path(__file__).resolve().parents[2] / 'AutomationOutputs/RsiGame/checkouts/RSIGame/src'
            sys.path.insert(0, str(upstream))
            from rsigame.monitor.replay_pair import pair
            paired = pair(receipt, receipt, 'fixture old', 'fixture new')
            self.assertTrue(paired.aligned)
            self.assertEqual(len(paired.events), 1)
            self.assertEqual(paired.old_final, receipt['final_frame'])
            with self.assertRaises(FileNotFoundError):
                unity_adapter.require_actual_work(root / 'old_tree', root)
            self.assertEqual(unity_adapter.require_actual_work(root / 'old_tree', root, out, 'fixture'), receipt)
            before.unlink()
            with self.assertRaises(FileNotFoundError):
                unity_adapter.require_actual_work(root / 'old_tree', root, out, 'fixture')

    def test_png_conversion_excludes_generated_evidence_and_reports_failure(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            for folder in ('Assets', 'Library', 'AutomationOutputs'):
                (root / folder).mkdir()
                Image.new('RGB', (2, 2), 'red').save(root / folder / 'jpeg_named.png', format='JPEG')
            baseline = root / 'Assets/TutorialInfo/Icons/URP.png'
            baseline.parent.mkdir(parents=True)
            Image.new('RGB', (2, 2), 'red').save(baseline, format='JPEG')
            baseline_bytes = baseline.read_bytes()
            scope = EditorPngScope(root, root / 'AutomationOutputs')
            scope.begin()
            # Baseline/user PNG unchanged; the Editor added only the authored
            # jpeg_named.png. Even explicitly listed generated noise is excluded.
            sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
            before = {'Assets/TutorialInfo/Icons/URP.png': sha(baseline)}
            after = {**before, **{p.relative_to(root).as_posix(): sha(p) for p in
                     (root / 'Assets/jpeg_named.png', root / 'Library/jpeg_named.png',
                      root / 'AutomationOutputs/jpeg_named.png')}}
            scope.capture(before, after)
            fixed = scope.fix(root)
            self.assertEqual(fixed, [str(Path('Assets/jpeg_named.png'))])
            self.assertEqual(baseline.read_bytes(), baseline_bytes)
            self.assertTrue((root / 'Assets/jpeg_named.png').read_bytes().startswith(b'\x89PNG'))
            for folder in ('Library', 'AutomationOutputs'):
                self.assertTrue((root / folder / 'jpeg_named.png').read_bytes().startswith(b'\xff\xd8'))
            scope.begin()
            scope.capture(after, after)
            self.assertEqual(scope.fix(root), [])
            receipts = [json.loads(p.read_text()) for p in (root / 'AutomationOutputs/png_conversion_scopes').glob('*.json')]
            self.assertTrue(any(r['eligible_after_sha256'] == {} for r in receipts))
            self.assertTrue(all('does not certify' in r['import_gate'] for r in receipts))
            (root / 'Assets/broken.png').write_bytes(b'not an image')
            scope.begin()
            scope.capture({}, {'Assets/broken.png': sha(root / 'Assets/broken.png')})
            with self.assertRaisesRegex(RuntimeError, 're-encoding unavailable'):
                scope.fix(root)


if __name__ == '__main__':
    unittest.main()
