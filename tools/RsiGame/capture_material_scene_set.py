"""Reusable seven actual character contexts for versioned screenshot comparisons."""
import argparse,json
from pathlib import Path
from capture_evaluation_cycle import Collector,save
from capture_embedded_asset_cases import dismiss_hover
from validate_four_unit_battle_layout import FIGHT
from structured_ui_ppo import state

ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'

def main():
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);p.add_argument('--live',type=Path,required=True);a=p.parse_args()
    c=Collector(a.out.resolve(),a.live.resolve())
    for mid in ('037','066'):
        fixture=json.loads((BASE/'attempt47/after/setups'/('monster_monster_'+mid+'.json')).read_text())['fixture']
        c.prepare(fixture);dismiss_hover(c);c.snapshot('monster_'+mid+'_neutral')
        c.click('Btn_Target');dismiss_hover(c);c.snapshot('monster_'+mid+'_target_selected')
    fixture=json.loads((BASE/'attempt47/after/setups/three_enemies.json').read_text())['fixture']
    c.prepare(fixture);dismiss_hover(c);c.snapshot('three_enemies_neutral')
    task=json.loads((BASE/'attempt47/four_unit_material_task.json').read_text())
    c.demo.reset_task(task);c.demo.prepare_prefix([FIGHT]);c.demo.design(False);c.rpc('observe');dismiss_hover(c)
    c.snapshot('four_allies_new_materials');c.click('Btn_Target',occurrence=0);dismiss_hover(c);c.snapshot('four_allies_gold_selected')
    save(a.out/'manifest.json',dict(passed=True,captures=c.captures,scope='Seven actual standalone/three-enemy/four-ally material contexts. Formal native setup separately recorded. Visual review and version comparisons separate.'))
    print('Seven material scene contexts captured',flush=True)

if __name__=='__main__':main()
