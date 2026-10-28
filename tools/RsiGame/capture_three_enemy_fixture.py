"""Capture one preserved three-enemy context; native setup is not player input."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import shutil
import zipfile

from capture_phase_baseline import compare, design_default


def save(path, value):
    with path.open('x', encoding='utf8') as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--fixture', type=Path, required=True, help='Previously preserved archive/member/SHA/task/prefix provenance')
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--attach-live', type=Path)
    parser.add_argument('--execute', action='store_true')
    parser.add_argument('--operator-fresh-launch', action='store_true')
    args = parser.parse_args()
    if args.execute and (not args.operator_fresh_launch or not args.attach_live):
        parser.error('Live capture requires --attach-live and --operator-fresh-launch')
    provenance = json.loads(args.fixture.read_text(encoding='utf-8-sig'))
    with zipfile.ZipFile(provenance['archive']) as archive:
        raw = archive.read(provenance['member'])
    if hashlib.sha256(raw).hexdigest() != provenance['archive_member_sha256']:
        raise ValueError('Archived fixture member SHA mismatch')
    trace = json.loads(raw)['trace']
    step = provenance['step']
    expected = trace[step]['before']
    task = provenance['task']
    if len(task['EnemyGroups'][0]) != 3 or expected['phase'] != 'Ranged':
        raise ValueError('Expected preserved three-enemy Ranged fixture')
    out = args.out.resolve()
    out.mkdir(parents=True, exist_ok=False)
    save(out / 'fixture.json', provenance)
    if not args.execute:
        save(out / 'preflight.json', dict(passed=True, live_operations=0, archive_member_sha_verified=True))
        print(json.dumps(dict(validated=True, live_operations=0)))
        return
    from unity_adapter import capture_demo
    try:
        demo = capture_demo(args.attach_live.resolve(), attach=True)
        demo.reset_task(task)
        demo.prepare_prefix(provenance['prefix'])
        demo.design(design_default())
        result = demo.call('observe', screenshot=True)
        native, diffs = compare(expected, result['observation'])
        screenshot = out / 'screen.png'
        shutil.copyfile(result['screenshotPath'], screenshot)
        save(out / 'observation.json', result)
        save(out / 'validation.json', dict(passed=not diffs, differences=diffs,
            native_state=native, declared_enemy_count=3,
            screenshot_sha256=hashlib.sha256(screenshot.read_bytes()).hexdigest(),
            scope='Actual same archived three-enemy Ranged context; formal native fixture setup, not a player rollout or visual acceptance',
            player_click_count=0, operator_must_restore_normal_launch=True))
        if diffs:
            raise AssertionError(diffs)
        print(json.dumps(dict(passed=True, episode=provenance['episode'], enemies=3)))
    except Exception as error:
        save(out / 'failure.json', dict(error=repr(error)))
        raise
    finally:
        save(out / 'operator_fresh_launch_required.json', dict(required=True,
            role='explicit_operator_cleanup_obligation', automatically_restored=False))


if __name__ == '__main__':
    main()
