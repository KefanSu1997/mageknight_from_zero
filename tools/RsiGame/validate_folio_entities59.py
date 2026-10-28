"""Actual common actor folio readers; no visual or human acceptance claim."""
import json
from pathlib import Path
from capture_evaluation_cycle import Collector,save
from capture_embedded_asset_cases import dismiss_hover
from validate_entity_hover_reader import inside
from structured_ui_ppo import state
ROOT=Path(__file__).resolve().parents[2];BASE=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'
def main():
    out=BASE/'attempt59/entity_readers_final';c=Collector(out.resolve(),(BASE/'attempt59/live_review').resolve())
    c.prepare(json.loads((BASE/'attempt56/after_final/setups/three_enemies.json').read_text(encoding='utf8'))['fixture'])
    c.click('Btn_Next');c.click('Btn_UnitActor_1');dismiss_hover(c)
    rows=[]
    for label,name in [('hero','Btn_HeroActor'),('unit','Btn_UnitActor_0'),('enemy','Btn_Target')]:
        native=state(c.observation)['audit.costNativeState'];control=next(b for b in c.observation['controls'] if b['name']==name)
        c.rpc('hover_info',path=control['path'],expectedVersion=c.observation['version']);r=c.snapshot(label+'_top');seen=set()
        for step in range(30):
            body=next(t for t in r['evaluation']['texts'] if t['path'].endswith('/EntityHoverViewport/Body'))
            vp=next(t['screenRect'] for t in r['evaluation']['presentationSurfaces'] if t['name']=='EntityHoverViewport')
            assert vp['height']>=160 and body['fontSize']>=21 and body['componentColor']['r']<.3
            panel=next(t['screenRect'] for t in r['evaluation']['presentationSurfaces'] if t['name']=='BattleHoverInfo');assert inside(panel,dict(x=0,y=0,width=1920,height=1080))
            for t in r['evaluation']['texts']:
                if '/BattleHoverInfo/' in t['path'] and t['name'] in ['Heading','FolioEyebrow','HoverContext','SelectedActionComparison','EntityReadingProgress']:
                    assert {i for i,ch in enumerate(t['fullText']) if not ch.isspace()}.issubset({g['sourceIndex'] for g in t['glyphs']})
                    assert all(inside(g['screenRect'],panel,.1) for g in t['glyphs']),(label,t['name'])
            seen|={g['sourceIndex'] for g in body['glyphs'] if inside(g['screenRect'],vp,.1)}
            if body['screenRect']['y']+body['screenRect']['height']<=vp['y']+vp['height']+.2:break
            c.rpc('scroll_info',path=control['path'],expectedVersion=c.observation['version'],scrollY=-3);r=c.snapshot(label+'_wheel_'+str(step))
        else:raise AssertionError(('Unreachable common reader',label))
        required={i for i,ch in enumerate(body['fullText']) if not ch.isspace()};assert required.issubset(seen)
        assert state(c.observation)['audit.costNativeState']==native
        rows.append(dict(actor=label,body=body['fullText'],all_chars_seen=len(required),wheel_events=step,viewport=vp))
        c.rpc('unhover_info',path=control['path'],expectedVersion=c.observation['version']);dismiss_hover(c);c.snapshot(label+'_returned');assert state(c.observation)['audit.costNativeState']==native
    save(out/'manifest.json',dict(passed=True,rows=rows,scope='3actual hero/unit/enemy common readers, all body glyphs visible through actual wheel; static header/footer fit within panel, native state unchanged. Not all identities or human usability.'))
    print('3common folio readers and exact return state passed')
if __name__=='__main__':main()
