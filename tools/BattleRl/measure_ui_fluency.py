"""Current actual Demo baseline: declared intents, pointer paths and render-frame timing.

No PPO update or new gameplay rule. Setup/fixtures, intentional rejection and
recovery have separate denominators. All timing excludes model/file/0.15s wait.
"""
import argparse
import hashlib
import json
import time
import uuid
from pathlib import Path
import numpy as np
from structured_ui_ppo import ROOT, Demo, state, write_json

PREFIX = 'Canvas/BattleTable/'
NEXT_PATHS = {'Btn_Target': PREFIX+'Enemy_0/Btn_Target', 'Btn_ResetPhase': PREFIX+'Btn_ResetPhase',
    'Btn_All': PREFIX+'AttackSelection/Btn_All', 'Btn_Done': PREFIX+'AttackSelection/Btn_Done'}


class MeasurementDemo(Demo):
    def call(self, operation, observation=None, control=None, screenshot=False, timeout=30, next_name=None):
        if operation != 'click':
            return super().call(operation, observation, control, screenshot, timeout)
        self.calls += 1
        request_id = f'{self.calls:04d}_{uuid.uuid4().hex[:8]}'
        request = {'id': request_id, 'operation': operation, 'screenshot': screenshot, 'measure': True,
            'fastCapture':getattr(self,'fast_capture',False),
            'path': control['path'], 'expectedVersion': observation['version'],
            'nextControlPath': NEXT_PATHS.get(next_name, PREFIX+'Inspector/'+next_name) if next_name else ''}
        if control.get('raycastPoint'):
            point=control['raycastPoint']
            request.update(customPoint=True,relativeX=point['relativeX'],relativeY=point['relativeY'])
        write_json(self.directory/'request.json', request)
        started = time.monotonic()
        while time.monotonic()-started < timeout:
            path = self.directory/'responses'/f'{request_id}.json'
            if path.exists():
                try:
                    result = json.loads(path.read_text(encoding='utf-8'))
                except (OSError, json.JSONDecodeError):
                    time.sleep(.05)
                    continue
                write_json(self.directory/'exchanges'/f'{request_id}.json', {'request': request, 'response': result})
                if not result['success']:
                    raise RuntimeError('Actual measured pointer failed: '+result['error'])
                if not result.get('timing') or result['timing']['firstRenderedFrameMilliseconds'] < 0:
                    raise RuntimeError('Actual timing instrumentation missing')
                return result
            time.sleep(.01 if getattr(self,'fast_capture',False) else .05)
        raise TimeoutError('Measured UI response timeout')


def panel(observation):
    controls = [c['path'] for c in observation['controls'] if c['clickable']]
    for name in ('AttackSelection', 'BlockSelection', 'DeedInputs', 'ManaPayment', 'HeroStart', 'Camp'):
        if any('/'+name+'/' in p for p in controls):
            return name
    return 'Battle'


