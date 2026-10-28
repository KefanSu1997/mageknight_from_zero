"""Actual skills/party shortcut hover, open and return; preserve formal state."""
import argparse,json
from pathlib import Path
from capture_evaluation_cycle import Collector,save
from capture_embedded_asset_cases import dismiss_hover
from structured_ui_ppo import state
from validate_entity_hover_reader import inside,intersect

def main():
 p=argparse.ArgumentParser()
 for n in ['out','live','fixture','before']:p.add_argument('--'+n,type=Path,required=True)
 a=p.parse_args();a.out.mkdir(parents=True,exist_ok=False);c=Collector(a.out.resolve(),a.live.resolve());rows=[]
 try:
  c.prepare(json.loads(a.fixture.read_text(encoding='utf8'))['fixture']);dismiss_hover(c);neutral=c.snapshot('party_shortcuts_neutral')
  old=json.loads(a.before.read_text(encoding='utf8'));prior=state(c.observation)
  for name in ['Btn_Skills','Btn_Roster']:
   b=next(b for b in c.observation['controls'] if b['name']==name);previous=next(b for b in old['observation']['controls'] if b['name']==name)
   for key in ['x','y','width','height','text','interactable','clickable']:assert b[key]==previous[key],(name,key)
   text=next(t for t in neutral['evaluation']['texts'] if t['path']==b['path']+'/Label');ot=next(t for t in old['evaluation']['texts'] if t['path']==text['path'])
   assert text['fontSize']==ot['fontSize'] and text['fullText']==ot['fullText'] and not text['isTextOverflowing']
   expected={i for i,ch in enumerate(text['fullText']) if not ch.isspace()};assert expected.issubset({g['sourceIndex'] for g in text['glyphs']})
   c.rpc('hover',path=b['path'],expectedVersion=c.observation['version']);r=c.snapshot(name+'_hover')
   body=next(t for t in r['evaluation']['texts'] if t['name']=='Body' and '/BattleHoverInfo/' in t['path']);panel=next(t['screenRect'] for t in r['evaluation']['presentationSurfaces'] if t['name']=='BattleHoverInfo')
   assert body['fontSize']>=21 and not body['isTextOverflowing']
   assert {i for i,ch in enumerate(body['fullText']) if not ch.isspace()}.issubset({g['sourceIndex'] for g in body['glyphs']})
   assert all(inside(g['screenRect'],panel,.1) for g in body['glyphs'])
   c.rpc('unhover',path=b['path'],expectedVersion=c.observation['version']);c.click(name);dismiss_hover(c);opened=c.snapshot(name+'_open')
   assert state(c.observation)['audit.costNativeState']==prior['audit.costNativeState']
   current=next(b for b in c.observation['controls'] if b['name']==name);assert current['text']=='返回手牌'
   header=next(t for t in opened['evaluation']['texts'] if t['name']==('SkillLabel' if name=='Btn_Skills' else 'UnitLabel'))
   assert header['fontSize']>=22 and not header['isTextOverflowing']
   assert {i for i,ch in enumerate(header['fullText']) if not ch.isspace()}.issubset({g['sourceIndex'] for g in header['glyphs']})
   for control in opened['observation']['controls']:
    if control['name'] not in ['Btn_Skills','Btn_Roster']:continue
    rect=dict(x=control['x']-control['width']/2,y=control['y']-control['height']/2,width=control['width'],height=control['height'])
    assert not any(intersect(g['screenRect'],rect) for g in header['glyphs']),(name,'header overlaps shortcut')
   c.click(name);dismiss_hover(c);c.snapshot(name+'_returned')
   assert state(c.observation)['audit.costNativeState']==prior['audit.costNativeState']
   assert next(b for b in c.observation['controls'] if b['name']==name)['text']==previous['text']
   rows.append(dict(name=name,same_hit_caption_font_enabledness=True,hover_full_generated_text=True,actual_open_and_return=True,native_state_unchanged=True))
  save(a.out/'manifest.json',dict(passed=True,rows=rows,captures=c.captures,scope='One formal fixture, skills/roster shortcut actual hover/open/return; not all hero skills or abilities consumed.'))
  print('PASS skills and roster actual hover/open/return')
 except Exception as e:save(a.out/'failure.json',dict(error=repr(e),captures=c.captures));raise
if __name__=='__main__':main()
