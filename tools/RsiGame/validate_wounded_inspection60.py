"""Actual ready-but-wounded unit inspection, not a fabricated UI model."""
import json
from pathlib import Path
from capture_evaluation_cycle import Collector,save
from capture_embedded_asset_cases import dismiss_hover
from validate_entity_hover_reader import inside
from structured_ui_ppo import state
ROOT=Path(__file__).resolve().parents[2];BASE=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'
def main():
    out=BASE/'attempt60/wounded_validation_verified';c=Collector(out.resolve(),(BASE/'attempt60/live_final').resolve())
    fixture=json.loads((BASE/'attempt55/before/setups/monster_monster_060.json').read_text(encoding='utf8'))['fixture'];c.prepare(fixture)
    n=json.loads(state(c.observation)['audit.costNativeState']);i=next(i for i,u in enumerate(n['units']) if u['wounds']>0);assert n['units'][i]['ready'];c.click('Btn_UnitActor_'+str(i));dismiss_hover(c)
    native=state(c.observation)['audit.costNativeState'];b=next(b for b in c.observation['controls'] if b['name']=='Btn_UnitActor_'+str(i));c.rpc('hover_info',path=b['path'],expectedVersion=c.observation['version']);r=c.snapshot('wounded_top');seen={};catalog={}
    for step in range(30):
        texts=[t for t in r['evaluation']['texts'] if '/EntityHoverViewport/InspectionContent/' in t['path']];vp=next(s['screenRect'] for s in r['evaluation']['presentationSurfaces'] if s['name']=='EntityHoverViewport')
        if step==0:
            assert next(t['fullText'] for t in texts if '/UnitWounds/' in t['path'] and t['name']=='FactValue')==str(n['units'][i]['wounds'])
            assert '就绪' in next(t['fullText'] for t in texts if '/UnitState/' in t['path'] and t['name']=='SectionBody')
            assert '不能激活' in next(t['fullText'] for t in texts if '/UnitState/' in t['path'] and t['name']=='SectionBody')
        for t in texts:
            catalog[t['path']]=t
            seen.setdefault(t['path'],set()).update(g['sourceIndex'] for g in t['glyphs'] if inside(g['screenRect'],vp,.1))
        progress=next(t['fullText'] for t in r['evaluation']['texts'] if t['name']=='EntityReadingProgress')
        if '100%' in progress or progress=='信息已显示':break
        c.rpc('scroll_info',path=b['path'],expectedVersion=c.observation['version'],scrollY=-3);r=c.snapshot('wounded_wheel_'+str(step))
    else:raise AssertionError('Wounded reader final line unreachable')
    for t in catalog.values():assert {j for j,ch in enumerate(t['fullText']) if not ch.isspace()}.issubset(seen[t['path']])
    c.rpc('unhover_info',path=b['path'],expectedVersion=c.observation['version']);dismiss_hover(c);c.snapshot('wounded_returned');assert state(c.observation)['audit.costNativeState']==native
    save(out/'manifest.json',dict(passed=True,index=i,ready=True,wounds=n['units'][i]['wounds'],wheel_events=step,all_glyphs_seen=True,native_unchanged=True,scope='One actual archived wounded ready unit; state and fullrules preserved, not all wounded/special-rule combinations.'))
    print('Wounded structured state and fullrule return passed')
if __name__=='__main__':main()
