"""Capture new character bindings in SHA-verified, unmodified archived rule states.

The sole live Unity operator must launch a fresh pilot first and restore the
normal pilot afterwards. This script neither invents builds nor trains a policy.
"""
import argparse
import hashlib
import json
import re
import sqlite3
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path[:0] = [str(ROOT / 'tools/BattleRl'), str(ROOT / 'tools/RsiGame')]
from capture_evaluation_cycle import Collector, save
from structured_ui_ppo import state


def check_unit_names(response):
    """Inspect generated runtime glyphs, not just the source caption string."""
    labels = []
    def visit(value):
        if isinstance(value, dict):
            if re.search(r'Btn_UnitActor_\d+/Identity$', value.get('path', '')) and 'fontSize' in value:
                labels.append(value)
            for child in value.values():
                visit(child)
        elif isinstance(value, list):
            for child in value:
                visit(child)
    visit(response['evaluation'])
    native = json.loads(state(response['observation'])['audit.costNativeState'])
    names = {}
    for filename in ['army.json', 'army_2.json']:
        names.update({x['id']: x['card_name_cn'] for x in json.loads((ROOT / 'resources/text_json' / filename).read_text(encoding='utf8')) if 'card_name_cn' in x})
    converted = {p.stem for p in (ROOT / 'Assets/Resources/UI/UnitCharacters/embedded_v1').glob('*.png')}
    required = {i for i, u in enumerate(native['units']) if u['id'] in converted}
    actual = {int(re.search(r'Btn_UnitActor_(\d+)/Identity$', x['path']).group(1)): x for x in labels}
    assert required == set(actual), (required, actual.keys())
    for i, label in actual.items():
        assert label['fullText'].replace('\n', '') == names[native['units'][i]['id']]
        assert label['fontSize'] >= 21 and label['hasVisibleGlyphs'] and not label['isTextOverflowing'], label
        expected_version = 'embedded_v2' if (ROOT / 'Assets/Resources/UI/UnitCharacters/embedded_v2' / (native['units'][i]['id']+'.png')).is_file() else 'embedded_v1'
        observed = state(response['observation'])
        if 'ui.unitActor.'+str(i)+'.resourceVersion' in observed:
            assert observed['ui.unitActor.'+str(i)+'.resourceVersion'] == expected_version
    return labels


def choose_fixture(kind, identity):
    database = ROOT / 'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite'
    with sqlite3.connect(database.resolve().as_uri() + '?mode=ro', uri=True) as db:
        if kind == 'hero':
            rows = db.execute("SELECT id,path,volume,sha256,reset_json FROM episodes WHERE json_extract(reset_json,'$.task.HeroId')=? ORDER BY id LIMIT 256", (identity,)).fetchall()
        elif kind == 'unit':
            rows = db.execute("SELECT id,path,volume,sha256,reset_json FROM episodes WHERE json_extract(reset_json,'$.task.Build.UnitIds') LIKE ? ORDER BY id LIMIT 256", ('%' + identity + '%',)).fetchall()
        else:
            rows = db.execute("SELECT id,path,volume,sha256,reset_json FROM episodes WHERE json_extract(reset_json,'$.task.EnemyGroups') LIKE ? ORDER BY id LIMIT 256", ('%' + identity + '%',)).fetchall()
    for episode, member, volume, expected_sha, reset in rows:
        task = json.loads(reset)['task']
        # The current native fixture contract requires exactly one of these.
        # Skip legacy definitions rather than inventing an inventory for them.
        if bool(task.get('OriginalOpening')) == (task.get('Build') is not None):
            continue
        archive = Path('H:/MageKnight_RL_Backups/20261012_before_reward_v2') / volume
        with zipfile.ZipFile(archive) as z:
            raw = z.read(member)
        if hashlib.sha256(raw).hexdigest() != expected_sha:
            raise ValueError('Archive member hash mismatch')
        document = json.loads(raw)
        for pos, step in enumerate(document['trace'][:80]):
            expected = step['before']
            if expected['phase'] != 'Ranged':
                continue
            if kind == 'monster' and not any(e['id'] == identity for e in expected['enemies']):
                continue
            if kind == 'unit' and not any(u['id'] == identity for u in expected['units']):
                continue
            prefix = [x['action'] for x in document.get('curriculum_prefix', [])]
            prefix += [x['action'] for x in document['trace'][:pos]]
            return dict(label=kind + '_' + identity, task=json.loads(reset)['task'], prefix=prefix, expected=expected,
                        provenance=dict(episode=episode, archive=str(archive), member=member, source_sha256=expected_sha, step=pos))
    raise ValueError('No archived Ranged fixture found: ' + identity)


