"""Unity transport for RSIGame; no repair policy or substitute game simulation.

Only the owner of the live Unity Editor may instantiate this adapter. Raw bridge
exchanges remain local audit evidence; role prompts receive public_view only.
"""
from __future__ import annotations

import copy
import hashlib
import json
import os
import re
import sys
import time
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BATTLE_TOOLS = ROOT / 'tools' / 'BattleRl'
if str(BATTLE_TOOLS) not in sys.path:
    sys.path.insert(0, str(BATTLE_TOOLS))


def digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, ensure_ascii=False).encode()).hexdigest()


def visible_digest(observation):
    return digest({k: v for k, v in public_view(observation).items() if k != 'version'})


def public_view(observation):
    """Legacy public projection for frozen hashes; no native/audit state.

    Its control coordinates retain Unity axes for historical compatibility.
    Send model_view() to readers so their centers match screenshot clicks.
    """
    return {
        key: copy.deepcopy(observation[key])
        for key in ('version', 'screenWidth', 'screenHeight', 'controls')
        if key in observation
    } | {'visible_text': [copy.deepcopy(row) for row in observation.get('state', [])
                          if row.get('key', '').startswith('ui.') and
                          row.get('key') != 'ui.hoverPreviewVisible']}


def model_view(observation):
    """Match displayed control centers to the screenshot/click coordinate system.

    Keep public_view unchanged for historical frozen replay digests. Native
    point probes and relative pointer coordinates continue using Unity axes.
    """
    view = public_view(observation)
    height = view['screenHeight']
    for control in view.get('controls', []):
        control['y'] = height - control['y']
    view['coordinate_origin'] = 'top-left viewport; controls and click arguments use screenshot coordinates'
    return view


