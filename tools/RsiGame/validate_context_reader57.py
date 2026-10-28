"""Actual hover-info wheel reading through mask; no OS/human reading claim."""
import json
from pathlib import Path
from capture_evaluation_cycle import Collector,save
from capture_embedded_asset_cases import dismiss_hover
from validate_entity_hover_reader import inside
from structured_ui_ppo import state
ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'
def main():
    out=BASE/'attempt57/reader_review_fix';c=Collector(out.resolve(),(BASE/'attempt57/live_review_fix').resolve())
    fixture=json.loads((BASE/'attempt56/after_final/setups/three_enemies.json').read_text(encoding='utf8'))['fixture'];c.prepare(fixture)
    rows=[]
    for label in ['ranged','block','consumed']:
        if label=='block':c.click('Btn_Next')
        c.click('Btn_UnitActor_1')
        if label=='consumed':c.click('Btn_UnitAbility_1')
        dismiss_hover(c);native=state(c.observation)['audit.costNativeState'];r=c.snapshot(label)
        facts=[x['path'].rsplit('/',1)[0] for x in r['evaluation']['presentationSurfaces'] if x['name']=='Glyph' and '/Unit' in x['path']]
        for i,path in enumerate(facts):
            c.rpc('hover_info',path=path,expectedVersion=c.observation['version']);r=c.snapshot(label+'_'+str(i)+'_top');seen=set()
            for step in range(24):
                body=next(t for t in r['evaluation']['texts'] if t['path'].endswith('/EntityHoverViewport/Body'))
                vp=next(t['screenRect'] for t in r['evaluation']['presentationSurfaces'] if t['name']=='EntityHoverViewport')
                seen|={g['sourceIndex'] for g in body['glyphs'] if inside(g['screenRect'],vp,.1)}
                if body['screenRect']['y']+body['screenRect']['height']<=vp['y']+vp['height']+.2:break
                c.rpc('scroll_info',path=path,expectedVersion=c.observation['version'],scrollY=-3)
                r=c.snapshot(label+'_'+str(i)+'_wheel_'+str(step))
            else:raise AssertionError(('Unreachable final line',path))
            required={n for n,ch in enumerate(body['fullText']) if not ch.isspace()}
            assert required.issubset(seen),(label,path,sorted(required-seen))
            assert state(c.observation)['audit.costNativeState']==native
            rows.append(dict(state=label,path=path,nonwhite_chars=len(required),actual_scroll_events=step,all_chars_seen_through_mask=True))
            c.rpc('unhover_info',path=path,expectedVersion=c.observation['version']);dismiss_hover(c)
    save(out/'manifest.json',dict(passed=True,readers=rows,scope='15actual Unity hover-info event readers with real ScrollRect wheel handler, all nonwhite characters visible through actual viewport across captured frames; not OS physical input or human reading.'))
    print('All15fact readers:full glyph coverage through actual wheel')
if __name__=='__main__':main()
