"""Six actual same-fixture field/selection views and symbol hover channels."""
import json,argparse
from pathlib import Path
from capture_evaluation_cycle import Collector,save
from capture_embedded_asset_cases import dismiss_hover
from structured_ui_ppo import state
ROOT=Path(__file__).resolve().parents[2];BASE=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'
def main():
 p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);p.add_argument('--live',type=Path,required=True);p.add_argument('--symbols',action='store_true');a=p.parse_args();a.out.mkdir(parents=True);c=Collector(a.out.resolve(),a.live.resolve());rows=[]
 f=json.loads((BASE/'attempt56/after_final/setups/three_enemies.json').read_text(encoding='utf8'))['fixture']
 save(a.out/'fixture.json',f)
 for label in ['neutral','card','unit','target','block','damage']:
  c.prepare(dict(f,label=label));dismiss_hover(c)
  if label=='card':c.click(next(b['name'] for b in c.observation['controls'] if b['clickable'] and b['name'].startswith('Btn_Card_')))
  if label=='unit':c.click('Btn_Next');c.click('Btn_UnitActor_1')
  if label=='target':c.click('Btn_Target')
  if label=='block':c.click('Btn_Next')
  if label=='damage':c.click('Btn_Next');c.click('Btn_Next')
  dismiss_hover(c);r=c.snapshot(label);native=state(c.observation)['audit.costNativeState']
  rows.append(dict(label=label,native=json.loads(native)))
  print(label,'captured',flush=True)
 if a.symbols:
  c.prepare(dict(f,label='symbol_catalog'));dismiss_hover(c);r=c.snapshot('symbol_catalog');native=state(c.observation)['audit.costNativeState'];readers=[]
  surfaces=[dict(path=x['path'].rsplit('/',1)[0]) for x in r['evaluation']['presentationSurfaces'] if x['name']=='Sigil' and ('/FieldFact_' in x['path'] or '/Trait_' in x['path'])]
  assert len(surfaces)>=12,len(surfaces)
  for i,s in enumerate(surfaces):
   c.rpc('hover_info',path=s['path'],expectedVersion=c.observation['version']);h=c.snapshot('symbol_'+str(i))
   assert state(c.observation)['audit.costNativeState']==native
   assert any(t['name']=='Heading' and t['hasVisibleGlyphs'] for t in h['evaluation']['texts'])
   readers.append(dict(path=s['path'],same_native=True));c.rpc('unhover_info',path=s['path'],expectedVersion=c.observation['version']);dismiss_hover(c)
  save(a.out/'symbol_readers.json',dict(passed=True,rows=readers))
 save(a.out/'manifest.json',dict(passed=True,rows=rows,captures=c.captures,scope='Six actual fixture/selection/phase views, not all tasks or human recognition.'))
if __name__=='__main__':main()