def intent_metrics(case):
    rows = case['steps']
    points = np.array([[r['actual']['click']['clickX'], r['actual']['click']['clickY']] for r in rows])
    distance = float(np.linalg.norm(np.diff(points, axis=0), axis=1).sum()) if len(points)>1 else 0.
    comparisons = []
    for row in rows:
        before, after = row['actual']['before'], row['actual']['observation']
        before_controls = {c['path']: c for c in before['controls'] if c['clickable']}
        after_controls = {c['path']: c for c in after['controls'] if c['clickable']}
        # Raw shared-control motion, not automatically classified as a UX defect.
        for path in before_controls.keys() & after_controls.keys():
            b, a = before_controls[path], after_controls[path]
            displacement = float(np.hypot(a['x']-b['x'], a['y']-b['y']))
            if displacement > 1:
                comparisons.append({'path': path, 'pixels': displacement})
    reject_delta = sum(max(0, int(state(r['actual']['observation']).get('rejections', 0))-
                                 int(state(r['actual']['before']).get('rejections', 0))) for r in rows)
    switches = sum(panel(r['actual']['before']) != panel(r['actual']['observation']) for r in rows)
    timings = [r['actual']['timing'] for r in rows]
    ready = [t['postFrameInputBlockedMilliseconds'] for t in timings if t['nextControlReadyMilliseconds'] >= 0]
    return {'clicks': len(rows), 'verified_reference_clicks': case['reference_clicks'],
        'excess_vs_reference': max(0, len(rows)-case['reference_clicks']), 'completed': case['completed'],
        'panel_transitions': switches, 'distinct_panels': sorted({panel(r['actual']['before']) for r in rows} |
                                                               {panel(r['actual']['observation']) for r in rows}),
        'rule_rejections': reject_delta, 'pointer_distance_within_intent_pixels': distance,
        'handler_ms': [t['handlerMilliseconds'] for t in timings],
        'first_rendered_frame_ms': [t['firstRenderedFrameMilliseconds'] for t in timings],
        'next_control_wait_samples': len(ready), 'post_frame_block_ms': ready,
        'next_control_timeouts': sum(t['nextControlTimedOut'] for t in timings),
        'raw_shared_control_movements': comparisons}


