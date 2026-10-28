"""Open actual learned skill entries, including interactive input panels, for seven hero builds."""
import argparse,json
from pathlib import Path
from capture_evaluation_cycle import Collector,save
from capture_embedded_asset_cases import choose_fixture,dismiss_hover
from capture_phase_baseline import design_default
from validate_party_choice_band import text_check
from validate_four_unit_battle_layout import FIGHT
from structured_ui_ppo import state

def main():
 p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);p.add_argument('--live',type=Path,required=True);a=p.parse_args();a.out.mkdir();rows=[]
 for hero in ['Arythea','Braevalar','Goldyx','Krang','Norowas','Tovak','Wolfhawk']:
  directory=a.out/hero;directory.mkdir();c=Collector(directory.resolve(),a.live.resolve());fixture=choose_fixture('hero',hero)
  try:
   c.demo.reset_task(fixture['task']);c.demo.prepare_prefix([FIGHT]);c.demo.design(design_default());c.rpc('observe');dismiss_hover(c)
   native=state(c.observation)['audit.costNativeState'];n=json.loads(native);assert n['hero']==hero
   save(directory/'formal_entry.json',dict(task=fixture['task'],prefix=[FIGHT],native=n,role='fresh_formal_entry_not_archived_midgame_prefix'))
   def restore_choices():
    for _ in range(6):
     controls=c.observation['controls']
     if any(x['name'].startswith('Btn_Skill_') and x['clickable'] for x in controls):return
     if any(x['name']=='Btn_Back' and x['clickable'] for x in controls):c.click('Btn_Back')
     elif any(x['name']=='Btn_Skills' and x['clickable'] for x in controls):c.click('Btn_Skills')
     else:raise AssertionError('No native-preserving path back from actual skill UI')
     assert state(c.observation)['audit.costNativeState']==native
     dismiss_hover(c)
    raise AssertionError('Skill entry return did not converge')
   if n['skills']:
    restore_choices();r=c.snapshot('skills_open');text_check(r)
    choices=[x['name'] for x in c.observation['controls'] if x['name'].startswith('Btn_Skill_') and x['clickable']];assert len(choices)==len(n['skills'])
    for name in choices:
     restore_choices();c.click(name);dismiss_hover(c);assert state(c.observation)['audit.costNativeState']==native;c.snapshot(name+'_entry')
    restore_choices();c.click('Btn_Skills');dismiss_hover(c);assert state(c.observation)['audit.costNativeState']==native
   rows.append(dict(hero=hero,learned_skills=len(n['skills']),actual_entry_and_return=True,captures=c.captures))
   print(hero,len(n['skills']),'actual skill entries passed',flush=True)
  except Exception as e:save(directory/'failure.json',dict(error=repr(e),captures=c.captures));raise
 save(a.out/'manifest.json',dict(passed=True,rows=rows,scope='Seven actual original hero builds after fresh native reset/fight; every currently learned entry opened and returned including input panels, preserving native state. Not all70 skills or activation/consumer/whole-game acceptance.'))

if __name__=='__main__':main()
