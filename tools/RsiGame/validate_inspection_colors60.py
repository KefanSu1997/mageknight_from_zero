"""A formally reset task with distinct legal crystal counts, not synthetic UI data."""
import json,copy
from pathlib import Path
from capture_evaluation_cycle import Collector,save
from capture_embedded_asset_cases import dismiss_hover
from validate_entity_hover_reader import inside
from validate_four_unit_battle_layout import FIGHT
from structured_ui_ppo import state
ROOT=Path(__file__).resolve().parents[2];BASE=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'
def main():
    out=BASE/'attempt60/color_binding';c=Collector(out.resolve(),(BASE/'attempt60/live_final').resolve())
    task=json.loads((BASE/'attempt56/four_unit_task.json').read_text(encoding='utf8'));task['Build']['Crystals']=dict(Red=3,Blue=2,Green=1,White=0);save(out/'task.json',task)
    c.observation=c.demo.reset_task(task);c.observation=c.demo.prepare_prefix([FIGHT]);dismiss_hover(c);native=state(c.observation)['audit.costNativeState'];n=json.loads(native)
    b=next(b for b in c.observation['controls'] if b['name']=='Btn_HeroActor');c.rpc('hover_info',path=b['path'],expectedVersion=c.observation['version']);r=c.snapshot('distinct_crystals')
    texts=[t for t in r['evaluation']['texts'] if '/EntityHoverViewport/InspectionContent/' in t['path']];vp=next(x['screenRect'] for x in r['evaluation']['presentationSurfaces'] if x['name']=='EntityHoverViewport');checks=[]
    for color,short in [('Red','红'),('Blue','蓝'),('Green','绿'),('White','白')]:
        t=next(t for t in texts if '/Crystals_'+color+'/' in t['path'] and t['name']=='ResourceValue');label=next(t for t in texts if '/Crystals_'+color+'/' in t['path'] and t['name']=='ResourceColor');assert t['fullText']==str(n['crystals'].get(color,0)) and label['fullText']==short
        assert all(inside(g['screenRect'],vp,.1) for g in t['glyphs']+label['glyphs']);checks.append(dict(color=color,label=short,actual=t['fullText']))
    c.rpc('unhover_info',path=b['path'],expectedVersion=c.observation['version']);dismiss_hover(c);assert state(c.observation)['audit.costNativeState']==native
    save(out/'manifest.json',dict(passed=True,checks=checks,native_unchanged=True,scope='Formal task with valid distinct crystals3/2/1/0, actual4units/3enemies; checks color/value pairing and first-screen glyphs. Not normal original opening, policy frequency or paid consumption.'))
    print('Distinct native crystals3/2/1/0 color binding passed')
if __name__=='__main__':main()
