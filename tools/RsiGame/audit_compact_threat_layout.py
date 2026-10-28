"""Compare actual44/45 captures, retaining all information and fixed actor/hit geometry."""
import argparse,json,re,hashlib
from pathlib import Path

def read(p):return json.loads(p.read_text(encoding='utf8'))
def inside(a,b,slack=3):return a['x']>=b['x']-slack and a['y']>=b['y']-slack and a['x']+a['width']<=b['x']+b['width']+slack and a['y']+a['height']<=b['y']+b['height']+slack

def main():
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--catalog',action='store_true');a=p.parse_args();root=a.root.resolve()
 rows=[];groups=[('validation','validation_final'),('four_unit_validation','four_unit_validation'),('card_validation','card_validation_final')]
 if a.catalog:groups.append(('asset_cases','asset_cases'))
 for old_group,new_group in groups:
  for current in sorted((root/'attempt45'/new_group/'key_phases').glob('*/capture.json')):
   prior=root/'attempt44'/old_group/'key_phases'/current.parent.name/'capture.json'
   if not prior.exists():continue
   b,c=read(prior),read(current);state=lambda r:{x['key']:x['value'] for x in r['observation']['state']}
   assert state(b)['audit.costNativeState']==state(c)['audit.costNativeState'],current
   keys=['name','text','interactable','clickable','x','y','width','height']
   controls=lambda r:{x['path']:{k:x[k] for k in keys} for x in r['observation']['controls']}
   assert controls(b)==controls(c),current
   old_images={i['path']:i for i in b['evaluation']['rawImages']};new_images={i['path']:i for i in c['evaluation']['rawImages']}
   assert old_images.keys()==new_images.keys(),current
   for path,im in new_images.items():
    for k in ['texture','sourceWidth','sourceHeight','screenRect','uvRect','componentColor','effectiveRendererAlpha','shader']:
     assert im[k]==old_images[path][k],(current,path,k)
   old_text={t['path']:t for t in b['evaluation']['texts']};threats=[];restored=[]
   for t in c['evaluation']['texts']:
    if not re.search(r'/Enemy_\d+/(Name|Numbers|State|EnemyIntent)$',t['path']):continue
    old=old_text[t['path']]
    assert t['fontSize']>=old['fontSize'],(current,t['path'])
    assert not t['isTextOverflowing'],(current,t['path'])
    assert ''.join(g['character'] for g in t['glyphs'])==''.join(ch for ch in t['fullText'] if not ch.isspace()),(current,t['path'])
    for g in t['glyphs']:
     assert inside(g['screenRect'],t['screenRect']),(current,t['path'],'glyph outside text')
     assert inside(g['screenRect'],dict(x=0,y=0,width=1920,height=1080),0),(current,t['path'],'glyph outside canvas')
    if t['name']=='Numbers' and t['fullText']!=old['fullText']:
     restored.append(dict(path=t['path'],before=old['fullText'],after=t['fullText'],requires_review=True))
    else:assert t['fullText']==old['fullText'],(current,t['path'])
    if t['name']=='EnemyIntent':threats.append(dict(path=t['path'],text=t['fullText'],font_size=t['fontSize'],before_width=old['screenRect']['width'],after_width=t['screenRect']['width']))
   rows.append(dict(capture=str(current),same_native_and_controls=True,unchanged_images=True,threats=threats,canonical_readout_differences=restored))
 expected=152 if a.catalog else 46
 assert len(rows)==expected,(len(rows),expected)
 inventory=read(root/'attempt42/catalog_alpha_inventory.json');project=Path(__file__).resolve().parents[2]
 for item in inventory['records']:assert hashlib.sha256((project/item['path']).read_bytes()).hexdigest()==item['sha256'],item['id']
 result=dict(passed=True,matched_captures=len(rows),source_png_unchanged=len(inventory['records']),records=rows,
 scope='Actual equal native state, formal control/hit geometry and RawImages; complete enemy glyphs/font/canvas. Canonical text differences enumerated for review, not automatically treated as improvements. No all-consumer, printed identity, aesthetic, dynamic timing or human acceptance claim.')
 out=root/('attempt45/compact_layout_catalog_audit.json' if a.catalog else 'attempt45/compact_layout_audit.json')
 with out.open('x',encoding='utf8') as f:json.dump(result,f,ensure_ascii=False,indent=2)
 print('PASS',len(rows),'matched captures;',sum(len(r['canonical_readout_differences']) for r in rows),'canonical text differences')

if __name__=='__main__':main()
