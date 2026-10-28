"""Actual native utility entry hit targets, captions, hover and open/return."""
import argparse,json
from pathlib import Path
from capture_evaluation_cycle import Collector,save
from capture_embedded_asset_cases import dismiss_hover
from structured_ui_ppo import state,ROOT
from PIL import Image
from validate_entity_hover_reader import intersect,inside

def main():
 p=argparse.ArgumentParser()
 for name in ['out','live','fixture','before']:p.add_argument('--'+name,type=Path,required=True)
 p.add_argument('--require-no-occlusion',action='store_true')
 a=p.parse_args();a.out.mkdir(parents=True,exist_ok=False);c=Collector(a.out.resolve(),a.live.resolve())
 try:
  c.prepare(json.loads(a.fixture.read_text(encoding='utf8'))['fixture']);dismiss_hover(c);r=c.snapshot('utilities_neutral')
  old=json.loads(a.before.read_text(encoding='utf8'));before={b['name']:b for b in old['observation']['controls']}
  prior=state(c.observation);results=[]
  names=['Btn_QuickMana','Btn_QuickSource','Btn_GlobalWiki','Btn_CombatLog','Btn_ResetPhase','Btn_Menu']
  for name in names:
   b=next((b for b in c.observation['controls'] if b['name']==name),None)
   if b is None:continue
   assert name in before,name
   for key in ['x','y','width','height','text','interactable','clickable']:assert b[key]==before[name][key],(name,key,b[key],before[name][key])
   caption=next(t for t in r['evaluation']['texts'] if t['path'].startswith(b['path']+'/') and t['name']=='Label')
   orig=next(t for t in old['evaluation']['texts'] if t['path']==caption['path'])
   assert caption['fullText']==orig['fullText'] and caption['fontSize']==orig['fontSize']
   assert caption['hasVisibleGlyphs'] and not caption['isTextOverflowing'],name
   for g in caption['glyphs']:
    q=g['screenRect'];assert q['x']>=0 and q['y']>=0 and q['x']+q['width']<=1920 and q['y']+q['height']<=1080,(name,q)
   if b['clickable']:
    c.rpc('hover',path=b['path'],expectedVersion=c.observation['version']);hover=c.snapshot(name+'_hover')
    assert state(c.observation)['audit.costNativeState']==prior['audit.costNativeState']
    assert any(t['name']=='Heading' and t['fullText'] for t in hover['evaluation']['texts']),name
    if a.require_no_occlusion:
     panel=next(v['screenRect'] for v in hover['evaluation']['presentationSurfaces'] if v['name']=='BattleHoverInfo')
     body=next(t for t in hover['evaluation']['texts'] if t['name']=='Body' and '/BattleHoverInfo/' in t['path'])
     assert body['fontSize']>=21 and not body['isTextOverflowing']
     assert {i for i,ch in enumerate(body['fullText']) if not ch.isspace()}.issubset({g['sourceIndex'] for g in body['glyphs']})
     assert all(inside(g['screenRect'],panel,.1) for g in body['glyphs'])
     for im in hover['evaluation']['rawImages']:
      if im['name'] not in ['PartyFigure','EnemyIllustration']:continue
      cat='EnemyCharacters' if im['name']=='EnemyIllustration' else 'HeroCharacters' if 'HeroActor' in im['path'] else 'UnitCharacters'
      path=next(p for v in ['embedded_v2','embedded_v1'] if (p:=ROOT/'Assets/Resources/UI'/cat/v/(im['texture']+'.png')).exists())
      with Image.open(path) as image:box=image.getchannel('A').getbbox()
      q=im['screenRect'];sx=q['width']/im['sourceWidth'];sy=q['height']/im['sourceHeight']
      alpha=dict(x=q['x']+box[0]*sx,y=q['y']+box[1]*sy,width=(box[2]-box[0])*sx,height=(box[3]-box[1])*sy)
      assert not intersect(panel,alpha),(name,im['path'],panel,alpha)
    c.rpc('unhover',path=b['path'],expectedVersion=c.observation['version'])
   results.append(dict(name=name,caption=caption['fullText'],font_size=caption['fontSize'],same_hit_target_and_enabledness=True,hover_fulltext_and_no_alpha_bbox_occlusion=a.require_no_occlusion))
  for name,close,label in [('Btn_GlobalWiki','Btn_ReturnCombat','wiki'),('Btn_CombatLog','Btn_ReturnCombat','log'),('Btn_Menu','Btn_CloseBattleMenu','menu')]:
   c.click(name);dismiss_hover(c);c.snapshot(label+'_open');assert state(c.observation)['audit.costNativeState']==prior['audit.costNativeState']
   c.click(close);dismiss_hover(c);c.snapshot(label+'_returned');assert state(c.observation)['audit.costNativeState']==prior['audit.costNativeState']
  save(a.out/'manifest.json',dict(passed=True,results=results,actual_open_return=['wiki','log','menu'],captures=c.captures,scope='One actual three-enemy fixture; same outer hit rectangles/captions/fonts and enabledness as prior. Actual hover and native-state-preserving open/return for Wiki/log/menu. Source formal take/use is independently validated by the full phase suite; reset handler not claimed consumed here.'))
  print('PASS utility entries',len(results))
 except Exception as e:save(a.out/'failure.json',dict(error=repr(e),captures=c.captures));raise
if __name__=='__main__':main()
