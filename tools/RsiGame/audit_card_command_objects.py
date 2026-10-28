"""Compare actual command labels, hit geometry, state and artwork across versions."""
import argparse,json
from pathlib import Path

def read(p):return json.loads(p.read_text(encoding='utf8'))
def audit(before,after,out):
 rows=[]
 for label in ['focus_base','focus_strong','focus_base_hover_hero','focus_strong_hover_hero','focus_blue_consumed']:
  a,b=read(before/label/'capture.json'),read(after/label/'capture.json')
  state=lambda r:{x['key']:x['value'] for x in r['observation']['state']}
  assert state(a)['audit.costNativeState']==state(b)['audit.costNativeState']
  controls=lambda r:{x['path']:{k:x[k] for k in ['name','text','interactable','clickable','x','y','width','height']} for x in r['observation']['controls']}
  assert controls(a)==controls(b),label
  raw=lambda r:{x['path']:{k:x[k] for k in ['texture','uvRect','screenRect']} for x in r['evaluation']['rawImages']}
  assert raw(a)==raw(b),label
  texts=[];old={x['path']:x for x in a['evaluation']['texts']}
  for t in b['evaluation']['texts']:
   if '/Inspector/Actions/Btn_' not in t['path'] or t['name']!='Label':continue
   assert t['fullText']==old[t['path']]['fullText'] and t['fontSize']>=old[t['path']]['fontSize']
   assert not t['isTextOverflowing'],(label,t['path'])
   expected={i for i,ch in enumerate(t['fullText']) if not ch.isspace()}
   assert expected.issubset({g['sourceIndex'] for g in t['glyphs']}),(label,t['path'])
   for g in t['glyphs']:
    r,q=t['screenRect'],g['screenRect']
    assert q['x']>=r['x']-2 and q['x']+q['width']<=r['x']+r['width']+2,(label,t['path'],q,r)
    assert q['y']>=r['y']-2 and q['y']+q['height']<=r['y']+r['height']+2,(label,t['path'],q,r)
   texts.append(dict(path=t['path'],fontSize=t['fontSize'],text=t['fullText']))
  rows.append(dict(label=label,same_native=True,same_button_ids_captions_enabledness_and_hit_rectangles=True,same_original_raw_images=True,checked_texts=texts))
 assert any(r['checked_texts'] for r in rows),'No actual label paths found'
 out.write_text(json.dumps(dict(passed=True,rows=rows,scope='5 same-state focus captures; native state, original art and exposed button hit rectangles unchanged; generated dock label glyphs. Not all cards.'),ensure_ascii=False,indent=2),encoding='utf8')
 print('PASS5pairs actual semantics/hit rectangles/art/glyphs')
if __name__=='__main__':
 p=argparse.ArgumentParser()
 for n in ['before','after','out']:p.add_argument('--'+n,type=Path,required=True)
 a=p.parse_args();audit(a.before,a.after,a.out)
