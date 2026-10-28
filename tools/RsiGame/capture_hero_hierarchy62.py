"""Actual seven-hero scale hierarchy; same native tasks as pre-lighting60."""
import argparse,json
from pathlib import Path
from capture_evaluation_cycle import Collector,save
from capture_embedded_asset_cases import choose_fixture,dismiss_hover
from capture_phase_baseline import design_default
from validate_four_unit_battle_layout import FIGHT
from structured_ui_ppo import state

def main():
 p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);p.add_argument('--live',type=Path,required=True);p.add_argument('--baseline',type=Path);a=p.parse_args();a.out.mkdir(parents=True);rows=[]
 c=Collector(a.out.resolve(),a.live.resolve())
 for hero in ['Arythea','Braevalar','Goldyx','Krang','Norowas','Tovak','Wolfhawk']:
  fixture=json.loads((a.baseline/'setups'/f'{hero}.json').read_text(encoding='utf8')) if a.baseline else choose_fixture('hero',hero)
  save(a.out/'setups'/f'{hero}.json',fixture)
  c.demo.reset_task(fixture['task']);c.demo.prepare_prefix([FIGHT]);c.demo.design(design_default());c.rpc('observe');dismiss_hover(c)
  native=state(c.observation)['audit.costNativeState'];assert json.loads(native)['hero']==hero
  r=c.snapshot(hero)
  assert (r['observation']['screenWidth'],r['observation']['screenHeight'])==(1920,1080)
  image=next(x for x in r['evaluation']['rawImages'] if x['path'].endswith('Btn_HeroActor/PartyFigure'))
  assert image['shaderSupported']
  if a.baseline:
   b=json.loads((a.baseline/'key_phases'/hero/'capture.json').read_text(encoding='utf8'))
   assert state(b['observation'])['audit.costNativeState']==native
   old={x['path']:x for x in b['evaluation']['rawImages']};new={x['path']:x for x in r['evaluation']['rawImages']}
   assert old.keys()==new.keys()
   for k in old:
    for field in ['texture','uvRect','material','atmosphereSaturation','atmosphereTop','atmosphereGround']:assert old[k][field]==new[k][field],(hero,k,field)
    if 'Btn_HeroActor/' not in k:
     for field in ['width','height']:assert abs(old[k]['screenRect'][field]-new[k]['screenRect'][field])<.001,(hero,k,field)
   bc={x['path']:x for x in b['observation']['controls']};ac={x['path']:x for x in r['observation']['controls']}
   assert bc.keys()==ac.keys()
   for k in bc:
    for f in ['clickable','interactable']:assert bc[k][f]==ac[k][f],(hero,k,f)
    if 'Btn_HeroActor' not in k and 'Btn_UnitActor_' not in k:
     for f in ['x','y','width','height']:assert bc[k][f]==ac[k][f],(hero,k,f)
   assert image['material'].startswith('ForestAtmosphere_'),image['material']
   ot={t['path']:t for t in b['evaluation']['texts'] if '/BattleFeedback/' not in t['path']};nt={t['path']:t for t in r['evaluation']['texts'] if '/BattleFeedback/' not in t['path']}
   assert ot.keys()==nt.keys()
   for k in ot:
    for f in ['fullText','fontSize']:assert ot[k][f]==nt[k][f],(hero,k,f)
  rows.append(dict(hero=hero,hero_material=image['material'],native=json.loads(native),same_native_and_material=bool(a.baseline)))
  print(hero,'captured',flush=True)
 save(a.out/'manifest.json',dict(passed=True,rows=rows,captures=c.captures,scope='Seven actual formal native entries, same-task before/after. Hero enlarged; allies translate only; all textures/fullUV/material/type sizes/content retained. Remaining controls geometry and enabledness unchanged. No all-phase/human recognition claim.'))
if __name__=='__main__':main()