class Trial:
    def __init__(self, path):
        self.path = Path(path)
        self.demo = MeasurementDemo(self.path)
        self.observation = self.demo.original_opening()
        self.cases, self.current = [], None

    @classmethod
    def attach_pending(cls, path):
        path = Path(path).resolve()
        active = Path((ROOT/'AutomationOutputs/structured_ui_pilot_directory.txt').read_text(encoding='utf-8').strip())
        if active != path or (path/'cases.json').exists():
            raise ValueError('Only the current unmeasured pending pilot may be attached')
        trial = cls.__new__(cls)
        trial.path = path
        trial.demo = MeasurementDemo.__new__(MeasurementDemo)
        trial.demo.directory, trial.demo.calls = path, 0
        trial.demo.initial_observation = trial.demo.call('observe', timeout=90)['observation']
        write_json(path/'attachment.json', {'role': 'recovered pending menu after transport timeout',
            'actual_first_observation_version': trial.demo.initial_observation['version'],
            'no_previous_task_or_policy_steps_repeated': True})
        trial.observation = trial.demo.original_opening()
        trial.cases, trial.current = [], None
        return trial

    def click(self, name, next_name=None, screenshot=False):
        controls = [c for c in self.observation['controls'] if c['name']==name and c['clickable']]
        assert len(controls)==1, (name, [(c['path'], c['clickable']) for c in self.observation['controls']])
        before = self.observation
        actual = self.demo.call('click', before, controls[0], screenshot=screenshot, next_name=next_name)
        self.observation = actual['observation']
        if self.current is not None:
            self.current['steps'].append({'name': name, 'role': 'scripted_fixed_intent_measurement',
                'actual': actual})
            self.save()
        return actual

    def card(self):
        controls = [c for c in self.observation['controls'] if c['clickable'] and
                    c['name'].startswith('Btn_Card_') and not c['name'].startswith('Btn_Card_w_')]
        return min(controls, key=lambda c: c['x'])['name']

    def sideways(self):
        self.click(self.card(), next_name='Btn_Sideways')
        self.click('Btn_Sideways')

    def begin(self, identity, reference):
        self.current = {'intent_id': identity, 'reference_clicks': reference, 'completed': False,
                        'before': state(self.observation), 'steps': []}
        self.cases.append(self.current)

    def end(self, assertions):
        actual = state(self.observation)
        self.current['assertions'] = [{'key': k, 'expected': str(v), 'actual': actual.get(k),
                                      'passed': actual.get(k)==str(v)} for k, v in assertions.items()]
        self.current['completed'] = all(a['passed'] for a in self.current['assertions'])
        self.current['after'] = actual
        self.current['metrics'] = intent_metrics(self.current)
        self.save()
        assert self.current['completed'], self.current['assertions']
        self.current = None

    def save(self):
        write_json(self.path/'cases.json', {'cases': self.cases})

    def run(self):
        try:
            self.click('Btn_Next')
            assert state(self.observation)['phase']=='Block'
            start = state(self.observation)
            self.begin('sideways_block', 2)
            self.sideways()
            self.end({'hand': int(start['hand'])-1, 'block': 1, 'played': 1})
            self.begin('restore_phase_checkpoint', 1)
            self.click('Btn_ResetPhase', screenshot=True)
            self.end({'hand.ids': start['hand.ids'], 'block': 0, 'played': 0})
            self.click('Btn_Next')
            assert state(self.observation)['phase']=='AssignDamage'
            self.begin('commit_hero_damage', 1)
            self.click('Btn_Next', screenshot=True)
            self.end({'phase': 'Melee', 'wounds': 2})
            for _ in range(3):
                self.sideways()
            assert state(self.observation)['melee']=='3'
            self.begin('attack_all_short', 2)
            self.click('Btn_Target', next_name='Btn_Resolve')
            self.click('Btn_Resolve', screenshot=True)
            self.end({'kills': 1, 'melee': 0, 'attacks.sources': ''})
            self.click('Btn_ResetPhase')
            for _ in range(3):
                self.sideways()
            self.begin('attack_all_explicit', 5)
            self.click('Btn_Target', next_name='Btn_AttackParts')
            self.click('Btn_AttackParts', next_name='Btn_All', screenshot=True)
            self.click('Btn_All', next_name='Btn_Done')
            self.click('Btn_Done', next_name='Btn_Resolve')
            self.click('Btn_Resolve', screenshot=True)
            self.end({'kills': 1, 'melee': 0, 'attacks.sources': ''})
            self.click('Btn_ResetPhase')
            self.sideways()
            rejection_start = int(state(self.observation)['rejections'])
            self.begin('insufficient_attack_expected_rejection', 2)
            self.click('Btn_Target', next_name='Btn_Resolve')
            self.click('Btn_Resolve', screenshot=True)
            self.end({'kills': 0, 'melee': 1, 'rejections': rejection_start+1})
            self.begin('recover_attack_add_two_cards', 6)
            for _ in range(2):
                self.sideways()
            self.click('Btn_Target', next_name='Btn_Resolve')
            self.click('Btn_Resolve', screenshot=True)
            self.end({'kills': 1, 'melee': 0, 'attacks.sources': ''})
        finally:
            self.save()
            self.demo.close()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--out', required=True)
    parser.add_argument('--trials', type=int, default=3)
    parser.add_argument('--attach-pending', action='store_true')
    args = parser.parse_args()
    out = Path(args.out).resolve()
    if args.attach_pending:
        Trial.attach_pending(out).run()
        print('Completed recovered pending actual trial', flush=True)
        return
    out.mkdir(parents=True, exist_ok=False)
    sources = ['Assets/Scripts/SceneAutomation/StructuredUiPilot.cs',
               'Assets/Scripts/Battle/PveBattleController.cs', 'Assets/Scripts/Battle/PveBattleController.CardModes.cs',
               'Assets/Scripts/Battle/PveBattleController.AttackSelection.cs',
               'Library/ScriptAssemblies/MageKnight.Scripts.dll', 'Library/ScriptAssemblies/MageKnight.Logic.dll']
    write_json(out/'manifest.json', {'screen': [1920, 1080], 'trials': args.trials,
        'source_sha256': {p: hashlib.sha256((ROOT/p).read_bytes()).hexdigest() for p in sources},
        'scope': 'scripted current UI tasks; no PPO update, native rules/layout unchanged, not human experience score',
        'timing': 'software EventSystem-to-first-completed-render-frame proxy; excludes bridge, model and 0.15s delay'})
    for index in range(args.trials):
        Trial(out/f'trial_{index:02d}').run()
        print('Completed actual UI baseline trial', index, flush=True)


if __name__ == '__main__':
    main()
