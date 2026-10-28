"""Live full-rule readers, guide navigation and a real guard consumer."""
import argparse,json
from pathlib import Path
from capture_evaluation_cycle import Collector,save
from capture_embedded_asset_cases import dismiss_hover
from structured_ui_ppo import state
from validate_entity_hover_reader import inside
ROOT=Path(__file__).resolve().parents[2];BASE=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'
def main():
 p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);p.add_argument('--live',type=Path,required=True);a=p.parse_args();a.out.mkdir(parents=True);c=Collector(a.out.resolve(),a.live.resolve())
 f=json.loads((BASE/'attempt56/after_final/setups/three_enemies.json').read_text(encoding='utf8'))['fixture'];rows=[]
 for label in ['card','target','trait']:
  c.prepare(dict(f,label=label));dismiss_hover(c)
  if label=='card':c.click(next(b['name'] for b in c.observation['controls'] if b['clickable'] and b['name'].startswith('Btn_Card_')))
  if label=='target':c.click('Btn_Target')
  dismiss_hover(c);neutral=c.snapshot(label+'_neutral');native=state(c.observation)['audit.costNativeState']
  paths=[x['path'].rsplit('/',1)[0] for x in neutral['evaluation']['presentationSurfaces'] if x['name']=='Sigil']
  path=next(x for x in paths if ('FieldFact_CurrentCardEffect' in x if label=='card' else 'FieldFact_Decision_3' in x if label=='target' else 'Enemy_0/Trait_0' in x))
  c.rpc('hover_info',path=path,expectedVersion=c.observation['version']);r=c.snapshot(label+'_reader_top');seen=set();body=None
  for i in range(45):
   texts=[t for t in r['evaluation']['texts'] if t['name']=='Body' and '/BattleHoverInfo/' in t['path']]
   if texts:
    t=texts[0];body=t['fullText'];vp=next(v['screenRect'] for v in r['evaluation']['presentationSurfaces'] if v['name']=='EntityHoverViewport')
    seen.update(g['sourceIndex'] for g in t['glyphs'] if inside(g['screenRect'],vp,.1))
   progress=next((t['fullText'] for t in r['evaluation']['texts'] if t['name']=='EntityReadingProgress'),'')
   if progress=='信息已显示' or '全文已显示' in progress or '100%' in progress:break
   c.rpc('scroll_info',path=path,expectedVersion=c.observation['version'],scrollY=-3);r=c.snapshot(label+'_wheel_'+str(i))
  else:raise AssertionError('Reader end not reached: '+label)
  assert body and {i for i,ch in enumerate(body) if not ch.isspace()}.issubset(seen),(label,len(body),len(seen))
  assert state(c.observation)['audit.costNativeState']==native
  c.rpc('unhover_info',path=path,expectedVersion=c.observation['version']);dismiss_hover(c);c.snapshot(label+'_returned')
  rows.append(dict(label=label,path=path,body=body,all_nonwhite_glyphs_visible_in_actual_viewport_frames=True,actual_scroll_events=i,same_native=True))
 c.prepare(dict(f,label='guide'));dismiss_hover(c);native=state(c.observation)['audit.costNativeState'];c.click('Btn_GlobalWiki');c.click('Btn_WikiTopic_7');c.snapshot('guide_top');c.click('Btn_ReadEnd');c.snapshot('guide_end');c.click('Btn_ReturnCombat');dismiss_hover(c);assert state(c.observation)['audit.costNativeState']==native
 c.prepare(dict(f,label='consumer'));c.click('Btn_Next');c.click('Btn_UnitActor_1');dismiss_hover(c);before=state(c.observation);c.snapshot('guard_ready')
 available=[b for b in c.observation['controls'] if b['name'].startswith('Btn_UnitAbility_') and b['clickable']];assert len(available)==1,available
 action_name=available[0]['name'];c.click(action_name);dismiss_hover(c);after=state(c.observation);c.snapshot('guard_consumed')
 assert int(after['block'])-int(before['block'])==4
 assert not json.loads(after['audit.costNativeState'])['units'][1]['ready']
 button=next(x for x in c.observation['controls'] if x['name']==action_name);assert not button['clickable'] and not button['interactable']
 save(a.out/'manifest.json',dict(passed=True,readers=rows,guide_real_open_scroll_end_return=True,guard_actual_block_delta=4,guard_exhausted=True,captures=c.captures,scope='Three actual full-rule hover channels and native guide return, one formal guard ability consumer. Generated glyph union is not a human reading test or all-content validation.'))
 print('Full readers, guide return and actual guard +4/exhaustion verified',flush=True)
if __name__=='__main__':main()
