"""Real hero entry/return and relocated unit status readers after UI68 reflow."""
import argparse
import json
from pathlib import Path
from capture_evaluation_cycle import Collector, save
from capture_embedded_asset_cases import dismiss_hover
from structured_ui_ppo import state


def main():
    p = argparse.ArgumentParser(description=__doc__)
    for n in ['out', 'live', 'fixture']:
        p.add_argument('--' + n, type=Path, required=True)
    a = p.parse_args()
    a.out.mkdir(parents=True)
    c = Collector(a.out.resolve(), a.live.resolve())
    f = json.loads(a.fixture.read_text(encoding='utf-8'))['fixture']
    c.prepare(f)
    dismiss_hover(c)
    native = state(c.observation)['audit.costNativeState']
    actor = next(x for x in c.observation['controls'] if x['name'] == 'Btn_HeroActor')
    c.rpc('hover_info', path=actor['path'], expectedVersion=c.observation['version'])
    r = c.snapshot('hero_details')
    details = [t for t in r['evaluation']['texts'] if t['fullText'] and ('/BattleHoverInfo/' in t['path'] or '/EntityHover' in t['path'])]
    assert details and any(t['hasVisibleGlyphs'] and t['fontSize'] >= 21 for t in details)
    assert state(c.observation)['audit.costNativeState'] == native
    c.rpc('unhover_info', path=actor['path'], expectedVersion=c.observation['version'])
    dismiss_hover(c)
    c.click('Btn_HeroActor')
    c.snapshot('hero_skills')
    skills = [x for x in c.observation['controls'] if x['name'].startswith('Btn_Skill_')]
    assert skills, 'Actual hero skills not exposed'
    assert state(c.observation)['audit.costNativeState'] == native
    c.click('Btn_HeroActor')
    c.snapshot('hero_return_hand')
    assert any(x['name'].startswith('Btn_Card_') and x['clickable'] for x in c.observation['controls'])
    assert state(c.observation)['audit.costNativeState'] == native
    readers = []
    for name in ['ReadinessSymbol', 'WoundSymbol']:
        c.prepare(dict(f, label='unit_' + name))
        dismiss_hover(c)
        path = 'Canvas/BattleTable/Btn_UnitActor_0/' + name
        native = state(c.observation)['audit.costNativeState']
        c.rpc('hover_info', path=path, expectedVersion=c.observation['version'])
        r = c.snapshot('unit_' + name)
        texts = [t for t in r['evaluation']['texts'] if t['fullText'] and '/BattleHoverInfo/' in t['path']]
        assert texts and any(t['hasVisibleGlyphs'] for t in texts)
        assert state(c.observation)['audit.costNativeState'] == native
        readers.append(dict(path=path, visible_text=[t['fullText'] for t in texts], unchanged_native=True))
        c.rpc('unhover_info', path=path, expectedVersion=c.observation['version'])
        dismiss_hover(c)
    save(a.out / 'manifest.json', dict(passed=True, hero_details_entry=True, actual_hero_skill_entries=len(skills), hero_return_hand=True, readers=readers, captures=c.captures, scope='Actual hero details and skill open/return and two relocated unit-status hover channels, native unchanged. Visible reader existence only, not complete scroll/full content or human usability certification.'))
    print('Hero details/skills return and moved unit-status readers verified')


if __name__ == '__main__':
    main()
