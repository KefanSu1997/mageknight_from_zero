"""Exercise actual nonbutton full-rule scroll and return without native changes."""
import argparse,json
from pathlib import Path
from capture_evaluation_cycle import Collector,save
from capture_embedded_asset_cases import dismiss_hover
from structured_ui_ppo import state

def main():
 p=argparse.ArgumentParser()
 for n in ['out','live','fixture']:p.add_argument('--'+n,type=Path,required=True)
 a=p.parse_args();a.out.mkdir(parents=True,exist_ok=False);c=Collector(a.out.resolve(),a.live.resolve())
 c.prepare(json.loads(a.fixture.read_text(encoding='utf8'))['fixture']);c.click('Btn_Card_basic_card_006_0');c.click('Btn_Choice_strong');dismiss_hover(c);source=c.snapshot('strong_shelf')
 effect=next(t for t in source['evaluation']['texts'] if t['name']=='CurrentCardEffect');prior=state(c.observation)
 enter=c.rpc('hover_info',path=effect['path'],expectedVersion=c.observation['version']);assert enter['raycastProbes'][0]['valid'];top=c.snapshot('full_rule_top')
 down=c.rpc('scroll_info',path=effect['path'],expectedVersion=c.observation['version'],scrollY=-3);assert down['raycastProbes'][0]['valid'];end=c.snapshot('full_rule_scrolled')
 up=c.rpc('scroll_info',path=effect['path'],expectedVersion=c.observation['version'],scrollY=3);assert up['raycastProbes'][0]['valid'];returned=c.snapshot('full_rule_top_returned')
 body=lambda r:next(t for t in r['evaluation']['texts'] if t['path'].endswith('/EntityHoverViewport/Body'))
 assert body(top)['fullText']==body(end)['fullText']==body(returned)['fullText']
 assert body(top)['fontSize']==body(end)['fontSize']==22
 delta=body(top)['screenRect']['y']-body(end)['screenRect']['y'];assert delta>0.1,delta
 assert abs(body(top)['screenRect']['y']-body(returned)['screenRect']['y'])<.1
 assert state(c.observation)['audit.costNativeState']==prior['audit.costNativeState'] and state(c.observation)['audit.costFixture']==prior['audit.costFixture']
 c.rpc('unhover_info',path=effect['path'],expectedVersion=c.observation['version']);c.snapshot('shelf_returned')
 save(a.out/'manifest.json',dict(passed=True,actual_body_travel_px=delta,font_size=22,source=effect['path'],native_and_selection_unchanged=True,captures=c.captures,scope='One long strong focus-rule surface, actual EventSystem info enter/scroll down/up/exit with version/raycast guards. Not physical OS mouse, all text sets or human reading.'));print('PASS actual nonbutton rule scroll',delta)
if __name__=='__main__':main()
