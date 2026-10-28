"""Current full-rule entry, base/strong choice and real blue-token consumer."""
import argparse
import json
from pathlib import Path
from capture_evaluation_cycle import Collector, save
from capture_embedded_asset_cases import dismiss_hover
from structured_ui_ppo import state
from validate_entity_hover_reader import inside


def main():
    p = argparse.ArgumentParser(description=__doc__)
    for n in ['out', 'live', 'fixture']:
        p.add_argument('--' + n, type=Path, required=True)
    a = p.parse_args()
    a.out.mkdir(parents=True)
    c = Collector(a.out.resolve(), a.live.resolve())
    c.prepare(json.loads(a.fixture.read_text(encoding='utf-8'))['fixture'])
    c.click('Btn_Card_basic_card_006_0')
    dismiss_hover(c)
    unchanged = state(c.observation)['audit.costNativeState']
    readers = []
    for mode in ['base', 'strong']:
        c.click('Btn_Choice_' + mode)
        dismiss_hover(c)
        r = c.snapshot('focus_' + mode)
        assert state(c.observation)['audit.costNativeState'] == unchanged
        path = next(s['path'].rsplit('/', 1)[0] for s in r['evaluation']['presentationSurfaces']
                    if s['name'] == 'Sigil' and '/FieldFact_CurrentCardEffect/' in s['path'])
        c.rpc('hover_info', path=path, expectedVersion=c.observation['version'])
        r = c.snapshot('focus_' + mode + '_full_rule')
        vp = next((s['screenRect'] for s in r['evaluation']['presentationSurfaces'] if s['name'] == 'EntityHoverViewport'), None)
        if vp is None:
            vp = next(s['screenRect'] for s in r['evaluation']['presentationSurfaces'] if s['name'] == 'BattleHoverInfo')
        body = next(t for t in r['evaluation']['texts'] if t['name'] == 'Body' and '/BattleHoverInfo/' in t['path'])
        assert body['fullText'] and body['fontSize'] >= 22 and not body['isTextOverflowing']
        assert {i for i, ch in enumerate(body['fullText']) if not ch.isspace()}.issubset({g['sourceIndex'] for g in body['glyphs']})
        assert all(inside(g['screenRect'], vp, .2) for g in body['glyphs'])
        assert state(c.observation)['audit.costNativeState'] == unchanged
        readers.append(dict(mode=mode, text=body['fullText'], all_glyphs_in_actual_viewport=True, source_path=path))
        c.rpc('unhover_info', path=path, expectedVersion=c.observation['version'])
        dismiss_hover(c)
    c.click('Btn_Choice_base')
    c.click('Btn_Choice_blue')
    dismiss_hover(c)
    c.snapshot('blue_before_consume')
    before = json.loads(state(c.observation)['audit.costNativeState'])
    c.click('Btn_Play')
    dismiss_hover(c)
    c.snapshot('blue_consumed')
    after = json.loads(state(c.observation)['audit.costNativeState'])
    assert after['tokens'].get('Blue', 0) - before['tokens'].get('Blue', 0) == 1
    assert after['hand'].count('basic_card_006') == before['hand'].count('basic_card_006') - 1
    save(a.out / 'manifest.json', dict(passed=True, readers=readers, before_native=before, after_native=after,
         captures=c.captures, events=c.events,
         scope='One existing focus-card fixture, real base/strong UI choices and visible full-rule readers; one formal base-blue consumer. Not enhanced payment/all card modes or human/global cost acceptance.'))
    print('Base/strong current readers and actual blue-token/card consumption passed')


if __name__ == '__main__':
    main()