def dismiss_hover(collector):
    """Dispatch actual pointer-exit events for neutral, non-hover evidence.

    This changes presentation only. A physical cursor can enter new objects after
    a fixture redraw, so we record actual visibility and fail if it remains open.
    """
    collector.rpc('observe')
    if state(collector.observation).get('ui.embeddedTooltipVisible') != 'True':
        return
    # A hovered choice may have scrolled offscreen before pointer exit. The
    # exit event is valid for that active control even when its center cannot
    # be clicked; filtering to clickable controls leaves its reader open.
    paths = [c['path'] for c in collector.observation['controls']]
    before = state(collector.observation)['audit.costNativeState']
    for path in paths:
        for attempt in range(3):
            collector.rpc('observe')
            try:
                collector.rpc('unhover', path=path, expectedVersion=collector.observation['version'])
                break
            except RuntimeError as error:
                # This exact bridge guard precedes event dispatch. Reobserve
                # only this known rejection; never replay unknown outcomes.
                if attempt==2 or not str(error).startswith('System.InvalidOperationException: Stale observation: reobserve before selecting an action.'):
                    raise
        if state(collector.observation).get('ui.embeddedTooltipVisible') != 'True':
            break
    if state(collector.observation)['audit.costNativeState'] != before:
        raise AssertionError('Pointer exit changed native state')
    if state(collector.observation).get('ui.embeddedTooltipVisible') == 'True':
        raise AssertionError('Primary screenshot still has an unintended tooltip')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--live', type=Path, required=True)
    parser.add_argument('--heroes', nargs='*', default=[])
    parser.add_argument('--monsters', nargs='*', default=[])
    parser.add_argument('--units', nargs='*', default=[])
    parser.add_argument('--before-import', action='store_true', help='Capture unchanged native fixtures and actual fallback bindings before importing assets; do not assert converted art.')
    parser.add_argument('--require-readable-unit-names', action='store_true')
    args = parser.parse_args()
    if args.out.exists():
        raise ValueError('Use a fresh evidence output directory')
    args.out.mkdir(parents=True)
    c = Collector(args.out.resolve(), args.live.resolve())
    results = []
    try:
        for kind, identities in [('hero', args.heroes), ('monster', args.monsters), ('unit', args.units)]:
            for identity in identities:
                fixture = choose_fixture(kind, identity)
                c.prepare(fixture)
                dismiss_hover(c)
                response = c.snapshot(fixture['label'])
                observed = state(response['observation'])
                name_geometry = check_unit_names(response) if args.require_readable_unit_names else []
                if args.before_import:
                    pass
                elif kind == 'hero':
                    assert observed['ui.heroActor.texture'] == identity
                elif kind == 'unit':
                    indexes = [i for i, u in enumerate(fixture['expected']['units']) if u['id'] == identity]
                    assert all(observed['ui.unitActor.' + str(i) + '.texture'] == identity for i in indexes)
                else:
                    indexes = [e['index'] for e in fixture['expected']['enemies'] if e['id'] == identity]
                    assert all(observed['ui.enemyArt.' + str(i) + '.texture'] == identity and
                               observed['ui.enemyArt.' + str(i) + '.characterBound'] == 'True' for i in indexes)
                results.append(dict(kind=kind, identity=identity, fixture=fixture['provenance'],
                    binding_verified=not args.before_import, role='before_import' if args.before_import else 'candidate',
                    unit_name_geometry=name_geometry,
                    actual_art_bindings={k:v for k,v in observed.items() if k.startswith(('ui.heroActor.', 'ui.unitActor.', 'ui.enemyArt.'))}))
                save(args.out / (fixture['label'] + '_result.json'), results[-1])
                print(kind, identity, 'baseline native fixture verified' if args.before_import else 'binding and native fixture verified', flush=True)
        save(args.out / 'manifest.json', dict(passed=True, results=results, captures=c.captures,
            scope='Before import: native snapshot equality and actual fallback bindings, no conversion assertion.' if args.before_import else 'Native snapshot equality and actual texture binding; screenshots still require visual review. No rule activation, whole-catalog or human acceptance claim.'))
    except Exception as error:
        save(args.out / 'failure.json', dict(error=repr(error), results=results, captures=c.captures))
        raise


if __name__ == '__main__':
    main()