class UnityAdapter:
    """Version checked actual EventSystem input using the established pilot.

    Construction is passive. boot/attach are explicit live Editor operations.
    Player scripts contain only control operations and waits; fixture resets
    and native prefixes are separately recorded setup, never explorer actions.
    """
    def __init__(self, directory, demo=None):
        self.directory = Path(directory).resolve()
        self.demo = demo
        self.observation = None
        self.actions = []
        self.setup_events = []
        self.frozen = False

    def boot(self, attach=False):
        self.demo = capture_demo(self.directory, attach=attach)
        self.observation = self.demo.initial_observation
        return self.observe(screenshot=True)

    def _ready(self):
        if self.demo is None:
            raise RuntimeError('boot or attach a pilot before operating Unity')

    def _accept(self, result):
        self.observation = result['observation']
        public = model_view(self.observation)
        screenshot = result.get('screenshotPath')
        if screenshot:
            path = Path(screenshot)
            if not path.is_file():
                raise RuntimeError('Unity screenshot receipt has no image file')
            public['screenshot'] = str(path.resolve())
            public['screenshot_sha256'] = hashlib.sha256(path.read_bytes()).hexdigest()
        return public

    def observe(self, screenshot=True):
        self._ready()
        return self._accept(self.demo.call('observe', screenshot=screenshot))

    def screenshot(self):
        return self.observe(screenshot=True)

    def act(self, operation, path, expected_version, *, record=True, point=None):
        self._ready()
        if operation not in ('click', 'hover', 'unhover', 'probe'):
            raise ValueError('Player input must target an actual control')
        if self.frozen and record:
            raise RuntimeError('Frozen script cannot be extended')
        # Fresh observation catches intervening updates, including stale replay input.
        self.observe(screenshot=False)
        if self.observation['version'] != expected_version:
            raise ValueError('Stale observation: reobserve and select again')
        matches = [c for c in self.observation['controls'] if c['path'] == path]
        if len(matches) != 1 or not matches[0].get('clickable'):
            raise ValueError('Control missing, ambiguous, disabled or center raycast obstructed')
        if point is None:
            result = self.demo.call(operation, self.observation, matches[0], screenshot=True)
        else:
            if operation != 'click':
                raise ValueError('Explicit pointer position is supported for click only')
            u, v = point
            if not (0 <= u <= 1 and 0 <= v <= 1):
                raise ValueError('Pointer outside actual control rectangle')
            self.demo.calls += 1
            rid = f'{self.demo.calls:04d}_{uuid.uuid4().hex[:8]}'
            request = {'id': rid, 'operation': 'click', 'path': path,
                       'expectedVersion': expected_version, 'screenshot': True,
                       'customPoint': True, 'relativeX': u, 'relativeY': v}
            from structured_ui_ppo import write_json
            write_json(self.directory / 'request.json', request)
            deadline = time.monotonic() + 30
            response = self.directory / 'responses' / (rid + '.json')
            while True:
                if response.is_file():
                    try:
                        result = json.loads(response.read_text(encoding='utf-8'))
                    except (OSError, json.JSONDecodeError):
                        result = None
                    if result and result.get('id') == rid:
                        write_json(self.directory / 'exchanges' / (rid + '.json'), {'request': request, 'response': result})
                        if not result.get('success'):
                            raise RuntimeError('Actual point click rejected: ' + str(result.get('error')))
                        break
                if time.monotonic() >= deadline:
                    raise TimeoutError('Actual point click timed out; no transition fabricated')
                time.sleep(.05)
        view = self._accept(result)
        if record:
            self.actions.append({'operation': operation, 'path': path,
                                 'expected_version': expected_version,
                                 'relative_point': list(point) if point is not None else None,
                                 'after_public_sha256': visible_digest(self.observation),
                                 'role': 'player_input'})
        return view

    def wait(self, seconds, *, record=True):
        seconds = float(seconds)
        if not 0 <= seconds <= 60:
            raise ValueError('Wait must be between 0 and 60 seconds')
        if self.frozen and record:
            raise RuntimeError('Frozen script cannot be extended')
        time.sleep(seconds)
        result = self.observe()
        if record:
            self.actions.append({'operation': 'wait', 'seconds': seconds, 'role': 'player_input'})
        return result

    def setup(self, task=None, prefix=None, optimized_ui=None):
        self._ready()
        if task is not None:
            self.demo.reset_task(task)
        if prefix is not None:
            self.demo.prepare_prefix(prefix)
        if optimized_ui is not None:
            self.demo.design(bool(optimized_ui))
        self.setup_events.append({'role': 'scripted_setup_not_player_input',
                                 'task': task, 'prefix': prefix, 'optimized_ui': optimized_ui})
        return self.observe()

    def freeze(self, destination):
        self.frozen = True
        script = {'schema': 1, 'engine': 'unity_structured_ui_pilot',
                  'actions': copy.deepcopy(self.actions)}
        script['sha256'] = digest(script)
        target = Path(destination)
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(json.dumps(script, indent=2, ensure_ascii=False), encoding='utf-8')
        return script

    def replay(self, script):
        """Replay exact controls after caller restores setup; reject divergence."""
        if isinstance(script, (str, Path)):
            script = json.loads(Path(script).read_text(encoding='utf-8'))
        content = {k: v for k, v in script.items() if k != 'sha256'}
        if script.get('sha256') != digest(content):
            raise ValueError('Frozen script digest mismatch')
        results = []
        for action in script['actions']:
            if action['operation'] == 'wait':
                results.append(self.wait(action['seconds'], record=False))
                continue
            view = self.observe(screenshot=False)
            # The original opaque version includes hidden audit state; replay uses
            # a fresh version while independently checking the visible outcome.
            results.append(self.act(action['operation'], action['path'], view['version'], record=False, point=action.get('relative_point')))
            if visible_digest(self.observation) != action['after_public_sha256']:
                raise RuntimeError('Replay visible outcome diverged; retained raw exchange')
        return results

    def status(self):
        from capture_ui_design_pairs import editor_call
        return editor_call('status')

    def build_gate(self, compile_receipt=None):
        """Editor readiness is not a zero-error compiler result."""
        status = self.status()
        receipt = (json.loads(Path(compile_receipt).read_text(encoding='utf-8'))
                   if compile_receipt else None)
        passed = bool(receipt and receipt.get('success') is True and
                      (receipt.get('errors') == 0 or (receipt.get('actualConsoleCountsAvailable') is True and receipt.get('actualConsoleErrorCount') == 0)) and
                      bool(receipt.get('runtimeModuleVersionId')) and
                      receipt.get('runtimeModuleVersionId') == status.get('runtimeModuleVersionId') and
                      status.get('success') and not status.get('isCompiling') and
                      status.get('actualConsoleCountsAvailable') is True and status.get('actualConsoleErrorCount') == 0)
        return {'success': passed, 'editor_status': status, 'compile_receipt': receipt,
                'scope': 'requires zero-error compile receipt bound to loaded assembly; status alone is insufficient'}

    def close(self):
        self._ready()
        self.demo.close()


