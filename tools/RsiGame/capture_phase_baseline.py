"""Immutable, same-fixture phase evidence. Fixture replay is not player input.

Default mode only validates the ten selected archive entries, without Unity.
The sole live operator must pass --execute --operator-fresh-launch and restart
the normal pilot after capture, before resuming any RSI game round.
"""
import argparse
import hashlib
import json
import re
import shutil
import sqlite3
import sys
import traceback
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools/BattleRl'))
from cost_entity_identity import align
from gate_unity_cost_prefix import differences

FIXTURES = (
    ('outside', 5131, 0), ('ranged', 1959, 1), ('block', 14407, 2),
    ('source_play', 14407, 4), ('assign_damage', 6803, 4),
    ('damage_toggle', 14407, 8), ('melee_two_enemies', 14617, 5),
    ('won_result', 6803, 17), ('end', 5092, 13), ('end_mixed', 1959, 36),
)
DEFAULT_DB = ROOT / 'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite'
DEFAULT_ARCHIVES = Path('H:/MageKnight_RL_Backups/20261012_before_reward_v2')
DESIGN_SOURCE = ROOT / 'Assets/Scripts/Battle/PveBattleController.FluentUi.cs'


def save(path, value):
    # Exclusive creation also protects failed and incomplete evidence folders.
    with path.open('x', encoding='utf8') as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)


def sha_file(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(chunk)
    return digest.hexdigest()


def design_default():
    source = DESIGN_SOURCE.read_text(encoding='utf8')
    match = re.search(r'public\s+bool\s+optimizedInteractionUi\s*=\s*(true|false)\s*;', source)
    if not match:
        raise ValueError('Cannot establish current UI design source default')
    return match.group(1) == 'true'


def load_fixtures(database=DEFAULT_DB, archive_root=DEFAULT_ARCHIVES):
    """Read only selected ZIP members; verify bytes before parsing each member."""
    import zipfile
    result = []
    with sqlite3.connect(database.resolve().as_uri() + '?mode=ro', uri=True) as db:
        for label, episode, position in FIXTURES:
            row = db.execute('SELECT path,volume,sha256,reset_json FROM episodes WHERE id=?', (episode,)).fetchone()
            if row is None:
                raise ValueError(f'Missing episode {episode}')
            member, volume, expected_sha, reset = row
            archive = (archive_root / volume).resolve()
            if not archive.is_relative_to(archive_root.resolve()):
                raise ValueError('Archive volume escapes declared archive root')
            with zipfile.ZipFile(archive) as zipped:
                raw = zipped.read(member)
            actual_sha = hashlib.sha256(raw).hexdigest()
            if actual_sha != expected_sha:
                raise ValueError(f'Archive member SHA mismatch: {episode}')
            doc = json.loads(raw)
            trace = doc['trace']
            expected = trace[position]['before']
            prefix = [x['action'] for x in doc.get('curriculum_prefix', [])]
            prefix += [x['action'] for x in trace[:position]]
            result.append(dict(label=label, task=json.loads(reset)['task'], prefix=prefix,
                               expected=expected, provenance=dict(episode=episode, step=position,
                               archive=str(archive), member=member, source_sha256=actual_sha,
                               expected_phase=expected['phase'])))
    return result


def compare(expected, observation):
    from structured_ui_ppo import state
    native = json.loads(state(observation)['audit.costNativeState'])
    aligned, _ = align(expected, native)
    return native, differences(expected, aligned)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out', type=Path, required=True, help='New immutable experiment/baseline/key_phases or candidate/key_phases folder')
    parser.add_argument('--database', type=Path, default=DEFAULT_DB)
    parser.add_argument('--archives', type=Path, default=DEFAULT_ARCHIVES)
    parser.add_argument('--attach-live', type=Path, help='Existing live pilot RPC directory')
    parser.add_argument('--execute', action='store_true')
    parser.add_argument('--validate-only', action='store_true')
    parser.add_argument('--operator-fresh-launch', action='store_true', help='Operator commits to editor stop/start of normal pilot after capture')
    args = parser.parse_args(argv)
    if args.execute and args.validate_only:
        parser.error('--execute and --validate-only are mutually exclusive')
    if args.execute and not args.operator_fresh_launch:
        parser.error('Live capture requires --operator-fresh-launch; fixture state must never leak into an RSI round')
    if args.attach_live and not args.attach_live.is_dir():
        parser.error('--attach-live must name an existing directory')
    out = args.out.resolve()
    out.mkdir(parents=True, exist_ok=False)
    records = []
    live_started = False
    try:
        optimized = design_default()
        fixtures = load_fixtures(args.database, args.archives)
        save(out / 'fixtures.json', dict(fixtures=fixtures, optimizedInteractionUi=optimized,
             design_source=str(DESIGN_SOURCE), design_source_sha256=sha_file(DESIGN_SOURCE),
             role='native_fixture_setup_not_player_clicks', mode='live' if args.execute else 'pure_validation'))
        if not args.execute:
            save(out / 'validation.json', dict(passed=True, selected_entries=len(fixtures), live_operations=0))
            print(json.dumps(dict(validated=len(fixtures), live_operations=0, out=str(out))))
            return 0
        # Import the live transport only after the pure validation gate.
        from capture_ui_design_pairs import CaptureDemo
        from structured_ui_ppo import state
        live_started = True
        demo = CaptureDemo(args.attach_live or out / 'live', attach=bool(args.attach_live))
        for fixture in fixtures:
            directory = out / fixture['label']
            directory.mkdir()
            demo.reset_task(fixture['task'])
            observation = demo.prepare_prefix(fixture['prefix'])
            native, diffs = compare(fixture['expected'], observation)
            save(directory / 'prepared.json', dict(observation=observation, native_state=native, differences=diffs))
            if diffs:
                raise AssertionError(dict(label=fixture['label'], differences=diffs))
            demo.design(optimized)  # Current source design only; never hard-code old candidate.
            result = demo.call('observe', screenshot=True)
            observation = result['observation']
            native, diffs = compare(fixture['expected'], observation)
            save(directory / 'observation.json', dict(observation=observation, native_state=native,
                 differences=diffs, provenance=fixture['provenance'],
                 fixture_audit=json.loads(state(observation)['audit.costFixture']),
                 role='actual_unity_screenshot_after_native_fixture_setup', player_click_count=0))
            screenshot = Path(result['screenshotPath'])
            if not screenshot.is_file() or screenshot.stat().st_size == 0:
                raise ValueError('Unity returned no actual screenshot')
            destination = directory / 'screen.png'
            shutil.copyfile(screenshot, destination)
            if diffs:
                raise AssertionError(dict(label=fixture['label'], screenshot_state_differences=diffs))
            record = dict(label=fixture['label'], screenshot=str(destination),
                          screenshot_sha256=sha_file(destination), provenance=fixture['provenance'])
            save(directory / 'capture.json', record)
            records.append(record)
        save(out / 'manifest.json', dict(captures=records, passed=True,
             optimizedInteractionUi=optimized, player_click_count=0,
             scope='Actual same-fixture phase screenshots; no bulk simulation, training or timing claims'))
        return 0
    except Exception as error:
        save(out / 'failure.json', dict(error=repr(error), traceback=traceback.format_exc(), captures=records))
        raise
    finally:
        if live_started:
            save(out / 'operator_fresh_launch_required.json', dict(required=True,
                 instruction='Sole operator must editor-stop and start the normal RSI pilot before further play. Capture left native fixture state; never resume this raw state.',
                 role='explicit_operator_cleanup_obligation', automatically_restored=False))


if __name__ == '__main__':
    raise SystemExit(main())
