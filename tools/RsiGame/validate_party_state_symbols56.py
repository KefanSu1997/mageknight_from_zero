"""Real hover entry points and native wounded/readiness values for party symbols."""
import json
from pathlib import Path
from capture_evaluation_cycle import Collector,save
from capture_embedded_asset_cases import dismiss_hover
from structured_ui_ppo import state

ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'
OUT=BASE/'attempt56/symbol_validation_verified'
def main():
    c=Collector(OUT.resolve(),(BASE/'attempt56/live_final').resolve())
    fixture=json.loads((BASE/'attempt55/before/setups/monster_monster_060.json').read_text(encoding='utf8'))['fixture']
    c.prepare(fixture);dismiss_hover(c);neutral=c.snapshot('wounded_party_neutral')
    native=state(c.observation)['audit.costNativeState'];units=json.loads(native)['units']
    assert any(u['wounds']>0 for u in units)
    badges=[s for s in neutral['evaluation']['presentationSurfaces'] if s['name'] in ['ReadinessSymbol','WoundSymbol','HeroLeadershipCrest']]
    assert len(badges)==2*len(units)+1,len(badges)
    assert not any(t['name']=='Condition' for t in neutral['evaluation']['texts'])
    counts={int(t['path'].split('Btn_UnitActor_')[1].split('/')[0]):t for t in neutral['evaluation']['texts'] if t['name']=='WoundCount'}
    for i,u in enumerate(units):
        text=counts[i]
        assert text['fullText']==str(u['wounds']) and text['fontSize']>=23 and text['hasVisibleGlyphs']
        assert {j for j,ch in enumerate(text['fullText']) if not ch.isspace()}.issubset({g['sourceIndex'] for g in text['glyphs']})
    rows=[]
    for i,symbol in enumerate(badges):
        c.rpc('hover_info',path=symbol['path'],expectedVersion=c.observation['version'])
        record=c.snapshot('symbol_'+str(i))
        assert state(c.observation)['audit.costNativeState']==native
        body=next(t for t in record['evaluation']['texts'] if t['path'].endswith('/EntityHoverViewport/Body'))
        assert body['hasVisibleGlyphs'] and body['fontSize']>=21
        if symbol['name']=='WoundSymbol':assert '创伤' in body['fullText']
        if symbol['name']=='ReadinessSymbol':assert '就绪' in body['fullText'] or '耗竭' in body['fullText']
        if symbol['name']=='HeroLeadershipCrest':assert '英雄' in body['fullText']
        rows.append(dict(path=symbol['path'],body=body['fullText'],font_size=body['fontSize'],same_native=True))
        c.rpc('unhover_info',path=symbol['path'],expectedVersion=c.observation['version']);dismiss_hover(c)
    save(OUT/'manifest.json',dict(passed=True,symbols=len(rows),rows=rows,native_units=units,
        scope='Actual per-symbol pointer-enter readers, native wounds/readiness and retained23pt counts in one archived wounded fixture. No human recognition or full-text-scroll coverage claim; separate real activation evidence covers exhaustion.'))
    print('All state symbols hovered; native wounded counts matched')
if __name__=='__main__':main()
