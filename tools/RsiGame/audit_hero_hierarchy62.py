"""Read actual Unity geometry and original alpha; never alter image files."""
import json,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'
OUT=BASE/'attempt62'
def read(p):return json.loads(p.read_text(encoding='utf8'))
def silhouettes(record):
 rows=[]
 for im in record['evaluation']['rawImages']:
  if not im['path'].endswith(('/PartyFigure','/EnemyIllustration')):continue
  hero='Btn_HeroActor/' in im['path'];enemy=im['name']=='EnemyIllustration'
  cat='HeroCharacters' if hero else 'EnemyCharacters' if enemy else 'UnitCharacters'
  src=next(ROOT/'Assets/Resources/UI'/cat/v/(im['texture']+'.png') for v in ['embedded_v5','embedded_v4','embedded_v3','embedded_v2','embedded_v1'] if (ROOT/'Assets/Resources/UI'/cat/v/(im['texture']+'.png')).exists())
  a=Image.open(src).getchannel('A')
  if im['uvRect']['width']<0:a=a.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
  box=a.getbbox();r=im['screenRect'];sx,sy=r['width']/a.width,r['height']/a.height
  bounds=dict(left=r['x']+box[0]*sx,top=r['y']+box[1]*sy,right=r['x']+box[2]*sx,bottom=r['y']+box[3]*sy)
  # Original translucent pixels count too; exact source projection precedes
  # raster rounding. A tiny floating point epsilon is not a crop allowance.
  assert bounds['left']>=-.01 and bounds['right']<=1920.01 and bounds['top']>=-.01 and bounds['bottom']<=1080.01,(im['texture'],bounds)
  a=np.asarray(a.resize((round(r['width']),round(r['height'])),Image.Resampling.BILINEAR))
  m=np.zeros((1080,1920),np.uint8);x,y=round(r['x']),round(r['y']);l,t,rr,bb=max(0,x),max(0,y),min(1920,x+a.shape[1]),min(1080,y+a.shape[0]);m[t:bb,l:rr]=a[t-y:bb-y,l-x:rr-x]
  rows.append(dict(path=im['path'],texture=im['texture'],hero=hero,enemy=enemy,source_sha256=hashlib.sha256(src.read_bytes()).hexdigest(),bounds=bounds,mask=m))
 return rows
def pair(before,after,label):
 a=silhouettes(read(before));b=silhouettes(read(after));old={x['path']:x for x in a};new={x['path']:x for x in b};assert old.keys()==new.keys()
 collisions=[]
 for i,x in enumerate(b):
  for y in b[i+1:]:
   previous=int(((old[x['path']]['mask']>=64)&(old[y['path']]['mask']>=64)).sum());current=int(((x['mask']>=64)&(y['mask']>=64)).sum())
   assert current<=previous,(label,x['texture'],y['texture'],previous,current)
   collisions.append(dict(pair=[x['texture'],y['texture']],before_alpha64_pixels=previous,after_alpha64_pixels=current))
 hero=next(x for x in b if x['hero']);units=[x for x in b if not x['hero'] and not x['enemy']]
 ratios=[]
 for u in units:
  hb=hero['bounds'];ub=u['bounds'];ratios.append((hb['bottom']-hb['top'])/(ub['bottom']-ub['top']))
 for x in b:x.pop('mask')
 return dict(label=label,figures=b,no_new_alpha64_overlap=collisions,hero_to_unit_full_alpha_height_ratios=ratios)
def main():
 rows=[]
 for p in sorted((OUT/'after_verified/key_phases').glob('*/capture.json')):
  rows.append(pair(BASE/'attempt61/before/key_phases'/p.parent.name/'capture.json',p,p.parent.name))
 for label in ['four_units_neutral','four_units_three_enemies']:
  rows.append(pair(BASE/'attempt60/four_unit_validation/key_phases'/label/'capture.json',OUT/'four_unit_validation/key_phases'/label/'capture.json',label))
 assert len(rows)==9
 (OUT/'geometry_audit.json').write_text(json.dumps(dict(passed=True,rows=rows,scope='Nine actual paired scenes. Original alpha>0 full silhouette stays on canvas; no increase in projected alpha>=64 body overlap. Resampled raster audit is not animation, exact renderer compositing, all106 identities or human acceptance.'),ensure_ascii=False,indent=2),encoding='utf8')
 print('Nine actual scenes: full-alpha canvas bounds and no additional opaque silhouette overlaps')
if __name__=='__main__':main()