def capture_demo(directory, attach=True):
    """Unity RSIGame cold-launch allowance, without changing BattleRl.

    A longer wait continues polling the same observe request. It does not
    relaunch a scene, synthesize input, or retry a timed-out player action.
    """
    from capture_ui_design_pairs import CaptureDemo, editor_call
    from structured_ui_ppo import write_json

    class ColdLaunchCaptureDemo(CaptureDemo):
        def __init__(self):
            self.directory = Path(directory).resolve()
            self.directory.mkdir(parents=True, exist_ok=attach)
            self.calls = 0
            (ROOT / 'AutomationOutputs/structured_ui_pilot_directory.txt').write_text(
                str(self.directory), encoding='utf-8')
            if not attach:
                write_json(self.directory / 'launch.json', editor_call('start_pilot'))
            self.initial_observation = self.call('observe', timeout=180)['observation']

    return ColdLaunchCaptureDemo()


def session_class(adapter, setup=None):
    """Bind one owner-controlled pilot to the upstream session constructor.

    setup is called at each entry to restore a declared task through native
    fixture admission, recorded separately from actions. No child process may
    acquire the Editor concurrently.
    """
    class UnitySession:
        SUPPORTS = ('boot', 'click', 'wait', 'read_text', 'screenshot', 'read_source', 'list_source', 'read_bindings')

        def __init__(self, src, port=None, out_dir=None, max_frames=None, available_scenarios=(), **kwargs):
            if available_scenarios:
                raise ValueError('Unity named scenario launches are unavailable; no game-declared entry has been grounded')
            self.available_scenarios = ()
            self.max_frames = max_frames
            self.src = Path(src)
            self.out_dir = Path(out_dir) if out_dir else adapter.directory
            self.out_dir.mkdir(parents=True, exist_ok=True)
            self._frames_dir = self.out_dir / '_unity_input_frames' / uuid.uuid4().hex
            self.actions, self.observations, self.frames = [], [], []
            self.w = self.h = 0

        def __enter__(self):
            start = time.monotonic()
            if setup:
                setup(adapter)
            view = adapter.observe()
            self.w, self.h = view['screenWidth'], view['screenHeight']
            self.boot_s = time.monotonic() - start
            self.evidence_pathway = 'Unity visible UI and real EventSystem controls'
            return self

        def __exit__(self, *exc):
            # A bound shared pilot belongs to the owning process.
            return False

        def closing_snapshot(self):
            view = adapter.observe()
            return {'frame': view.get('screenshot'), 'visible_ui': view,
                    'note': 'taken after verdict'}

        def do(self, action, args):
            args = args or {}
            rec = {'n': len(self.actions) + 1, 'action': action, 'args': args}
            try:
                if action in ('read_source', 'list_source', 'read_bindings'):
                    root = self.src.resolve()
                    if action == 'read_source':
                        path = (root / str(args.get('path', ''))).resolve()
                        path.relative_to(root)
                        if any(part in {'Library', 'Temp', 'AutomationOutputs', '.git'} for part in path.relative_to(root).parts):
                            raise ValueError('Source read targets a cache or private evidence directory')
                        observed = path.read_text(encoding='utf-8', errors='replace')[:6000]
                    else:
                        sources = sorted((root / 'Assets').rglob('*.cs'))
                        if action == 'list_source':
                            observed = [p.relative_to(root).as_posix() for p in sources][:120]
                        else:
                            observed = [{'path': p.relative_to(root).as_posix(), 'line': n, 'source': line.strip()}
                                        for p in sources for n, line in enumerate(p.read_text(encoding='utf-8', errors='replace').splitlines(), 1)
                                        if re.search(r'onClick\.AddListener|Input\.(GetKey|GetButton|GetMouseButton)|InputAction', line)][:120]
                    rec.update(ok=True, observed=observed)
                    self.actions.append(rec)
                    self.observations.append({'after_action': rec['n'], 'action': action, 'value': observed})
                    return rec
                # Upstream keyframes.from_exploration reads these canonical
                # observed fields, not our top-level frame/screenshot aliases.
                # Capture an actual before-frame; never infer one from a later
                # state or manufacture it from a previous action's screenshot.
                before = adapter.observe()
                before_frame = (freeze_replay_frame(before['screenshot'],
                    self._frames_dir, f"event_{rec['n']:04d}_before.png")
                    if before.get('screenshot') else None)
                if action == 'wait':
                    view = adapter.wait(float(args.get('ms', 600)) / 1000)
                elif action == 'click':
                    count = int(args.get('times', 1))
                    if not 1 <= count <= 10:
                        raise ValueError('Click times must be 1..10')
                    for _ in range(count):
                        view = adapter.observe(screenshot=False)
                        # Upstream image coordinates have a top-left origin;
                        # Unity controls report bottom-left screen coordinates.
                        x, y = float(args['x']), view['screenHeight'] - float(args['y'])
                        candidates = []
                        # Probe actual transformed coordinates, not local Rect
                        # sizes (Canvas scaling can make those differ).
                        for control in sorted(adapter.observation['controls'], key=lambda c: (c['x']-x)**2+(c['y']-y)**2):
                            if not control.get('clickable'):
                                continue
                            probe = adapter.demo.call('probe', adapter.observation, control)
                            points = {(round(p['relativeX'], 2), round(p['relativeY'], 2)): p
                                      for p in probe['raycastProbes']}
                            center, right, up = points[(.5, .5)], points[(.9, .5)], points[(.5, .9)]
                            ax, ay = (right['x']-center['x'])/.4, (right['y']-center['y'])/.4
                            bx, by = (up['x']-center['x'])/.4, (up['y']-center['y'])/.4
                            det = ax*by-ay*bx
                            if abs(det) < 1e-9:
                                continue
                            dx, dy = x-center['x'], y-center['y']
                            u, v = .5+(dx*by-dy*bx)/det, .5+(ax*dy-ay*dx)/det
                            if 0 <= u <= 1 and 0 <= v <= 1:
                                candidates.append((control, u, v))
                                break
                        if len(candidates) != 1:
                            raise ValueError('Requested point has no unique actual clickable control rectangle')
                        control, u, v = candidates[0]
                        view = adapter.act('click', control['path'], view['version'], point=(u, v))
                elif action == 'boot':
                    if not setup:
                        raise RuntimeError('Unity boot requires owner fresh-launch setup')
                    setup(adapter)
                    rec['role'] = 'scripted_setup_not_player_input'
                    view = adapter.observe()
                elif action == 'boot_scenario':
                    raise ValueError('Unity named scenario unavailable: no grounded game-declared entry')
                elif action in ('screenshot', 'read_text'):
                    view = adapter.observe()
                else:
                    raise ValueError('Unity action unavailable: ' + action)
                after_frame = (freeze_replay_frame(view['screenshot'],
                    self._frames_dir, f"event_{rec['n']:04d}_after.png")
                    if view.get('screenshot') else None)
                view = dict(view, before_frame=before_frame, after_frame=after_frame)
                rec.update(ok=True, observed=view, frame=view.get('screenshot'))
                if view.get('screenshot'):
                    self.frames.append(view['screenshot'])
                self.observations.append({'after_action': rec['n'], 'action': action, 'value': view})
            except Exception as error:
                rec['error'] = f'{type(error).__name__}: {error}'
            self.actions.append(rec)
            return rec
    return UnitySession


