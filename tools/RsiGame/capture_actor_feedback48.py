"""Capture actual corrected actor response, retaining47 source screenshots as baseline."""
import json, shutil
from pathlib import Path
from capture_evaluation_cycle import Collector, save
from capture_ui_design_pairs import CaptureDemo
from capture_embedded_asset_cases import dismiss_hover
from structured_ui_ppo import state
from validate_four_unit_battle_layout import FIGHT

ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'
OUT=BASE/'attempt48'

def main():
    demo=CaptureDemo(OUT/'live')
    c=Collector((OUT/'after').resolve(),(OUT/'live').resolve())
    for mid in ('037','066'):
        fixture=json.loads((BASE/'attempt47/after/setups'/('monster_monster_'+mid+'.json')).read_text())['fixture']
        c.prepare(fixture);dismiss_hover(c);c.snapshot('monster_'+mid+'_neutral')
        c.click('Btn_Target');dismiss_hover(c);c.snapshot('monster_'+mid+'_target_selected')
    task=json.loads((BASE/'attempt47/four_unit_material_task.json').read_text())
    c.demo.reset_task(task);c.demo.prepare_prefix([FIGHT]);c.demo.design(False);c.rpc('observe');dismiss_hover(c)
    c.snapshot('four_allies_new_materials');c.click('Btn_Target',occurrence=0);dismiss_hover(c);c.snapshot('four_allies_gold_selected')
    matched=[]
    for item in c.captures:
        label=item['label'];prior=BASE/'attempt47/after/key_phases'/label
        old=json.loads((prior/'capture.json').read_text());new=json.loads(Path(item['capture']).read_text())
        assert state(old['observation'])['audit.costNativeState']==state(new['observation'])['audit.costNativeState']
        target=OUT/'before/key_phases'/label;target.mkdir(parents=True)
        shutil.copy2(prior/'screen.png',target/'screen.png');shutil.copy2(prior/'capture.json',target/'capture.json')
        matched.append(label)
        if label.endswith('_selected'):
            surfaces={x['path']:x for x in new['evaluation']['presentationSurfaces']}
            # Decoration is separate from the original formal hit targets.
            controls={x['path']:x for x in old['observation']['controls']}
            for control in new['observation']['controls']:
                prev=controls.pop(control['path'])
                for k in ('x','y','width','height','interactable','clickable'):assert prev[k]==control[k],(label,k)
            assert not controls
    save(OUT/'after/manifest.json',dict(passed=True,captures=c.captures,matched_native_states=matched,
        baseline='47 actual archived captures copied with provenance; not claimed as freshly recaptured',
        scope='Six actual same-native scenes; selected-control hit geometry unchanged. Visual response correction requires independent review.'))
    print('48 six same-native actor feedback scenes captured',flush=True)

if __name__=='__main__':main()
