"""Offline substrate gates; no Editor calls."""
import importlib
import inspect
import json
import sys
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch
from unity_adapter import UnityAdapter, session_class
from unity_upstream_session import install_explore_build_gate, install_explore_reader

UPSTREAM = Path(__file__).resolve().parents[2] / 'AutomationOutputs/RsiGame/20261021_original_alignment/upstream/src'

class UnityExploreGate(unittest.TestCase):
    def test_original_explore_reaches_session(self):
        sys.path.insert(0, str(UPSTREAM))
        explore = importlib.import_module('rsigame.agent.evolve.explore_arm')
        original = explore.explore
        entered = []
        class MockSession:
            SUPPORTS = ('read_text',)
            def __init__(self, src, port, out_dir, max_frames=None, available_scenarios=()):
                entered.append(max_frames)
            def __enter__(self): return self
            def __exit__(self, *args): pass
        try:
            with tempfile.TemporaryDirectory() as d:
                root = Path(d)
                (root / 'ProjectSettings').mkdir()
                (root / 'ProjectSettings/ProjectVersion.txt').write_text('Unity')
                adapter = SimpleNamespace(build_gate=lambda receipt: {'success': True})
                install_explore_build_gate(explore, adapter, 'mock-receipt', root)
                with patch.object(explore, '_session_for', return_value=MockSession), patch.object(explore, '_brief', return_value='offline mock'), patch.object(explore, '_ask_for_the_summary'):
                    result = explore.explore(root, steps=1, chat=lambda *a, **kw: ({'action': 'done', 'args': {}}, '{}'))
                self.assertTrue(entered)
                self.assertNotEqual(result.stopped_by, 'never_built')
                adapter.build_gate = lambda receipt: {'success': False}
                failed = explore.explore(root, steps=1)
                self.assertEqual(failed.stopped_by, 'never_built')
        finally:
            explore.explore = original

    def test_build_receipt_bound_to_loaded_module(self):
        with tempfile.TemporaryDirectory() as d:
            receipt = Path(d) / 'receipt.json'
            receipt.write_text('{"success":true,"errors":0,"runtimeModuleVersionId":"a"}')
            adapter = UnityAdapter(d)
            status = {'success': True, 'isCompiling': False, 'actualConsoleCountsAvailable': True, 'actualConsoleErrorCount': 0, 'runtimeModuleVersionId': 'a'}
            with patch.object(adapter, 'status', return_value=status):
                self.assertTrue(adapter.build_gate(receipt)['success'])
                status['runtimeModuleVersionId'] = 'b'
                self.assertFalse(adapter.build_gate(receipt)['success'])

    def test_authoritative_task_reader(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            (root / 'ProjectSettings').mkdir()
            (root / 'ProjectSettings/ProjectVersion.txt').touch()
            explore = SimpleNamespace(_brief=lambda src: 'non-Unity original')
            calls = []
            def task(game, work):
                calls.append((game, work))
                return 'actual current task text', 'tasks/mageknight-unity/instruction.md'
            install_explore_reader(explore, 'mageknight-unity', task)
            result = explore._brief(root)
            self.assertIn('actual current task text', result)
            self.assertIn('partial C# source lines', result)
            self.assertEqual(calls, [('mageknight-unity', root)])
            self.assertEqual(explore._brief(root / 'other'), 'non-Unity original')

    def test_boot_and_scenarios(self):
        with tempfile.TemporaryDirectory() as d:
            adapter = SimpleNamespace(directory=Path(d), observe=lambda: {'screenWidth': 1, 'screenHeight': 1})
            setups = []
            Session = session_class(adapter, lambda bound: setups.append('fresh launch'))
            self.assertIn('available_scenarios', inspect.signature(Session).parameters)
            with self.assertRaisesRegex(ValueError, 'scenario'):
                Session(d, available_scenarios=['invented'])
            session = Session(d, max_frames=23)
            record = session.do('boot', {})
            self.assertTrue(record['ok'])
            self.assertEqual(setups, ['fresh launch'])
            self.assertEqual(record['role'], 'scripted_setup_not_player_input')
            self.assertIn('unavailable', session.do('boot_scenario', {'id': 'invented'})['error'])

    def test_actual_frame_aliases_reach_original_keyframe_reader(self):
        sys.path.insert(0, str(UPSTREAM))
        from rsigame.evidence.keyframes import from_exploration
        from PIL import Image
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            source = root / 'capture.png'
            Image.new('RGB', (2, 2), 'red').save(source)
            def observe():
                return {'screenshot': str(source), 'screenWidth': 2, 'screenHeight': 2}
            def wait(seconds):
                Image.new('RGB', (2, 2), 'blue').save(source)
                return observe()
            adapter = SimpleNamespace(directory=root, observe=observe, wait=wait)
            Session = session_class(adapter)
            session = Session(root, out_dir=root / 'trace')
            record = session.do('wait', {'ms': 1})
            self.assertTrue(record['ok'])
            (root / 'trace/exploration.json').write_text(json.dumps({'steps': [record]}))
            frames = from_exploration(root / 'trace')
            self.assertEqual(len(frames), 2)
            self.assertEqual(Image.open(frames[0][1]).getpixel((0, 0)), (255, 0, 0))
            self.assertEqual(Image.open(frames[1][1]).getpixel((0, 0)), (0, 0, 255))
            self.assertNotEqual(frames[0][1], str(source))
            self.assertNotEqual(Session(root, out_dir=root / 'trace')._frames_dir, session._frames_dir)

if __name__ == '__main__': unittest.main()
