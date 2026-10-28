"""Same-fixture focus card base/strong and native blue-token consumer evidence."""
import argparse,json
from pathlib import Path
from capture_evaluation_cycle import Collector,save
from capture_embedded_asset_cases import dismiss_hover
from structured_ui_ppo import state,ROOT
from validate_entity_hover_reader import inside

def main():
 p=argparse.ArgumentParser()
 for n in ['out','live','fixture']:p.add_argument('--'+n,type=Path,required=True)
 p.add_argument('--candidate',action='store_true');p.add_argument('--before',type=Path);a=p.parse_args();a.out.mkdir(parents=True,exist_ok=False);c=Collector(a.out.resolve(),a.live.resolve())
 try:
  c.prepare(json.loads(a.fixture.read_text(encoding='utf8'))['fixture']);c.click('Btn_Card_basic_card_006_0');dismiss_hover(c);base=c.snapshot('focus_base');source=json.loads((ROOT/'resources/text_json/basic_card.json').read_text(encoding='utf8'));printed=next(v for v in source if v['id']=='basic_card_006')
  before=state(c.observation);body=[]
  for label in ['base','strong']:
   if label=='strong':c.click('Btn_Choice_strong');dismiss_hover(c);c.snapshot('focus_strong')
   if a.candidate:
    current=c.snapshot('focus_'+label+'_geometry')
    effect=next(t for t in current['evaluation']['texts'] if t['name']=='CurrentCardEffect')
    assert effect['fontSize']>=22 and not effect['isTextOverflowing']
    assert all(inside(g['screenRect'],effect['screenRect'],2) for g in effect['glyphs'])
    hint=next(t for t in current['evaluation']['texts'] if t['name']=='CardEffectHint')
    assert hint['fontSize']>=21 and hint['hasVisibleGlyphs']
    rule_before=state(c.observation)
    c.rpc('hover_info',path=effect['path'],expectedVersion=c.observation['version']);rule=c.snapshot('focus_'+label+'_full_rule')
    full=next(t for t in rule['evaluation']['texts'] if t['path'].endswith('/EntityHoverViewport/Body'))
    assert printed['enhanced_effect' if label=='strong' else 'base_effect'] in full['fullText']
    assert full['fontSize']>=22 and not full['isTextOverflowing']
    seen=set()
    for index in range(30):
     viewport=next(v['screenRect'] for v in rule['evaluation']['presentationSurfaces'] if v['name']=='EntityHoverViewport')
     full=next(t for t in rule['evaluation']['texts'] if t['path'].endswith('/EntityHoverViewport/Body'))
     seen.update(g['sourceIndex'] for g in full['glyphs'] if inside(g['screenRect'],viewport,.1))
     expected={i for i,ch in enumerate(full['fullText']) if not ch.isspace()}
     if expected.issubset(seen):break
     c.rpc('scroll_info',path=effect['path'],expectedVersion=c.observation['version'],scrollY=-3);rule=c.snapshot('focus_'+label+'_full_rule_down_'+str(index))
    else:raise AssertionError('Full printed rule not consumed through hover viewport')
    assert state(c.observation)['audit.costNativeState']==rule_before['audit.costNativeState']
    assert state(c.observation)['audit.costFixture']==rule_before['audit.costFixture']
    c.rpc('unhover_info',path=effect['path'],expectedVersion=c.observation['version']);c.snapshot('focus_'+label+'_rule_returned')
    if a.before:
     old=json.loads((a.before/'key_phases'/('focus_'+label)/'capture.json').read_text(encoding='utf8'))
     sig=lambda r:{b['name']:('横置使用' if b['name']=='Btn_Sideways' and not b['interactable'] else b['text'],b['interactable'],b['clickable']) for b in r['observation']['controls'] if '/Inspector/' in b['path']}
     assert sig(current)==sig(old),(sig(current),sig(old))
     assert state(current['observation'])['audit.costNativeState']==state(old['observation'])['audit.costNativeState']
   original=state(c.observation);actor=next(b for b in c.observation['controls'] if b['name']=='Btn_HeroActor')
   c.rpc('hover',path=actor['path'],expectedVersion=c.observation['version']);r=c.snapshot('focus_'+label+'_hover_hero');assert state(c.observation)['audit.costNativeState']==original['audit.costNativeState'];assert state(c.observation)['audit.costFixture']==original['audit.costFixture']
   comparison=next(t for t in r['evaluation']['texts'] if t['name']=='SelectedActionComparison');body.append(dict(mode=label,text=comparison['fullText'],font_size=comparison['fontSize']))
   if a.candidate:
    assert not comparison['isTextOverflowing']
    panel=next(v['screenRect'] for v in r['evaluation']['presentationSurfaces'] if v['name']=='BattleHoverInfo')
    assert all(inside(g['screenRect'],panel,.1) for g in comparison['glyphs'])
    assert '绿' in comparison['fullText'] and '数值+3' in comparison['fullText'] if label=='strong' else '蓝色魔力' in comparison['fullText']
   c.rpc('unhover',path=actor['path'],expectedVersion=c.observation['version']);c.snapshot('focus_'+label+'_returned')
  c.click('Btn_Choice_base');c.click('Btn_Choice_blue');dismiss_hover(c);c.snapshot('focus_blue_before_consume');prior=json.loads(state(c.observation)['audit.costNativeState']);c.click('Btn_Play');dismiss_hover(c);c.snapshot('focus_blue_consumed');after=json.loads(state(c.observation)['audit.costNativeState'])
  assert after['tokens'].get('Blue',0)-prior['tokens'].get('Blue',0)==1,(prior['tokens'],after['tokens'])
  assert after['hand'].count('basic_card_006')==prior['hand'].count('basic_card_006')-1,(prior['hand'],after['hand'])
  if a.candidate:
   if a.before:assert after==json.loads((a.before/'manifest.json').read_text(encoding='utf8'))['after_native']
   assert all(not x['text'].startswith('已选有 ') for x in body),body
   assert all(x['font_size']>=22 for x in body)
  save(a.out/'manifest.json',dict(passed=True,candidate=a.candidate,hover_comparison=body,formal_blue_token_gain=1,before_native=prior,after_native=after,captures=c.captures,scope='One actual focus card base/strong on-demand original22pt printed rule glyph consumption and hover selection retention; formal base-blue token/card consumption. Not all card modes or enhanced consumer/fee acceptance.'))
  print('PASS focus layout and actual base-blue consumer',flush=True)
 except Exception as e:save(a.out/'failure.json',dict(error=repr(e),captures=c.captures));raise
if __name__=='__main__':main()
