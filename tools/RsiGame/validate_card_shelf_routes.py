"""Additional actual base-color consumers and legal sideways, per-route pointer traces."""
import argparse,json
from pathlib import Path
from capture_evaluation_cycle import Collector,save
from capture_embedded_asset_cases import dismiss_hover
from structured_ui_ppo import state

def main():
 p=argparse.ArgumentParser()
 for n in ['out','live','fixture']:p.add_argument('--'+n,type=Path,required=True)
 a=p.parse_args();a.out.mkdir(parents=True,exist_ok=False);c=Collector(a.out.resolve(),a.live.resolve());fixture=json.loads(a.fixture.read_text(encoding='utf8'))['fixture'];rows=[]
 try:
  for color,choice in [('default_blue',None),('blue','blue'),('white','white'),('red','red'),('green','green_crystal')]:
   c=Collector((a.out/color).resolve(),a.live.resolve());c.prepare(fixture);start=len(c.events);c.click('Btn_Card_basic_card_006_0',role=color);dismiss_hover(c)
   if choice:c.click('Btn_Choice_'+choice,role=color);dismiss_hover(c)
   c.snapshot(color+'_before');before=json.loads(state(c.observation)['audit.costNativeState']);c.click('Btn_Play',role=color);dismiss_hover(c);c.snapshot(color+'_consumed');after=json.loads(state(c.observation)['audit.costNativeState'])
   collection='crystals' if color=='green' else 'tokens';key={'default_blue':'Blue','blue':'Blue','white':'White','red':'Red','green':'Green'}[color]
   assert after[collection].get(key,0)-before[collection].get(key,0)==1,(color,before[collection],after[collection])
   assert after['hand'].count('basic_card_006')==before['hand'].count('basic_card_006')-1
   rows.append(dict(route=color,events=c.events[start:],before=before,after=after,captures=c.captures))
  c=Collector((a.out/'rage_sideways').resolve(),a.live.resolve());c.prepare(fixture);c.click('Btn_Next',role='sideways_phase_entry');start=len(c.events);c.click('Btn_Card_basic_card_021_0',role='rage_sideways');dismiss_hover(c);c.snapshot('rage_sideways_before');before=json.loads(state(c.observation)['audit.costNativeState']);c.click('Btn_Sideways',role='rage_sideways');dismiss_hover(c);c.snapshot('rage_sideways_consumed');after=json.loads(state(c.observation)['audit.costNativeState']);assert after['block']-before['block']==1;assert after['hand'].count('basic_card_021')==before['hand'].count('basic_card_021')-1;rows.append(dict(route='rage_sideways',events=c.events[start:],before=before,after=after,captures=c.captures))
  save(a.out/'manifest.json',dict(passed=True,routes=rows,captures=c.captures,scope='One formal fixture, five focus base color routes (default and explicitBlue/White/Red/GreenCrystal), one different-card legal Block sideways. Actual resource/card consumers; not all cards, enhanced fees, human paths or historical weighted cost.'))
  print('PASS six actual routes',flush=True)
 except Exception as e:save(a.out/'failure.json',dict(error=repr(e),captures=c.captures));raise
if __name__=='__main__':main()