def freeze_replay_frame(source, frames_dir, name):
    """Copy a real PNG immediately, before another live capture can replace it."""
    raw = Path(source).read_bytes()
    if not raw.startswith(b'\x89PNG\r\n\x1a\n'):
        raise ValueError(f'Unity evidence is not an actual PNG: {source}')
    target = Path(frames_dir) / name
    target.parent.mkdir(parents=True, exist_ok=True)
    try:
        with target.open('xb') as stream:
            stream.write(raw)
    except FileExistsError:
        if target.read_bytes() != raw:
            raise RuntimeError(f'Refusing to overwrite checkpoint frame: {target}')
    return str(target.resolve())


def recorded_replay(out_dir, demo_id):
    receipt = Path(out_dir) / f'{demo_id}.unity_replay.json'
    if not receipt.is_file():
        return None
    result = json.loads(receipt.read_text(encoding='utf8'))
    if result.get('replay_error') or not result.get('events') or not result.get('final_frame'):
        return None
    frames = [result['final_frame']]
    for event in result['events']:
        if not event.get('before') or not event.get('after'):
            return None
        frames.extend([event['before'], *event['after']])
    if not all(Path(p).is_file() for p in frames):
        return None
    return result


def require_actual_work(work, actual_work, out_dir=None, demo_id=None):
    if Path(work).resolve() != Path(actual_work).resolve():
        recorded = recorded_replay(out_dir, demo_id) if out_dir is not None else None
        if recorded is not None:
            return recorded
        raise FileNotFoundError(f'Historical Unity tree cannot be replayed by the live current build: {work}')
    return None


