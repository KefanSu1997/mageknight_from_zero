"""Formal five-slot build validates legacy multirow fallback, not redesign acceptance."""
import argparse
import json
from pathlib import Path
from capture_evaluation_cycle import Collector, save
from capture_embedded_asset_cases import dismiss_hover, check_unit_names
from validate_four_unit_battle_layout import FIGHT
from structured_ui_ppo import state


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--out', type=Path, required=True)
    p.add_argument('--live', type=Path, required=True)
    a = p.parse_args()
    a.out.mkdir(parents=True)
    root = Path(__file__).resolve().parents[2]
    source = root / 'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1/attempt60/four_unit_validation/manifest.json'
    task = json.loads(source.read_text(encoding='utf-8'))['task']
    task['Build']['Fame'] = 80
    task['Build']['Family'] = 'ui-five-unit-legacy-compatibility'
    task['Build']['UnitIds'].append('army_004')
    save(a.out / 'task.json', task)
    c = Collector(a.out.resolve(), a.live.resolve())
    c.demo.reset_task(task)
    c.demo.prepare_prefix([FIGHT])
    c.rpc('observe')
    dismiss_hover(c)
    r = c.snapshot('five_units')
    check_unit_names(r)
    native = state(c.observation)['audit.costNativeState']
    n = json.loads(native)
    assert len(n['units']) == 5 and n['commandSlots'] == 5
    folios = [v['screenRect'] for v in r['evaluation']['presentationSurfaces'] if v['name'] == 'UnitIdentityFolio66']
    assert len(folios) == 5
    assert all(0 <= q['x'] and 0 <= q['y'] and q['x'] + q['width'] <= 1920.2 and q['y'] + q['height'] <= 1080.2 for q in folios)
    for i in range(5):
        c.click('Btn_UnitActor_' + str(i))
        dismiss_hover(c)
        check_unit_names(c.snapshot('selected_' + str(i)))
        assert state(c.observation)['audit.costNativeState'] == native
    save(a.out / 'manifest.json', dict(passed=True, task=task, actual_units=5, command_slots=5, all_five_actual_actor_clicks_preserve_native=True, five_label_backgrounds_on_canvas=True, captures=c.captures, events=c.events, scope='One formal Fame80/level9 native reset and FIGHT, five original units; legacy row positions retained. No five-unit design acceptance, no all labels/art overlap or whole rules/human claim.'))
    print('Formal five-unit legacy fallback and five actor selections verified')


if __name__ == '__main__':
    main()
