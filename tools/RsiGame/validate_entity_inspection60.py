"""Actual structured inspection; native fields and retained canonical rules."""
import json
from pathlib import Path
from capture_evaluation_cycle import Collector,save
from capture_embedded_asset_cases import dismiss_hover
from validate_entity_hover_reader import inside
from structured_ui_ppo import state
ROOT=Path(__file__).resolve().parents[2];BASE=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'
def read(p):return json.loads(p.read_text(encoding='utf8'))
def main():
    out=BASE/'attempt60/validation_verified';c=Collector(out.resolve(),(BASE/'attempt60/live_final').resolve())
    c.prepare(read(BASE/'attempt56/after_final/setups/three_enemies.json')['fixture']);c.click('Btn_Next');c.click('Btn_UnitActor_1');dismiss_hover(c)
    old_rows={r['actor']:r for r in read(BASE/'attempt59/entity_readers_final/manifest.json')['rows']};rows=[]
    for label,name in [('hero','Btn_HeroActor'),('unit','Btn_UnitActor_0'),('enemy','Btn_Target')]:
        native=state(c.observation)['audit.costNativeState'];n=json.loads(native);control=next(b for b in c.observation['controls'] if b['name']==name)
        old=read(BASE/'attempt59/entity_readers_final/key_phases'/(label+'_top')/'capture.json');assert state(old['observation'])['audit.costNativeState']==native
        c.rpc('hover_info',path=control['path'],expectedVersion=c.observation['version']);r=c.snapshot(label+'_top');seen={};catalog={}
        top_texts=[t for t in r['evaluation']['texts'] if '/EntityHoverViewport/InspectionContent/' in t['path']]
        joined='\n'.join(t['fullText'] for t in top_texts)
        vp=next(t['screenRect'] for t in r['evaluation']['presentationSurfaces'] if t['name']=='EntityHoverViewport')
        if label=='hero':
            values={t['path'].split('/')[-2]:t['fullText'] for t in top_texts if t['name']=='FactValue'}
            assert values==dict(HeroArmor=state(c.observation)['hero.armor'],HeroFame=str(n['fame']),HeroWounds=str(n['wounds']))
            resource_values={t['path'].split('/')[-2]:t for t in top_texts if t['name']=='ResourceValue'}
            assert len(resource_values)==10
            for color in ['Red','Blue','Green','White','Gold','Black']:
                key='Mana_'+color;expected=str(n['tokens'].get(color,0));assert resource_values[key]['fullText']==expected
            for color in ['Red','Blue','Green','White']:assert resource_values['Crystals_'+color]['fullText']==str(n['crystals'].get(color,0))
            assert all(inside(g['screenRect'],vp,.1) for t in resource_values.values() for g in t['glyphs']),'Hero resource first-screen clipping'
        elif label=='unit':
            values={t['path'].split('/')[-2]:t['fullText'] for t in top_texts if t['name']=='FactValue'}
            assert values==dict(UnitLevel=str(n['units'][0]['level']),UnitArmor=state(c.observation)['units.armor'].split(',')[0],UnitWounds=str(n['units'][0]['wounds']))
        for step in range(35):
            current=[t for t in r['evaluation']['texts'] if '/EntityHoverViewport/InspectionContent/' in t['path']]
            vp=next(t['screenRect'] for t in r['evaluation']['presentationSurfaces'] if t['name']=='EntityHoverViewport')
            panel=next(t['screenRect'] for t in r['evaluation']['presentationSurfaces'] if t['name']=='BattleHoverInfo')
            assert vp['height']>=280 and inside(panel,dict(x=0,y=0,width=1920,height=1080))
            assert not any(t['name'] in ['SelectedActionComparison','HoverContext'] for t in r['evaluation']['texts'] if '/BattleHoverInfo/' in t['path'])
            footer=next(t for t in r['evaluation']['texts'] if t['name']=='InspectionReturnContext');assert '当前操作' in footer['fullText'] and '乌顿守卫' in footer['fullText']
            for t in r['evaluation']['texts']:
                if '/BattleHoverInfo/' in t['path'] and '/EntityHoverViewport/' not in t['path']:
                    assert all(inside(g['screenRect'],panel,.1) for g in t['glyphs']),(label,t['name'])
            for t in current:
                assert t['fontSize']>=21 and not t['isTextOverflowing']
                catalog[t['path']]=t
                seen.setdefault(t['path'],set()).update(g['sourceIndex'] for g in t['glyphs'] if inside(g['screenRect'],vp,.1))
            progress=next(t['fullText'] for t in r['evaluation']['texts'] if t['name']=='EntityReadingProgress')
            if progress=='信息已显示' or '100%' in progress:break
            c.rpc('scroll_info',path=control['path'],expectedVersion=c.observation['version'],scrollY=-3);r=c.snapshot(label+'_wheel_'+str(step))
        else:raise AssertionError(('No final inspection frame',label))
        joined='\n'.join(t['fullText'] for t in catalog.values())
        for line in old_rows[label]['body'].split('\n'):
            if not line.strip() or label=='hero':continue
            if label=='unit' and (line.startswith('等级 ') or line in ['点击查看并选择本部队的原能力。','原能力（印刷费用）：','当前可用项与实际费用以选中后的动作区为准。']):continue
            if label=='enemy' and line=='点击该实体选择正式目标。':continue
            assert line in joined,(label,line)
        for t in catalog.values():
            required={i for i,ch in enumerate(t['fullText']) if not ch.isspace()};assert required.issubset(seen[t['path']]),(label,t['path'],sorted(required-seen[t['path']]))
        assert state(c.observation)['audit.costNativeState']==native
        rows.append(dict(actor=label,visible_texts=[dict(path=t['path'],text=t['fullText']) for t in catalog.values()],viewport=vp,wheel_events=step,all_glyphs_seen=True,native_unchanged=True,canonical_rule_lines_retained=True))
        c.rpc('unhover_info',path=control['path'],expectedVersion=c.observation['version']);dismiss_hover(c);c.snapshot(label+'_returned');assert state(c.observation)['audit.costNativeState']==native
    before=state(c.observation)['audit.costNativeState'];c.click('Btn_CombatSymbols');top=c.snapshot('guide_top')
    names=[t for t in top['evaluation']['texts'] if '/SymbolGuideContent/SymbolDefinition_' in t['path'] and t['name']=='Name'];assert len(names)==9 and any(t['fullText']=='名望' for t in names)
    b=next(t for t in top['evaluation']['texts'] if t['name']=='FullText');text=b['fullText'];assert '星章' in text;mask=dict(x=480,y=200,width=1260,height=682)
    visible={g['sourceIndex'] for g in b['glyphs'] if inside(g['screenRect'],mask,.1)}
    c.click('Btn_ReadEnd');end=c.snapshot('guide_end');b=next(t for t in end['evaluation']['texts'] if t['name']=='FullText');visible|={g['sourceIndex'] for g in b['glyphs'] if inside(g['screenRect'],mask,.1)}
    assert {i for i,ch in enumerate(text) if not ch.isspace()}.issubset(visible)
    c.click('Btn_ReturnCombat');dismiss_hover(c);c.snapshot('guide_returned');assert state(c.observation)['audit.costNativeState']==before
    before=state(c.observation);c.click('Btn_UnitAbility_1');dismiss_hover(c);c.snapshot('actual_guard_block4');after=state(c.observation);assert int(after['block'])-int(before['block'])==4 and not json.loads(after['audit.costNativeState'])['units'][1]['ready']
    save(out/'manifest.json',dict(passed=True,rows=rows,checks=['nativefieldvalues','hero10resourcevaluesfirstscreen','alloldcanonicalrulelinesretained','noborrowedentitystatsinfooter','fullglyphsactualwheel','guide9symbolsfullbody','same_native_return','realblock4consumer'],scope='3actual archived entities,10hero resource values, all old canonical rules retained across structured fields, actual wheel and native-preserving return. Not allidentities/infinity/largenumbers/human/globalUI acceptance.'))
    print('60structuredinspect native/rule/glyph/return/consumer passed')
if __name__=='__main__':main()
