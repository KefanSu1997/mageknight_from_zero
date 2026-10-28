"""Keep actual fourth-unit selection and native quick damage planning reachable."""
import argparse,json
from pathlib import Path
from capture_evaluation_cycle import Collector,save
from capture_embedded_asset_cases import dismiss_hover
from capture_phase_baseline import design_default
from validate_four_unit_battle_layout import FIGHT
from structured_ui_ppo import state

def main():
 p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);p.add_argument('--live',type=Path,required=True);p.add_argument('--task',type=Path,required=True);a=p.parse_args();a.out.mkdir();c=Collector(a.out.resolve(),a.live.resolve());task=json.loads(a.task.read_text(encoding='utf8'))
 try:
  c.demo.reset_task(task);c.demo.prepare_prefix([FIGHT]);c.demo.design(True);c.rpc('observe');dismiss_hover(c)
  c.click('Btn_Next');c.click('Btn_Next');assert state(c.observation)['phase']=='AssignDamage'
  c.click('Btn_Target');c.click('Btn_Roster');dismiss_hover(c);c.snapshot('damage_roster_top');native=json.loads(state(c.observation)['audit.costNativeState'])
  c.click('Btn_ChoiceScrollDown');dismiss_hover(c);c.snapshot('damage_roster_lower');assert json.loads(state(c.observation)['audit.costNativeState'])==native
  c.click('Btn_Unit_3');dismiss_hover(c);c.snapshot('fourth_unit_selected');assert json.loads(state(c.observation)['audit.costFixture'])['uiUnit']==task['Build']['UnitIds'][3]
  c.click('Btn_QuickAssignUnit_3');dismiss_hover(c);c.snapshot('fourth_unit_added_to_plan');after=json.loads(state(c.observation)['audit.costNativeState']);assert task['Build']['UnitIds'][3] in state(c.observation)['units.plan']
  for k in native:
   if k!='unitDamagePlan':assert after[k]==native[k],(k,after[k],native[k])
  c.click('Btn_QuickAssignUnit_3');dismiss_hover(c);c.snapshot('fourth_unit_removed_from_plan');assert task['Build']['UnitIds'][3] not in state(c.observation)['units.plan']
  save(a.out/'manifest.json',dict(passed=True,optimized_interaction_ui=True,captures=c.captures,actual_tray_navigation=True,fourth_unit_selected=True,quick_assignment_toggle=True,only_native_damage_plan_changed=True,scope='One real four-unit AssignDamage context with the existing optimized shortcuts enabled explicitly; planning toggle not damage settlement or all rosters.'))
  print('PASS four-unit damage tray and native quick plan toggle',flush=True)
 except Exception as e:save(a.out/'failure.json',dict(error=repr(e),captures=c.captures));raise

if __name__=='__main__':main()
