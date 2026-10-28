"""Actual pointer evidence for party availability, consumption and on-demand reading."""
import argparse
import json
from pathlib import Path
from capture_evaluation_cycle import Collector, save
from capture_embedded_asset_cases import dismiss_hover
from structured_ui_ppo import state


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--out', type=Path, required=True)
    p.add_argument('--live', type=Path, required=True)
    p.add_argument('--three-enemy', type=Path, required=True)
    p.add_argument('--prepared-fixture', action='store_true', help='Use an archived native fixture wrapper, without replaying the full phase suite.')
    args = p.parse_args()
    args.out.mkdir(parents=True, exist_ok=False)
    c = Collector(args.out.resolve(), args.live.resolve())
    try:
        if args.prepared_fixture:
            c.prepare(json.loads(args.three_enemy.read_text(encoding='utf8'))['fixture'])
        else:
            c.run(args.three_enemy)
        dismiss_hover(c)
        c.snapshot('three_enemies_neutral')
        c.click('Btn_UnitActor_1')
        dismiss_hover(c)
        c.snapshot('unit_unavailable_in_ranged')
        c.click('Btn_Next')
        c.click('Btn_UnitActor_1')
        dismiss_hover(c)
        c.snapshot('unit_selected_in_block')
        before = state(c.observation)
        c.click('Btn_UnitAbility_1')
        dismiss_hover(c)
        c.snapshot('unit_consumed')
        after = state(c.observation)
        assert int(after['block']) - int(before['block']) == 4
        button = next(x for x in c.observation['controls'] if x['name'] == 'Btn_UnitAbility_1')
        assert not button['interactable'] and not button['clickable']
        native = json.loads(after['audit.costNativeState'])
        assert not native['units'][1]['ready']
        save(args.out / 'unit_consumer.json', dict(passed=True, before=before, after=after, button_after=button,
            scope='Original block4 resource production, exhaustion and ordinary readiness UI guard; not whole-battle or Masters/leadership acceptance.'))
        for topic, label in [(2, 'wiki_block_risk'), (4, 'wiki_selected_unit_fulltext')]:
            if topic == 4:
                c.click('Btn_SelectedDetails')
            else:
                c.click('Btn_GlobalWiki')
                c.click('Btn_WikiTopic_' + str(topic))
            c.snapshot(label)
            c.click('Btn_ReturnCombat')
            assert state(c.observation)['audit.costNativeState'] == after['audit.costNativeState']
        c.rpc('observe')
        card = next(x for x in c.observation['controls'] if x['name'].startswith('Btn_Card_') and x['clickable'])
        c.rpc('hover', path=card['path'], expectedVersion=c.observation['version'])
        c.snapshot('card_hover_after_unit_consumption')
        assert state(c.observation)['ui.hoverPreviewVisible'] == 'True'
        assert state(c.observation)['audit.costNativeState'] == after['audit.costNativeState']
        c.rpc('unhover', path=card['path'], expectedVersion=c.observation['version'])
        c.click('Btn_Target')
        dismiss_hover(c)
        c.snapshot('enemy_target_selected_after_unit_consumption')
        save(args.out / 'party_feedback_manifest.json', dict(passed=True, captures=c.captures,
            checks=['block4_consumed_and_disabled', 'wiki_block_risk_open_return', 'selected_unit_fulltext_open_return', 'actual_card_hover', 'actual_enemy_target_selection'],
            scope='Actual scripted interactions, unchanged native state for reading. Screenshots need independent visual review.'))
    except Exception as error:
        save(args.out / 'party_failure.json', dict(error=repr(error), captures=c.captures))
        raise


if __name__ == '__main__':
    main()
