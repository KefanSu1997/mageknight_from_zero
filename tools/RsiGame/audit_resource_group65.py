"""Same native state, real numerical glyphs and retained battlefield artwork."""
import argparse
import hashlib
import json
from pathlib import Path
from capture_evaluation_cycle import save
from structured_ui_ppo import state
from validate_mana_inventory52 import inspect
from validate_entity_hover_reader import inside

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'
OLD = ROOT / 'AutomationOutputs/RsiGame/workflow_revisions/20261028_density_and_visual_v3_3/original_vs_current'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--candidate', default='final')
    parser.add_argument('--tag', default='final_typography')
    parser.add_argument('--sliced-housing', action='store_true')
    args = parser.parse_args()
    out = BASE / 'attempt65'
    rows = []
    for pair in read(out / args.candidate / 'manifest.json')['rows']:
        label = pair['label']
        previous = read(OLD / 'key_phases' / label / 'capture.json')
        actual = read(out / args.candidate / 'key_phases' / label / 'capture.json')
        native = json.loads(state(actual['observation'])['audit.costNativeState'])
        assert state(actual['observation'])['audit.costNativeState'] == state(previous['observation'])['audit.costNativeState']
        old_images = {x['path']: x for x in previous['evaluation']['rawImages']}
        new_images = {x['path']: x for x in actual['evaluation']['rawImages']}
        resource_hud = native['phase'] != 'Outside'
        expected_extra = {'Canvas/BattleTable/ManaReliquary'} if resource_hud and not args.sliced_housing else set()
        assert set(new_images) - set(old_images) == expected_extra, label
        if resource_hud and args.sliced_housing:
            housing = next(s for s in actual['evaluation']['presentationSurfaces'] if s['name'] == 'ManaReliquary')
            assert housing['effectiveRendererAlpha'] > .99
            assert housing['screenRect']['width'] == 720 and housing['screenRect']['height'] == 155
        for path, old in old_images.items():
            for field in ['texture', 'screenRect', 'uvRect', 'material']:
                assert old[field] == new_images[path][field], (label, path, field)
        old_buttons = {c['path']: c for c in previous['observation']['controls']}
        new_buttons = {c['path']: c for c in actual['observation']['controls']}
        assert old_buttons.keys() == new_buttons.keys(), label
        for path, old in old_buttons.items():
            for field in ['clickable', 'interactable', 'x', 'y', 'width', 'height']:
                assert old[field] == new_buttons[path][field], (label, path, field)
        values = inspect(actual, unavailable_label='×') if resource_hud else []
        checked = []
        for t in actual['evaluation']['texts']:
            required = t['name'] == 'ManaColorIdentity' or t['name'].startswith('Result')
            if label in ['won_result', 'end', 'end_mixed'] and t['name'] == 'ContextTitle':
                required = True
                assert t['fullText'] == ('战斗胜利' if native['won'] else '战斗结束 · 未获胜')
            if not required:
                continue
            assert t['fontSize'] >= 21 and not t['isTextOverflowing'] and t['hasVisibleGlyphs'], (label, t['path'])
            assert {i for i, c in enumerate(t['fullText']) if not c.isspace()}.issubset({g['sourceIndex'] for g in t['glyphs']}), (label, t['path'])
            assert all(inside(g['screenRect'], t['screenRect'], 2) for g in t['glyphs']), (label, t['path'])
            checked.append(dict(path=t['path'], text=t['fullText'], size=t['fontSize']))
        if label in ['won_result', 'end', 'end_mixed']:
            fields = {t['name']: t['fullText'] for t in actual['evaluation']['texts']}
            assert fields['ResultDefeated'] == f"已击败 {len([e for e in native['enemies'] if e['defeated']])} / {len(native['enemies'])}"
            assert fields['ResultResources'] == f"名望 {native['fame']} · 手牌创伤 {native['wounds']}"
        plates = [s['name'] for s in actual['evaluation']['presentationSurfaces']]
        assert not any(s in plates for s in ['TokenRuneMount', 'CrystalRuneMount', 'ColorRuneMount'])
        rows.append(dict(label=label, same_native=True, resource_hud_applicable=resource_hud, retained_original_images=len(old_images),
                         same_buttons_and_hit_rectangles=True, resource_values=values, checked_texts=checked))
    receipt = read(out / ('compile_' + args.tag + '.json'))
    assert all(hashlib.sha256(Path(s['path']).read_bytes()).hexdigest() == s['sha256'] for s in receipt['sources'])
    save(out / ('audit_' + args.candidate + '.json'), dict(passed=True, rows=rows,
         runtime_mvid=receipt['runtimeModuleVersionId'],
         scope='Ten exact-native paired views: nine battle HUDs and one unchanged outside scene. Original art/geometry/UV/material and actual Button hit rectangles preserved; six true resource quantities, generated color names and three End summaries checked. No human contrast, full visual acceptance, all-content or all-phase claims.'))
    print('Ten paired states, native resource values, full result fields and unchanged artwork/buttons verified')


if __name__ == '__main__':
    main()
