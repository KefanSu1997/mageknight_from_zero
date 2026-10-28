"""Same-fixture UI evidence plus a real shared-mana consumer chain.

Native fixture setup is separated from pointer actions. No training or claims
of human performance, global shortest paths or feedback-photon latency.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import shutil
import sys
import time
import uuid
import zipfile

ROOT = Path(__file__).resolve().parents[2]
sys.path[:0] = [str(ROOT / 'tools/BattleRl'), str(ROOT / 'tools/RsiGame')]
from capture_phase_baseline import load_fixtures, compare, design_default
from capture_ui_design_pairs import CaptureDemo, editor_call
from structured_ui_ppo import state, write_json


def save(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('x', encoding='utf8') as stream:
        json.dump(data, stream, ensure_ascii=False, indent=2)


class Collector:
    def __init__(self, out, live):
        self.out, self.live = out, live
        self.events, self.captures = [], []
        self.last_point = None
        self.demo = CaptureDemo(live, attach=True)
        self.observation = self.demo.initial_observation

    def rpc(self, operation, _stale_retries=2, **fields):
        request = dict(id='eval_' + uuid.uuid4().hex, operation=operation, fastCapture=True, **fields)
        write_json(self.live / 'request.json', request)
        path = self.live / 'responses' / (request['id'] + '.json')
        started = time.monotonic()
        while time.monotonic() - started < 60:
            if path.is_file():
                try:
                    response = json.loads(path.read_text(encoding='utf8'))
                except (OSError, ValueError):
                    time.sleep(.05)
                    continue
                save(self.out / 'exchanges' / (request['id'] + '.json'), dict(request=request, response=response))
                if not response['success']:
                    error=response.get('error','')
                    if (_stale_retries>0 and operation in ('hover','unhover','scroll','hover_info','unhover_info','scroll_info')
                        and str(error).startswith('System.InvalidOperationException: Stale observation: reobserve before selecting an action.')):
                        # This exact version guard precedes every dispatch. Keep
                        # its rejected exchange, refresh, and retry only reading
                        # gestures. Click handling stays with its explicit caller.
                        self.rpc('observe')
                        fields['expectedVersion']=self.observation['version']
                        return self.rpc(operation,_stale_retries=_stale_retries-1,**fields)
                    raise RuntimeError(response.get('error'))
                self.observation = response['observation']
                return response
            time.sleep(.05)
        raise TimeoutError('Single pending evaluation request: ' + request['id'])

    def snapshot(self, label, frames=0):
        response = self.rpc('observe', screenshot=True, evaluationDiagnostics=True, sampleFrames=frames)
        directory = self.out / 'key_phases' / label
        directory.mkdir(parents=True, exist_ok=False)
        screen = directory / 'screen.png'
        shutil.copyfile(response['screenshotPath'], screen)
        response['screenshotPath'] = str(screen)
        save(directory / 'capture.json', response)
        row = dict(label=label, screenshot=str(screen), sha256=hashlib.sha256(screen.read_bytes()).hexdigest(),
                   capture=str(directory / 'capture.json'))
        self.captures.append(row)
        write_json(self.out / 'capture_progress.json', dict(captures=self.captures, events=len(self.events)))
        return response

    def click(self, name, *, role='interaction', occurrence=0):
        for attempt in range(3):
            self.rpc('observe')
            controls = [c for c in self.observation['controls'] if c['name'] == name and c['clickable']]
            if len(controls) <= occurrence:
                raise ValueError(f'Actual control missing: {name}; available: ' + str([c['name'] for c in self.observation['controls'] if c['clickable']]))
            control = controls[occurrence]
            try:
                response = self.rpc('click', path=control['path'], expectedVersion=self.observation['version'], measure=True)
                break
            except RuntimeError as error:
                # This exact bridge guard runs before any pointer dispatch. Never
                # retry an unknown outcome, timeout, rule rejection or other error.
                if attempt == 2 or not str(error).startswith('System.InvalidOperationException: Stale observation: reobserve before selecting an action.'):
                    raise
        point = [response['click']['clickX'], response['click']['clickY']]
        row = dict(role=role, name=name, request_id=response['id'], path=control['path'], point_unity_bottom_left=point,
                   pointer_segment_pixels=math.dist(self.last_point, point) if self.last_point else None,
                   before=response['before'], after=response['observation'], timing=response['timing'])
        self.last_point = point
        self.events.append(row)
        write_json(self.out / 'pointer_events.json', dict(events=self.events, first_pointer_position=None,
            scope='Actual dispatched pointer path; first segment unavailable. Native resets separate chains, no shortest-path claim.'))
        return response

    def prepare(self, fixture):
        self.last_point = None
        self.demo.reset_task(fixture['task'])
        self.demo.prepare_prefix(fixture['prefix'])
        self.demo.design(design_default())
        self.observation = self.demo.call('observe')['observation']
        native, differences = compare(fixture['expected'], self.observation)
        if differences:
            raise AssertionError(differences[:10])
        save(self.out / 'setups' / (fixture['label'] + '.json'), dict(fixture=fixture,
            native_state=native, differences=differences, role='native_setup_not_player_input'))

    def run(self, three_enemy):
        assertions = []
        for fixture in load_fixtures():
            label = fixture['label']
            self.prepare(fixture)
            self.snapshot(label, frames=60 if label == 'ranged' else 0)
            if label == 'ranged':
                card = next(c for c in self.observation['controls'] if c['name'].startswith('Btn_Card_') and c['clickable'])
                self.rpc('hover', path=card['path'], expectedVersion=self.observation['version'])
                self.snapshot('card_hover')
                card = next(c for c in self.observation['controls'] if c['path'] == card['path'])
                self.rpc('unhover', path=card['path'], expectedVersion=self.observation['version'])
            if label == 'melee_two_enemies' and any(c['name'] == 'Btn_HandPage' and c['clickable'] for c in self.observation['controls']):
                self.click('Btn_HandPage', role='pagination_boundary')
                self.snapshot('hand_page_two')
            if label == 'source_play':
                provenance = fixture['provenance']
                with zipfile.ZipFile(provenance['archive']) as archive:
                    document = json.loads(archive.read(provenance['member']))
                trace, pos = document['trace'], provenance['step']
                color = ['Red', 'Blue', 'Green', 'White', 'Gold', 'Black'][trace[pos]['action']['Option']]
                index = state(self.observation)['source.faces'].split(',').index(color)
                start_event = len(self.events)
                if design_default():
                    if not any(c['name'] == f'Btn_TakeSource_{index}' and c['clickable'] for c in self.observation['controls']):
                        self.click('Btn_QuickMana')
                    self.snapshot('source_panel')
                    self.click(f'Btn_TakeSource_{index}')
                else:
                    quick = any(c['name'] == 'Btn_QuickSource' and c['clickable'] for c in self.observation['controls'])
                    if quick:
                        self.click('Btn_QuickSource')
                    else:
                        self.click('Btn_Menu'); self.click('Btn_JourneyTools')
                    self.snapshot('source_panel')
                    self.click(f'Btn_Source_{index}'); self.click('Btn_UseSource')
                    if any(c['name'] == 'Btn_CloseTools' and c['clickable'] for c in self.observation['controls']):
                        self.click('Btn_CloseTools')
                native, diffs = compare(trace[pos + 1]['before'], self.observation)
                assertions.append(dict(check='actual_source_taken_matches_archived_rule_action', differences=diffs))
                if diffs:
                    raise AssertionError(diffs[:10])
                self.snapshot('source_taken')
                action = trace[pos + 1]['action']
                self.click(f"Btn_Card_{action['Card']}_0")
                self.click('Btn_Choice_' + action['Choice'])
                self.snapshot('selected_action_payment')
                self.click('Btn_Play')
                native, diffs = compare(trace[pos + 2]['before'], self.observation)
                assertions.append(dict(check='actual_enhanced_card_consumer_matches_archived_rule_action', differences=diffs))
                if diffs:
                    raise AssertionError(diffs[:10])
                self.snapshot('source_consumed')
                rows = self.events[start_event:]
                save(self.out / 'source_intent.json', dict(intent='take_public_mana_then_enhance_same_card', clicks=len(rows),
                    event_ids=[x['request_id'] for x in rows], within_path_distance_pixels=sum(x['pointer_segment_pixels'] or 0 for x in rows),
                    initial_pointer_unknown=True, assertions=assertions,
                    scope='Recorded legal reference path, not proved minimum; menu transitions can be inspected in actual observations.'))
        fixture = json.loads(three_enemy.read_text(encoding='utf8'))
        with zipfile.ZipFile(fixture['archive']) as archive:
            raw = archive.read(fixture['member'])
        if hashlib.sha256(raw).hexdigest() != fixture['archive_member_sha256']:
            raise ValueError('Three-enemy provenance mismatch')
        expected = json.loads(raw)['trace'][fixture['step']]['before']
        self.prepare(dict(label='three_enemies', task=fixture['task'], prefix=fixture['prefix'], expected=expected, provenance=fixture))
        self.snapshot('three_enemies')
        save(self.out / 'manifest.json', dict(passed=True, captures=self.captures, actual_pointer_events=len(self.events),
            assertions=assertions, missing_effective_feedback_latency=True, no_human_search_or_error_rate_measurement=True))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--live', type=Path, required=True)
    parser.add_argument('--three-enemy', type=Path, required=True)
    args = parser.parse_args()
    args.out.mkdir(parents=True, exist_ok=True)
    collector = Collector(args.out.resolve(), args.live.resolve())
    try:
        collector.run(args.three_enemy)
    except Exception as error:
        save(args.out / 'failure.json', dict(error=repr(error), captures=collector.captures, events=len(collector.events)))
        raise
