"""Compute a bounded recovery path from saved actual pointer events, no scores."""
import argparse
import hashlib
import json
import math
from pathlib import Path


def state(observation):
    return {row['key']: row['value'] for row in observation['state']}


def native(observation):
    return json.loads(state(observation)['audit.costNativeState'])


def measure(path):
    events = json.loads(path.read_text(encoding='utf8'))['events']
    matches = [i for i, event in enumerate(events)
               if event['role'] == 'intentional_expected_rejection'
               and event['name'] == 'Btn_UseSource']
    if len(matches) != 1:
        raise ValueError('Exactly one explicit repeated-source rejection required')
    index = matches[0]
    rejected = events[index]
    following = events[index + 1:]
    if len(following) != 1 or following[0]['name'] != 'Btn_CloseTools':
        raise ValueError('Expected complete one-action return path, without inferred inputs')
    returned = following[0]
    before, after = state(rejected['before']), state(rejected['after'])
    if int(after['rejections']) != int(before['rejections']) + 1:
        raise ValueError('Rejection not confirmed by actual counter')
    unchanged = (native(rejected['before']) == native(rejected['after'])
                 == native(returned['before']) == native(returned['after']))
    if not unchanged:
        raise ValueError('Native state changed in rejection/return segment')
    destination = returned['after']
    if any(c['name'] == 'Btn_CloseTools' and c['clickable'] for c in destination['controls']):
        raise ValueError('Source panel remains open')
    cards = [c['name'] for c in destination['controls']
             if c['name'].startswith('Btn_Card_') and c['clickable']]
    if not cards:
        raise ValueError('No actual usable hand-card entry after return')
    points = [rejected['point_unity_bottom_left'], returned['point_unity_bottom_left']]
    distance = math.dist(*points)
    diagonal = math.hypot(destination['screenWidth'], destination['screenHeight'])
    return {
        'schema': 'saved-rejection-recovery-v1',
        'source': {'path': str(path.resolve()), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()},
        'scope': 'One archived repeated-source-draw rejection, then return to hand-card interaction. Not undoing a consumed card, restoring all prior UI selections, or a global shortest path.',
        'start': {'request_id': rejected['request_id'], 'after_rejected_input': True,
                  'pointer_unity_bottom_left': points[0], 'feedback': after.get('ui.Feedback')},
        'end': {'request_id': returned['request_id'], 'source_panel_closed': True,
                'clickable_hand_entries': cards, 'pointer_unity_bottom_left': points[1]},
        'recovery_clicks': 1,
        'pointer_distance_pixels': distance,
        'pointer_distance_canvas_diagonals': distance / diagonal,
        'native_state_unchanged': unchanged,
        'rejected_attempt_clicks_excluded_from_recovery': 1,
        'score': None,
        'limitations': ['Post-freeze supplemental measurement, not evidence read by cycle02 reviewer.',
                       'No human error rate, recovery time, or fixed numerical grading anchors.']}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--events', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    result = measure(args.events)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    with args.out.open('x', encoding='utf8') as stream:
        json.dump(result, stream, ensure_ascii=False, indent=2)
    print(json.dumps({k: result[k] for k in ['recovery_clicks', 'pointer_distance_pixels', 'native_state_unchanged']}, ensure_ascii=False))
