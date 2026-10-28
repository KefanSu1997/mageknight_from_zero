"""Actual selection/hover/native retention for the compact skills and roster tray."""
import argparse,json,time
from pathlib import Path
from capture_evaluation_cycle import Collector,save,load_fixtures
from capture_embedded_asset_cases import dismiss_hover,choose_fixture
from structured_ui_ppo import state
from validate_entity_hover_reader import inside

def text_check(r):
 rows=[]
 for t in r['evaluation']['texts']:
  if t['name'] not in ['ChoiceIdentity','ChoiceUsage']:continue
  assert t['fontSize'] >= (25 if t['name']=='ChoiceIdentity' else 22),t
  assert not t['isTextOverflowing'],t
  expected={i for i,ch in enumerate(t['fullText']) if not ch.isspace()}
  assert expected.issubset({g['sourceIndex'] for g in t['glyphs']}),t
  assert all(inside(g['screenRect'],t['screenRect'],3) for g in t['glyphs']),t
  rows.append(dict(path=t['path'],text=t['fullText'],font_size=t['fontSize']))
 return rows

def main():
 p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);p.add_argument('--live',type=Path,required=True);p.add_argument('--fixture',type=Path,required=True);a=p.parse_args()
 a.out.mkdir(exist_ok=False);c=Collector(a.out.resolve(),a.live.resolve());rows=[]
 fixture=json.loads(a.fixture.read_text(encoding='utf8'))['fixture']
 try:
  c.prepare(fixture);dismiss_hover(c);c.snapshot('neutral');native=state(c.observation)['audit.costNativeState']
  for control,label,prefix in [('Btn_Skills','skills','Btn_Skill_'),('Btn_Roster','roster','Btn_Unit_')]:
   c.click(control);dismiss_hover(c);r=c.snapshot(label+'_open');geometry=text_check(r)
   choices=[x for x in c.observation['controls'] if x['name'].startswith(prefix) and x['clickable']]
   expected=len(json.loads(native)['skills' if label=='skills' else 'units']);assert len(choices)==expected,(label,len(choices),expected)
   assert not any(x['name']=='Btn_NextSkill' for x in c.observation['controls'])
   for choice in choices:
    c.click(choice['name']);dismiss_hover(c);assert state(c.observation)['audit.costNativeState']==native
    r=c.snapshot(label+'_selected' if choice==choices[0] else label+'_selected_'+choice['name']);text_check(r)
    key='uiSkill' if label=='skills' else 'uiUnit';selected=json.loads(state(c.observation)['audit.costFixture'])[key];assert selected
   if choices:
    b=next(x for x in c.observation['controls'] if x['name']==choices[-1]['name'])
    c.rpc('hover',path=b['path'],expectedVersion=c.observation['version']);r=c.snapshot(label+'_full_rule_hover')
    body=next(t for t in r['evaluation']['texts'] if t['path'].endswith('/EntityHoverViewport/Body'))
    assert body['fontSize']>=22 and body['fullText'].strip()
    seen=set()
    for step in range(30):
     body=next(t for t in r['evaluation']['texts'] if t['path'].endswith('/EntityHoverViewport/Body'))
     viewport=next(v['screenRect'] for v in r['evaluation']['presentationSurfaces'] if v['name']=='EntityHoverViewport')
     seen.update(g['sourceIndex'] for g in body['glyphs'] if inside(g['screenRect'],viewport,.1))
     expected_glyphs={i for i,ch in enumerate(body['fullText']) if not ch.isspace()}
     if expected_glyphs.issubset(seen):break
     c.rpc('scroll',path=b['path'],expectedVersion=c.observation['version'],scrollY=-3)
     r=c.snapshot(label+'_full_rule_down_'+str(step))
    else:raise AssertionError('Full choice rule not reachable through actual hover scroll')
    dismiss_hover(c)
   c.click(control);dismiss_hover(c);c.snapshot(label+'_returned');assert state(c.observation)['audit.costNativeState']==native
   rows.append(dict(kind=label,choices=expected,all_selected_by_actual_pointer=True,text_geometry=geometry))
  # Recreate the same native fixture, then consume a real ordinary guard.
  timing=Collector((a.out/'feedback').resolve(),a.live.resolve());timing.out.mkdir()
  timing.prepare(fixture);timing.click('Btn_Next');timing.click('Btn_UnitActor_1');timing.click('Btn_UnitAbility_1');started=time.monotonic()
  native=state(timing.observation)['audit.costNativeState'];stages=[];feedback=''
  for target,label,visible in [(0,'feedback_immediate',True),(3.2,'feedback_hold',True),(4.8,'feedback_expired',False)]:
   remaining=target-(time.monotonic()-started)
   if remaining>0:time.sleep(remaining)
   r=timing.snapshot(label);texts=[t for t in r['evaluation']['texts'] if t['name']=='BattleToast'];backs=[x for x in r['evaluation']['presentationSurfaces'] if x['name']=='BattleToastSoftBacking']
   assert bool(texts)==visible and bool(backs)==visible,(label,texts,backs)
   if visible:
    assert texts[0]['effectiveRendererAlpha']>=.99 and backs[0]['effectiveRendererAlpha']>=.99
    assert texts[0]['fontSize']>=22 and not texts[0]['isTextOverflowing']
    feedback=texts[0]['fullText']
   assert state(timing.observation)['audit.costNativeState']==native
   stages.append(dict(label=label,wall_seconds_after_consumer=time.monotonic()-started,visible=visible))
  timing.click('Btn_CombatLog');r=timing.snapshot('feedback_recalled_in_journal');body=next(t for t in r['evaluation']['texts'] if t['path'].endswith('/ReadingViewport/FullText'))
  assert feedback in body['fullText'],feedback
  timing.click('Btn_ReturnCombat');assert state(timing.observation)['audit.costNativeState']==native
  save(a.out/'manifest.json',dict(passed=True,choices=rows,captures=c.captures,feedback_captures=timing.captures,feedback_stages=stages,journal_retains_feedback=True,
   scope='One formal three-enemy fixture, every tray selection and an actual guard consumer with visible/expired receipt and journal recall. Not all hero skills, full rule scroll, art identity, human or performance acceptance.'))
  print('PASS compact choices and real feedback lifecycle',flush=True)
 except Exception as e:save(a.out/'failure.json',dict(error=repr(e),captures=c.captures));raise

if __name__=='__main__':main()
