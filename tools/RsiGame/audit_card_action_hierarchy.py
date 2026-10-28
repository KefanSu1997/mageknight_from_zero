"""Audit actual same-focus card geometry and fixed click-center chords."""
from pathlib import Path
import json,math
from validate_entity_hover_reader import intersect
OUT=Path('AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1/attempt39')
def read(folder,label):return json.loads((OUT/folder/'key_phases'/label/'capture.json').read_text())
def main():
 rows=[]
 for folder in ['baseline_card','card_validation','card_validation_final']:
  events=json.loads((OUT/folder/'pointer_events.json').read_text())['events'];ps=[e['point_unity_bottom_left'] for e in events];rows.append(dict(version=folder,click_names=[e['name'] for e in events],clicks=len(events),sum_click_center_chords_px=sum(math.dist(a,b) for a,b in zip(ps,ps[1:])),points=ps))
 assert len({tuple(x['click_names']) for x in rows})==1
 change=100*(rows[-1]['sum_click_center_chords_px']/rows[0]['sum_click_center_chords_px']-1)
 (OUT/'fixed_click_center_cost_final.json').write_text(json.dumps(dict(passed=True,rows=rows,change_percent=change,scope='Fixed five-click select-focus/strong/base/blue/play sequence. Actual dispatched centers, straight chords excluding first position/all hover detours. Not shortest sequence, physical cursor path, human timing or overall weighted cost.'),indent=2),encoding='utf8')
 a,b=read('card_validation_final','focus_base'),read('card_validation_final','focus_strong');stable=[]
 for name in ['Btn_Choice_base','Btn_Choice_strong']:
  x=next(v for v in a['observation']['controls'] if v['name']==name);y=next(v for v in b['observation']['controls'] if v['name']==name);geometry=lambda c:{k:c[k] for k in ['x','y','width','height']}
  assert geometry(x)==geometry(y),(name,geometry(x),geometry(y));stable.append(dict(name=name,geometry=geometry(x),stable=True))
 consumed=read('card_validation_final','focus_blue_consumed');result=next(t for t in consumed['evaluation']['texts'] if '/ResolutionReceipt/' in t['path']);assert result['fontSize']>=18 and not result['isTextOverflowing']
 for t in consumed['evaluation']['texts']:
  if '/Enemy_' in t['path'] and t['name'] in ['EnemyIntent','Name','Numbers','State']:assert not intersect(result['screenRect'],t['screenRect'])
 images=[]
 for label in ['focus_base','focus_strong','focus_base_hover_hero','focus_strong_hover_hero','focus_blue_consumed']:
  old,new=read('baseline_card',label),read('card_validation_final',label);raw=lambda r:{im['path']:{k:im[k] for k in ['texture','uvRect','screenRect']} for im in r['evaluation']['rawImages']}
  assert raw(old)==raw(new),label;images.append(dict(label=label,raw_images_unchanged=True))
 (OUT/'card_geometry_final.json').write_text(json.dumps(dict(passed=True,stable_controls=stable,raw_images=images,receipt_result=result['screenRect'],receipt_result_avoids_enemy_text_rects=True,scope='Two focus base/strong mode controls exactly stable. Confirmation/sideways differ by3.15px vertically because one context uses normal flow and the other fits the reserved band; not claimed exactly stable. Five pairs preserve all raw texture/UV/geometry. Result text vs threat rectangles checked in immediate post-consumer; no whole-catalog or human proof.'),indent=2),encoding='utf8')
 print('Final click-center chords change percent:',change, 'rows:',[(r['version'],r['sum_click_center_chords_px']) for r in rows])
if __name__=='__main__':main()
