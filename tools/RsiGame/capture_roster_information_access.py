"""Archive actual full printed unit abilities, scroll coverage and selection-preserving return."""
import argparse,json
from pathlib import Path
from capture_evaluation_cycle import Collector,save
from capture_embedded_asset_cases import dismiss_hover
from validate_entity_hover_reader import inside
from structured_ui_ppo import state

def main():
 p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);p.add_argument('--live',type=Path,required=True);p.add_argument('--fixture',type=Path,required=True);a=p.parse_args();a.out.mkdir();c=Collector(a.out.resolve(),a.live.resolve());rows=[]
 try:
  c.prepare(json.loads(a.fixture.read_text(encoding='utf8'))['fixture']);dismiss_hover(c);c.click('Btn_Roster');dismiss_hover(c);c.snapshot('roster_open')
  native=state(c.observation)['audit.costNativeState'];buttons=[x['name'] for x in c.observation['controls'] if x['name'].startswith('Btn_Unit_') and x['clickable']]
  for name in buttons:
   c.click(name);dismiss_hover(c);c.snapshot(name+'_selected');selection=state(c.observation)['audit.costFixture'];b=next(x for x in c.observation['controls'] if x['name']==name)
   c.rpc('hover',path=b['path'],expectedVersion=c.observation['version']);r=c.snapshot(name+'_rule_top');seen=set();steps=[]
   for i in range(35):
    body=next(t for t in r['evaluation']['texts'] if t['path'].endswith('/EntityHoverViewport/Body'));vp=next(x['screenRect'] for x in r['evaluation']['presentationSurfaces'] if x['name']=='EntityHoverViewport')
    assert body['fontSize']>=22 and '原能力（印刷费用）' in body['fullText']
    seen.update(g['sourceIndex'] for g in body['glyphs'] if inside(g['screenRect'],vp,.1));expected={j for j,ch in enumerate(body['fullText']) if not ch.isspace()}
    steps.append(dict(index=i,visible_glyphs=len([g for g in body['glyphs'] if inside(g['screenRect'],vp,.1)])))
    if expected.issubset(seen):break
    c.rpc('scroll',path=b['path'],expectedVersion=c.observation['version'],scrollY=-3);r=c.snapshot(name+'_rule_down_'+str(i))
   else:raise AssertionError('Unit full rule remains unreachable')
   assert state(c.observation)['audit.costNativeState']==native and state(c.observation)['audit.costFixture']==selection
   c.rpc('unhover',path=b['path'],expectedVersion=c.observation['version']);c.snapshot(name+'_returned');assert state(c.observation)['audit.costFixture']==selection
   rows.append(dict(control=name,full_text=body['fullText'],font_size=body['fontSize'],all_nonspace_glyphs_seen=True,steps=steps))
  save(a.out/'manifest.json',dict(passed=True,results=rows,captures=c.captures,native_unchanged=True,selection_preserved=True,scope='Two current roster entries, actual22pt full-rule scroll and return. Evidence for on-demand preservation, not always-visible equivalence or all30unit rules, human discoverability or whole game.'))
  print('PASS actual roster full-rule reachability',len(rows),flush=True)
 except Exception as e:save(a.out/'failure.json',dict(error=repr(e),captures=c.captures));raise

if __name__=='__main__':main()
