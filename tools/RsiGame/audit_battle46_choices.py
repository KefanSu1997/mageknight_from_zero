"""Audit unchanged battlefield/native controls separately from redesigned optional trays."""
import json,hashlib,re,argparse
from pathlib import Path
from validate_entity_hover_reader import inside

def read(p):return json.loads(p.read_text(encoding='utf8'))
def native(r):return {v['key']:v['value'] for v in r['observation']['state']}['audit.costNativeState']

def main():
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);a=p.parse_args();root=a.root.resolve();out=root/'attempt46';rows=[]
 for group,oldgroup in [('validation_final','validation_final'),('four_unit_validation_final','four_unit_validation'),('card_validation_final','card_validation_final')]:
  for current in sorted((out/group/'key_phases').glob('*/capture.json')):
   prior=root/'attempt45'/oldgroup/'key_phases'/current.parent.name/'capture.json'
   if not prior.exists():continue
   b,c=read(prior),read(current);assert native(b)==native(c),current
   keys=['name','text','interactable','clickable','x','y','width','height'];controls=lambda r:{v['path']:{k:v[k] for k in keys} for v in r['observation']['controls']}
   assert controls(b)==controls(c),current
   images=lambda r:{v['path']:v for v in r['evaluation']['rawImages']}
   bi,ci=images(b),images(c);assert bi.keys()==ci.keys(),current
   for path,im in ci.items():
    for key in ['texture','sourceWidth','sourceHeight','uvRect','screenRect','shader','componentColor','effectiveRendererAlpha']:
     assert im[key]==bi[path][key],(current,path,key)
   rows.append(dict(capture=str(current),same_native_controls_and_images=True))
 assert len(rows)==46,len(rows)
 tray=[]
 for current in sorted((out/'choice_validation_final_compatibility/key_phases').glob('*/capture.json')):
  r=read(current)
  for t in r['evaluation']['texts']:
   if t['name'] not in ['ChoiceIdentity','ChoiceUsage','ChoicePrintedRule']:continue
   assert t['fontSize']>=(22 if t['name']=='ChoicePrintedRule' else 26),(current,t['path'])
   assert not t['isTextOverflowing'],(current,t['path'])
   expected=''.join(ch for ch in t['fullText'] if not ch.isspace());actual=''.join(g['character'] for g in t['glyphs']);assert actual==expected,(current,t['path'])
   assert all(inside(g['screenRect'],t['screenRect'],3) for g in t['glyphs']),(current,t['path'])
   tray.append(dict(capture=str(current),path=t['path'],font_size=t['fontSize'],text=t['fullText']))
 inventory=read(root/'attempt42/catalog_alpha_inventory.json');project=Path(__file__).resolve().parents[2]
 for item in inventory['records']:assert hashlib.sha256((project/item['path']).read_bytes()).hexdigest()==item['sha256'],item['id']
 result=dict(passed=True,matched_closed_captures=len(rows),rows=rows,optional_tray_texts=tray,unchanged_png_files=len(inventory['records']),scope='Actual46 closed-phase same native state/hit geometry/images plus optional tray26pt identity/status and22pt full printed rule geometry. Whole106 actual material scenes remain45 evidence, not re-labelled46. Not artistic acceptance, hidden text reachability, all skill consumers or human performance.')
 with (out/'geometry_audit.json').open('x',encoding='utf8') as f:json.dump(result,f,ensure_ascii=False,indent=2)
 print('PASS',len(rows),'matched closed states and',len(tray),'tray text blocks; unchanged106 PNGs')

if __name__=='__main__':main()
