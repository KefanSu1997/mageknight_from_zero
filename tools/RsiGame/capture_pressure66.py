"""Real four-unit/three-enemy views and fourth-guard consumer, unchanged task."""
import argparse
import json
from pathlib import Path
from capture_evaluation_cycle import Collector, save
from capture_embedded_asset_cases import dismiss_hover, check_unit_names
from validate_four_unit_battle_layout import FIGHT
from structured_ui_ppo import state

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--out', type=Path, required=True)
    p.add_argument('--live', type=Path, required=True)
    a = p.parse_args()
    a.out.mkdir(parents=True)
    c = Collector(a.out.resolve(), a.live.resolve())
    previous = json.loads((BASE / 'attempt60/four_unit_validation/manifest.json').read_text(encoding='utf-8'))
    rows = []
    for label, task in [('four_units', previous['task']), ('four_units_three_enemies', previous['three_enemy_task'])]:
        c.demo.reset_task(task)
        c.demo.prepare_prefix([FIGHT])
        c.rpc('observe')
        dismiss_hover(c)
        r = c.snapshot(label)
        check_unit_names(r)
        before = state(c.observation)['audit.costNativeState']
        native = json.loads(before)
        assert len(native['units']) == 4 and native['commandSlots'] == 4
        for i in range(4):
            c.click('Btn_UnitActor_' + str(i))
            dismiss_hover(c)
            assert state(c.observation)['audit.costNativeState'] == before
            assert json.loads(state(c.observation)['audit.costFixture'])['uiUnit'] == task['Build']['UnitIds'][i]
        c.snapshot(label + '_fourth_selected')
        if label == 'four_units':
            c.click('Btn_Next')
            c.click('Btn_UnitActor_3')
            dismiss_hover(c)
            prior = json.loads(state(c.observation)['audit.costNativeState'])
            available = [b for b in c.observation['controls'] if b['name'].startswith('Btn_UnitAbility_') and b['clickable']]
            assert len(available) == 1, available
            c.snapshot('fourth_guard_before')
            c.click(available[0]['name'])
            dismiss_hover(c)
            after = json.loads(state(c.observation)['audit.costNativeState'])
            assert after['block'] - prior['block'] == 5 and not after['units'][3]['ready']
            c.snapshot('fourth_guard_consumed')
        rows.append(dict(label=label, task=task, native=native, four_actor_queries_native_unchanged=True))
    save(a.out / 'manifest.json', dict(passed=True, rows=rows, captures=c.captures,
         fourth_guard_actual_block_delta=5,
         scope='Two existing conditional level7 tasks, four native unit queries and one real fourth-guard Block consumer. Not every party, input-error rate, all rules or human acceptance.'))
    print('Four units, three enemies, actual fourth guard +5/exhaustion verified')


if __name__ == '__main__':
    main()
