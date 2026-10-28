"""Audit runtime grading without equating shader metadata to artistic acceptance."""
import argparse,json,hashlib
from pathlib import Path

def read(p):return json.loads(p.read_text(encoding='utf8'))
def main():
 p=argparse.ArgumentParser()
 for n in ['before','after','source-inventory','out']:p.add_argument('--'+n,type=Path,required=True)
 p.add_argument('--capture-suffix',default='')
 a=p.parse_args();rows=[];root=Path(__file__).resolve().parents[2]
 inventory=read(a.source_inventory)
 for item in inventory['records']:
  assert hashlib.sha256((root/item['path']).read_bytes()).hexdigest()==item['sha256'],item['id']
 for group in ['validation','four_unit_validation','card_validation']:
  for current in sorted((a.after/(group+a.capture_suffix)/'key_phases').glob('*/capture.json')):
   old=a.before/group/'key_phases'/current.parent.name/'capture.json'
   if not old.exists():continue
   b,c=read(old),read(current);state=lambda r:{x['key']:x['value'] for x in r['observation']['state']}
   assert state(b)['audit.costNativeState']==state(c)['audit.costNativeState'],current
   controls=lambda r:{x['path']:{k:x[k] for k in ['name','text','interactable','clickable','x','y','width','height']} for x in r['observation']['controls']}
   assert controls(b)==controls(c),current
   before_images={x['path']:x for x in b['evaluation']['rawImages']};actors=[]
   for im in c['evaluation']['rawImages']:
    assert im['path'] in before_images,(current,im['path']);original=before_images[im['path']]
    for k in ['texture','sourceWidth','sourceHeight','screenRect','uvRect','componentColor','effectiveRendererAlpha']:
     assert im[k]==original[k],(current,im['path'],k)
    if im['name'] not in ['PartyFigure','EnemyIllustration']:continue
    assert im['shader']=='MageKnight/UI/BattleCharacterAtmosphere' and im['shaderSupported'],im
    assert abs(im['atmosphereSaturation']-.9)<1e-6
    uv=im['atmosphereBodyUV'];assert 0<=uv['x']<uv['y']<=1
    actors.append(dict(path=im['path'],texture=im['texture'],shader=im['shader'],saturation=im['atmosphereSaturation'],bodyUV=uv,top=im['atmosphereTop'],ground=im['atmosphereGround']))
   texts=[];prior={x['path']:x for x in b['evaluation']['texts']}
   for t in c['evaluation']['texts']:
    if not any(('/'+n+'/') in t['path'] for n in ['Btn_Next','Btn_EndTurn','Btn_Restart','Btn_FinishSite','Btn_SiteReward','Btn_Skills','Btn_Roster']):continue
    if t['name']!='Label':continue
    assert t['path'] in prior and t['fullText']==prior[t['path']]['fullText'] and t['fontSize']>=prior[t['path']]['fontSize']
    assert not t['isTextOverflowing'],(current,t['path'])
    expected={i for i,ch in enumerate(t['fullText']) if not ch.isspace()};assert expected.issubset({g['sourceIndex'] for g in t['glyphs']}),(current,t['path'])
    rect=t['screenRect']
    for g in t['glyphs']:
     r=g['screenRect'];assert r['x']>=rect['x']-2 and r['x']+r['width']<=rect['x']+rect['width']+2 and r['y']>=rect['y']-2 and r['y']+r['height']<=rect['y']+rect['height']+2,(current,t['path'],r,rect)
    texts.append(dict(path=t['path'],text=t['fullText'],fontSize=t['fontSize']))
   rows.append(dict(capture=str(current),same_native_controls_hit_geometry=True,all_raw_sources_uv_state_colors_and_geometry_equal=True,actors=actors,changed_control_texts=texts))
 assert len(rows)>=37 and sum(len(r['actors']) for r in rows)>0,(len(rows),'Missing expected32phase/four +5card pairs')
 a.out.write_text(json.dumps(dict(passed=True,source_files_unchanged=len(inventory['records']),rows=rows,scope='Runtime matched capture metadata and originalPNG SHA/UV/geometry/state alpha; supported shader grading verified on displayed actors only. Shader source preserves texture alpha. Not artistic acceptance, all106actual scene coverage, physically based lighting or performance proof.'),ensure_ascii=False,indent=2),encoding='utf8')
 print('PASS',len(rows),'matched states, original PNG files unchanged',len(inventory['records']))
if __name__=='__main__':main()
