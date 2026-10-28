"""Same-native-state geometry, rendered numerals and explanatory-plate audit."""
import json,hashlib,argparse
from pathlib import Path
from structured_ui_ppo import state
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1/attempt63'
def read(p):return json.loads(p.read_text(encoding='utf8'))
def main():
 parser=argparse.ArgumentParser();parser.add_argument('--candidate',default='open_glyph_verified');parser.add_argument('--compile-tag',default='open_glyph');args=parser.parse_args()
 rows=[]
 forbidden={'CompactHeaderShade','ManaInventoryShade','PhaseBackdrop','ResourceReadability','HeroCaptionShade','CaptionShade','UtilityCaptionBacking','UtilityIconShade','ActionShelfShade','ActionReadability','ThreatBadge'}
 for label in ['neutral','card','unit','target','block','damage']:
  old=read(OUT/'before_verified/key_phases'/label/'capture.json');new=read(OUT/args.candidate/'key_phases'/label/'capture.json')
  assert state(old['observation'])['audit.costNativeState']==state(new['observation'])['audit.costNativeState'],label
  assert (new['observation']['screenWidth'],new['observation']['screenHeight'])==(1920,1080)
  oi={x['path']:x for x in old['evaluation']['rawImages']};ni={x['path']:x for x in new['evaluation']['rawImages']};assert oi.keys()==ni.keys()
  for k in oi:
   for field in ['texture','screenRect','uvRect','material']:assert oi[k][field]==ni[k][field],(label,k,field)
  active=[x['path'] for x in new['evaluation']['presentationSurfaces'] if x['name'] in forbidden or x['name'].endswith('SoftBacking')];assert not active,(label,active)
  figures=len(ni);checked=[]
  for t in new['evaluation']['texts']:
   if t['name'] not in ['Value','Meaning','ManaTokenAmount','ManaCrystalAmount']:continue
   assert not t['isTextOverflowing'] and t['hasVisibleGlyphs'],(label,t['path'],t['fullText'])
   chars={i for i,ch in enumerate(t['fullText']) if not ch.isspace()};assert chars.issubset({g['sourceIndex'] for g in t['glyphs']}),(label,t['path'])
   checked.append(dict(path=t['path'],text=t['fullText'],font_size=t['fontSize']))
  rows.append(dict(label=label,same_native=True,all_original_raw_art_geometry_and_uv_retained=figures,active_explanatory_plates=active,rendered_values_and_labels=checked))
 target=read(OUT/args.candidate/'key_phases/target/capture.json');values={}
 for t in target['evaluation']['texts']:
  if t['name']=='Meaning':
   v=next(v for v in target['evaluation']['texts'] if v['name']=='Value' and v['path'].rsplit('/',1)[0]==t['path'].rsplit('/',1)[0]);values[t['fullText']]=v['fullText']
 assert values=={'目标':'1','有效攻击':'0','需求':'9','差额':'9','保留贡献':'0'},values
 compile=read(OUT/('compile_'+args.compile_tag+'.json'))
 for p in compile['sources']:assert hashlib.sha256(Path(p['path']).read_bytes()).hexdigest()==p['sha256']
 (OUT/'geometry_and_information_audit.json').write_text(json.dumps(dict(passed=True,rows=rows,target_decision_values=values,runtime_mvid=compile['runtimeModuleVersionId'],scope='Six actual same-native scenes; original raw artwork and UV/size/position retained; enumerated explanatory plates absent; numerical/short-label glyphs generated. This is not semantic/human contrast acceptance, all identities or full-game certification.'),ensure_ascii=False,indent=2),encoding='utf8')
 print('Six same-native scenes, retained art, no enumerated prose plates, full numerical glyphs')
if __name__=='__main__':main()
