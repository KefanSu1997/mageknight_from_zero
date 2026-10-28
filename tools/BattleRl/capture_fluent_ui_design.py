"""Capture a few rendered design screens; no rollout or timing benchmark."""
import json
import shutil
from pathlib import Path
from structured_ui_ppo import Demo, ROOT, write_json

OUT=ROOT/'AutomationOutputs/UiFluency/ui_iteration'


def main():
    import time
    try:
        demo=Demo(OUT/('preview_'+str(int(time.time()))))
    except RuntimeError:
        # Unknown menu outcome: attach to the exact directory once, never relaunch.
        demo=Demo.__new__(Demo)
        demo.directory=Path((ROOT/'AutomationOutputs/structured_ui_pilot_directory.txt').read_text().strip())
        demo.calls=0
        demo.initial_observation=demo.call('observe',timeout=60)['observation']
    obs=demo.initial_observation
    records=[]
    def click(name,label=None):
        nonlocal obs
        controls=[c for c in obs['controls'] if c['name']==name and c['clickable']]
        if len(controls)!=1:raise ValueError((name,len(controls)))
        r=demo.call('click',obs,controls[0],screenshot=label is not None)
        obs=r['observation'];records.append(dict(name=name,response=r))
        if label:
            # Screenshot path is returned by the existing Unity capture API.
            screenshot=r.get('screenshotPath') or r.get('screenshot')
            if not isinstance(screenshot,str):
                paths=list((demo.directory/'captures').glob(r['id']+'.png'))
                if len(paths)!=1:raise ValueError(('Screenshot not returned',list(r)))
                screenshot=str(paths[0])
            shutil.copyfile(screenshot,OUT/(label+'.png'))
            write_json(OUT/(label+'.json'),dict(observation=obs,source_screenshot=screenshot,role='rendered_design_preview_not_cost_measurement'))
    try:
        for name in ('Btn_Hero_Tovak','Btn_BattleHero','Btn_Tactic_day_1'):
            click(name)
        click('Btn_BeginTurn','outside')
        click('Btn_EndTurn','turn_review')
        click('Btn_SelectAllOrdinary','turn_review_selected')
        click('Btn_CancelEndTurn')
        click('Btn_JourneyEncounter_0','battle')
        click('Btn_QuickMana','mana')
        candidates=[c for c in obs['controls'] if c['name'].startswith('Btn_Source_') and c['clickable']]
        if candidates:
            click(candidates[0]['name'])
            click('Btn_UseSourceReturn','mana_return')
        write_json(OUT/'rendered_preview_evidence.json',dict(scope='Screenshots and layout navigation only; no rollout/timing data',steps=records))
    finally:
        demo.close()
    print(json.dumps(dict(output=str(OUT),screenshots=[p.name for p in OUT.glob('*.png')]),ensure_ascii=False))


if __name__=='__main__':main()
