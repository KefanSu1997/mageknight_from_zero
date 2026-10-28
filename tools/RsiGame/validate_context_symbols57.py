"""Real context-fact readers, complete symbol guide, preserved native commands."""
import json
from pathlib import Path
from capture_evaluation_cycle import Collector,save
from capture_embedded_asset_cases import dismiss_hover
from validate_entity_hover_reader import inside
from structured_ui_ppo import state
ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'
OUT=BASE/'attempt57/context_validation_review_fix'
def read(p):return json.loads(p.read_text(encoding='utf8'))
def main():
    c=Collector(OUT.resolve(),(BASE/'attempt57/live_review_fix').resolve())
    fixture=read(BASE/'attempt56/after_final/setups/three_enemies.json')['fixture'];c.prepare(fixture)
    rows=[]
    for label in ['unit_unavailable_in_ranged','unit_selected_in_block','unit_consumed']:
        if label=='unit_selected_in_block':c.click('Btn_Next')
        c.click('Btn_UnitActor_1')
        if label=='unit_consumed':c.click('Btn_UnitAbility_1')
        dismiss_hover(c);record=c.snapshot(label);native=state(c.observation)['audit.costNativeState']
        old=read(BASE/'attempt56/after_final/key_phases'/label/'capture.json')
        assert state(old['observation'])['audit.costNativeState']==native
        old_status=next(t['fullText'] for t in old['evaluation']['texts'] if t['name']=='UnitAvailability')
        assert not any(t['name']=='UnitAvailability' for t in record['evaluation']['texts'])
        facts=[s for s in record['evaluation']['presentationSurfaces'] if s['name']=='Glyph' and '/Unit' in s['path']]
        assert len(facts)==5,len(facts)
        values=[t for t in record['evaluation']['texts'] if t['name']=='Value' and 'Fact/' in t['path']]
        assert len(values)==5
        for t in values:
            assert t['fontSize']>=21 and t['hasVisibleGlyphs']
            assert {i for i,ch in enumerate(t['fullText']) if not ch.isspace()}.issubset({g['sourceIndex'] for g in t['glyphs']})
        expected=[str(json.loads(native)['units'][1]['level']),state(c.observation)['units.armor'].split(',')[1],str(json.loads(native)['units'][1]['wounds'])]
        for name,value in zip(['UnitRankFact','UnitArmorFact','UnitWoundFact'],expected):
            assert next(t['fullText'] for t in values if '/'+name+'/' in t['path'])==value
        commands=[b for b in c.observation['controls'] if b['name'].startswith('Btn_UnitAbility_') or b['name'] in ('Btn_AssignUnit','Btn_Fortitude','Btn_FearBanner','Btn_UseFear','Btn_Courage','Btn_HealWound')]
        count=sum(bool(b['interactable']) for b in commands)
        assert next(t['fullText'] for t in values if '/UnitCommandFact/' in t['path'])=='可选'+str(count)
        for i,fact in enumerate(facts):
            path=fact['path'].rsplit('/',1)[0]
            c.rpc('hover_info',path=path,expectedVersion=c.observation['version']);r=c.snapshot(label+'_fact_'+str(i))
            body=next(t for t in r['evaluation']['texts'] if t['path'].endswith('/EntityHoverViewport/Body'))
            assert old_status in body['fullText'] and body['fontSize']>=22
            assert state(c.observation)['audit.costNativeState']==native
            rows.append(dict(state=label,path=path,font_size=body['fontSize'],old_exact_status_retained=True,body=body['fullText']))
            c.rpc('unhover_info',path=path,expectedVersion=c.observation['version']);dismiss_hover(c)
    # Guide is one explicit Wiki topic, not a new battle page or a state mutation.
    before=state(c.observation)['audit.costNativeState'];c.click('Btn_CombatSymbols');top=c.snapshot('symbol_guide_top')
    body=next(t for t in top['evaluation']['texts'] if t['name']=='FullText')
    text=body['fullText'];assert all(s in text for s in ['闪电','沙漏','血滴','不保证','开锁','闭锁'])
    viewport=dict(x=480,y=200,width=1260,height=682)
    seen={g['sourceIndex'] for g in body['glyphs'] if inside(g['screenRect'],viewport,.1)}
    c.click('Btn_ReadEnd');end=c.snapshot('symbol_guide_end')
    body=next(t for t in end['evaluation']['texts'] if t['name']=='FullText')
    assert body['fullText']==text and body['fontSize']>=26
    seen|={g['sourceIndex'] for g in body['glyphs'] if inside(g['screenRect'],viewport,.1)}
    required={i for i,ch in enumerate(text) if not ch.isspace()};assert required.issubset(seen),len(required-seen)
    c.click('Btn_ReturnCombat');dismiss_hover(c);c.snapshot('guide_return_preserved')
    assert state(c.observation)['audit.costNativeState']==before
    save(OUT/'manifest.json',dict(passed=True,readers=rows,guide_all_nonwhite_chars=len(required),same_native_reading=True,
        scope='15actual context-reader hover entrances across unavailable/ready/exhausted; exact old status text retained and22pt body; full26pt guide top/end glyph union. Scripted input, not human learning or wholeUIacceptance.'))
    print('15context facts and complete symbol guide verified')
if __name__=='__main__':main()