def replay_one(adapter, work, trace_path, out_dir, setup=None):
    """Upstream PRR result schema with actual before/after Unity PNGs.

    Accepts upstream session action records, never translates keyboard input
    into invented Unity behavior. Native setup is outside this player trace.
    """
    trace_path, out_dir = Path(trace_path), Path(out_dir)
    frames_dir = out_dir / f'frames_{trace_path.stem}'
    out_dir.mkdir(parents=True, exist_ok=True)
    trace = json.loads(trace_path.read_text(encoding='utf-8'))
    rec = {'demo_id': trace_path.stem, 'scenario': trace.get('scenario'),
           'events': [], 'uniform': [], 'tail_s': 0,
           'evidence_pathway': 'Unity actual input frames; no fabricated video'}
    try:
        events = trace.get('events')
        actions = trace.get('actions')
        if events is not None:
            if not isinstance(events, list) or not events:
                raise ValueError('Unity event replay requires nonempty events')
            actions = []
            previous = 0
            for event in events:
                frame = int(event['frame'])
                if frame < previous:
                    raise ValueError('Frozen event frames are not monotonic')
                if event.get('type') != 'mouse_click' or event.get('button', 'left') != 'left':
                    raise ValueError('Unity input unsupported: ' + str(event.get('type')))
                actions.append({'action': 'click', 'args': {'x': event['x'], 'y': event['y']},
                                'frame': frame, 'gap_s': (frame - previous) / 30})
                previous = frame
        if not isinstance(actions, list) or not actions:
            raise ValueError('Unity replay requires nonempty supported actions/events')
        Session = session_class(adapter, setup)
        with Session(work, out_dir=out_dir) as session:
            for index, event in enumerate(actions, 1):
                remaining = event.get('gap_s', 0)
                while remaining > 0:
                    chunk = min(remaining, 60)
                    adapter.wait(chunk, record=False)
                    remaining -= chunk
                before = freeze_replay_frame(adapter.observe()['screenshot'], frames_dir,
                                             f'event_{index:04d}_before.png')
                result = session.do(event['action'], event.get('args', {}))
                if result.get('error'):
                    raise RuntimeError(result['error'])
                rec['events'].append({'n': index, 'trace_frame': event.get('frame', index),
                                      'what': event['action'], 'before': before,
                                      'after': [freeze_replay_frame(result['frame'], frames_dir,
                                                f'event_{index:04d}_after.png')] if result.get('frame') else []})
            tail = max(0, int(trace.get('duration_frames') or 0) - int(actions[-1].get('frame', 0))) / 30
            while tail > 0:
                chunk = min(tail, 60)
                adapter.wait(chunk, record=False)
                tail -= chunk
            rec['final_frame'] = freeze_replay_frame(adapter.observe()['screenshot'], frames_dir, 'final.png')
            rec['n_events'] = len(rec['events'])
    except Exception as error:
        rec['replay_error'] = f'{type(error).__name__}: {error}'
    (out_dir / f'{trace_path.stem}.unity_replay.json').write_text(
        json.dumps(rec, ensure_ascii=False, indent=2), encoding='utf-8')
    return rec
