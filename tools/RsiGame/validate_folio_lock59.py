"""Real typed combat command, restriction and full-rule reader evidence."""
import json
from pathlib import Path
from capture_evaluation_cycle import Collector,save
from capture_embedded_asset_cases import dismiss_hover
from validate_entity_hover_reader import inside
from structured_ui_ppo import state
ROOT=Path(__file__).resolve().parents[2];BASE=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'
def main():
    out=BASE/'attempt59/validation_lock_fix';c=Collector(out.resolve(),(BASE/'attempt59/live_lock_fix').resolve())
    c.prepare(json.loads((BASE/'attempt56/after_final/setups/three_enemies.json').read_text(encoding='utf8'))['fixture'])
    rows=[]
    for label in ['unit_unavailable_in_ranged','unit_selected_in_block','unit_consumed']:
        if label=='unit_selected_in_block':c.click('Btn_Next')
        c.click('Btn_UnitActor_1')
        if label=='unit_consumed':c.click('Btn_UnitAbility_1')
        dismiss_hover(c);r=c.snapshot(label);native=state(c.observation)['audit.costNativeState']
        old=json.loads((BASE/'attempt57/context_validation_review_fix/key_phases'/label/'capture.json').read_text(encoding='utf8'))
        assert state(old['observation'])['audit.costNativeState']==native
        texts=r['evaluation']['texts'];reason=next((t['fullText'] for t in texts if t['name']=='RestrictionLabel'),'')
        assert reason=={'unit_unavailable_in_ranged':'本阶段无能力','unit_selected_in_block':'','unit_consumed':'已耗竭'}[label],reason
        values=[t for t in texts if t['name']=='Value' and 'Fact/' in t['path']]
        assert next(t['fullText'] for t in values if '/UnitCommandFact/' in t['path'])==('可选1' if label=='unit_selected_in_block' else '可选0')
        if label!='unit_unavailable_in_ranged':
            control=next(b for b in c.observation['controls'] if b['name']=='Btn_UnitAbility_1')
            caption=next(t for t in texts if t['path']==control['path']+'/Label')
            assert caption['fullText']==('发动 · ' if label=='unit_selected_in_block' else '不可发动 · ')+'格挡 4\n无魔力费 · 迅捷×2',caption['fullText']
            assert caption['fontSize']>=22
            hit=dict(x=control['x']-control['width']/2,y=1080-control['y']-control['height']/2,width=control['width'],height=control['height'])
            assert all(inside(g['screenRect'],hit,.2) for g in caption['glyphs'])
            expected={i for i,ch in enumerate(caption['fullText']) if not ch.isspace()};assert expected.issubset({g['sourceIndex'] for g in caption['glyphs']})
            assert control['interactable']==(label=='unit_selected_in_block')
            c.rpc('hover_info',path=control['path'],expectedVersion=c.observation['version']);q=c.snapshot(label+'_rule_top');seen=set()
            for step in range(20):
                body=next(t for t in q['evaluation']['texts'] if t['path'].endswith('/EntityHoverViewport/Body'))
                vp=next(t['screenRect'] for t in q['evaluation']['presentationSurfaces'] if t['name']=='EntityHoverViewport')
                assert body['componentColor']['r']<.3 and body['componentColor']['g']<.3
                seen|={g['sourceIndex'] for g in body['glyphs'] if inside(g['screenRect'],vp,.1)}
                if body['screenRect']['y']+body['screenRect']['height']<=vp['y']+vp['height']+.2:break
                c.rpc('scroll_info',path=control['path'],expectedVersion=c.observation['version'],scrollY=-3);q=c.snapshot(label+'_rule_wheel_'+str(step))
            else:raise AssertionError('Unreachable full rule')
            oldcaption=next(b['text'] for b in old['observation']['controls'] if b['name']=='Btn_UnitAbility_1')
            assert oldcaption in body['fullText']
            assert {i for i,ch in enumerate(body['fullText']) if not ch.isspace()}.issubset(seen)
            assert state(c.observation)['audit.costNativeState']==native
            c.rpc('unhover_info',path=control['path'],expectedVersion=c.observation['version']);dismiss_hover(c)
        if label=='unit_consumed':
            c.click('Btn_SelectedDetails');full=c.snapshot('unit_consumed_selected_details')
            wiki=next(t for t in full['evaluation']['texts'] if t['name']=='FullText')
            assert '激活消耗原印刷费用，随后耗竭。' in wiki['fullText']
            assert '战斗中的能力按钮仅显示当前阶段适用项；全文另列全部原能力。' in wiki['fullText']
            assert '原能力与费用' in wiki['fullText']
            c.click('Btn_ReturnCombat');dismiss_hover(c);assert state(c.observation)['audit.costNativeState']==native
        rows.append(dict(scene=label,restriction=reason,native_matches57=True))
    # Separate real wounded state; stage reason is explicit rather than inferring wound causality.
    fixture=json.loads((BASE/'attempt55/before/setups/monster_monster_060.json').read_text(encoding='utf8'))['fixture'];c.prepare(fixture)
    units=json.loads(state(c.observation)['audit.costNativeState'])['units'];i=next(i for i,u in enumerate(units) if u['wounds']>0);c.click('Btn_UnitActor_'+str(i));dismiss_hover(c);c.snapshot('selected_wounded')
    save(out/'manifest.json',dict(passed=True,rows=rows,checks=['3same_native_states','trueguardblock4consumer','finalenabledcount0_1_0','semanticcaption22ptallglyphs','exactnativefullcaptioninreader','allreadercharactersseenbyactualwheel','actualwoundedfixture'],scope='One actual guard with block4/swiftx2 and no mana fee; no claim for every unit, paid/complex ability or human usability.'))
    print('59action affordance and folio fullrules passed')
if __name__=='__main__':main()
