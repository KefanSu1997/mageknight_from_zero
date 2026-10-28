"""Capture the rejected collision state, source context and actual resource readers."""
import argparse,json
from pathlib import Path
from capture_evaluation_cycle import Collector,save
from capture_embedded_asset_cases import dismiss_hover
from capture_phase_baseline import load_fixtures
from structured_ui_ppo import state
from validate_entity_hover_reader import intersect,inside
from validate_mana_inventory52 import inspect

ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1/attempt53'

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--live',type=Path,required=True);args=ap.parse_args()
    out=BASE/'collision_repair/evidence';c=Collector(out,args.live.resolve())
    fixture=json.loads((BASE/'before/setups/three_enemies.json').read_text())['fixture']
    c.prepare(fixture);dismiss_hover(c);before=state(c.observation)['audit.costNativeState']
    c.snapshot('three_enemies_neutral');c.click('Btn_QuickSource');dismiss_hover(c)
    c.snapshot('three_enemies_source_open');assert state(c.observation)['audit.costNativeState']==before
    c.click('Btn_SourceTacticDetails');dismiss_hover(c);c.snapshot('three_enemies_tactic_details')
    c.click('Btn_CloseTools');dismiss_hover(c);c.snapshot('three_enemies_source_returned')
    assert state(c.observation)['audit.costNativeState']==before
    rows=[]
    for color in ['Red','Blue','Green','White','Gold','Black']:
        path='Canvas/BattleTable/ManaInventory_'+color
        c.rpc('hover_info',path=path,expectedVersion=c.observation['version'])
        r=c.snapshot(color+'_hover');inspect(r)
        body=next(t for t in r['evaluation']['texts'] if t['path'].endswith('/BattleHoverInfo/Body'))
        assert body['fontSize']>=22 and body['hasVisibleGlyphs'] and not body['isTextOverflowing']
        needed={i for i,ch in enumerate(body['fullText']) if not ch.isspace()}
        assert needed.issubset({g['sourceIndex'] for g in body['glyphs']})
        assert state(c.observation)['audit.costNativeState']==before
        rows.append(dict(color=color,full_text=body['fullText'],font=body['fontSize']))
        c.rpc('unhover_info',path=path,expectedVersion=c.observation['version'])
    current=json.loads((BASE/'after_collision_fixed/key_phases/source_consumed/capture.json').read_text())
    old=json.loads((BASE/'after_final/key_phases/source_consumed/capture.json').read_text())
    def geometry(record):
        texts=record['evaluation']['texts']
        phase=next(t for t in texts if t['path'].endswith('/PhaseTrack/Phase'))
        receipt=next(t for t in texts if t['path'].endswith('/ResolutionReceipt/Result'))
        return phase,receipt,[t for t in texts if t['name']=='EnemyIntent']
    a,b,_=geometry(old);p,r,enemies=geometry(current)
    assert intersect(a['screenRect'],b['screenRect']),'Rejected regression must remain reproducible in old capture'
    assert not intersect(p['screenRect'],r['screenRect'])
    assert p['fullText']==a['fullText'] and p['fontSize']==a['fontSize']==26
    assert r['fullText']==b['fullText'] and r['fontSize']==b['fontSize']
    # Include the whole 33px receipt panel, rather than only text glyph bounds.
    panel=dict(x=700,y=166,width=800,height=33)
    assert not intersect(panel,p['screenRect'])
    for e in enemies:assert not intersect(panel,e['screenRect'])
    for t in [p,r]:
        assert t['hasVisibleGlyphs'] and not t['isTextOverflowing']
        required={i for i,ch in enumerate(t['fullText']) if not ch.isspace()}
        assert required.issubset({g['sourceIndex'] for g in t['glyphs']})
        assert all(inside(g['screenRect'],dict(x=0,y=0,width=1920,height=1080),.1) for g in t['glyphs'])
    assert state(current['observation'])['audit.costNativeState']==state(old['observation'])['audit.costNativeState']
    save(BASE/'collision_repair/geometry.json',dict(passed=True,old_collision=True,phase=p['screenRect'],receipt=panel,enemy_intents=[e['screenRect'] for e in enemies],same_native=True,unchanged_phase_font=26,scope='Actual source-consumed pair, complete phase/receipt glyphs and full receipt-panel nonintersection with phase and enemy intent. Not every future layout.'))
    save(out/'manifest.json',dict(passed=True,captures=c.captures,readers=rows,native_unchanged=True,scope='Actual three-enemy source drawer and full-tactic return, six complete22pt resource hovers. Static readability requires independent image review.'))
    print('53 collision gate and missing scene/hover evidence passed',flush=True)

if __name__=='__main__':main()
