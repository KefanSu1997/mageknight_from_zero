"""Read all three archived printed skill rules, then consume actual siege1."""
from pathlib import Path
import json,argparse
from capture_evaluation_cycle import Collector,save
from capture_embedded_asset_cases import dismiss_hover
from validate_entity_hover_reader import inside
from structured_ui_ppo import state

ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'
OUT=BASE/'attempt50'

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--out',type=Path,default=OUT/'skill_readers');parser.add_argument('--live',type=Path,default=OUT/'live');args=parser.parse_args()
    out=args.out;c=Collector(out.resolve(),args.live.resolve())
    fixture=json.loads((BASE/'attempt34/validation_corrected/setups/three_enemies.json').read_text())['fixture']
    baseline=json.loads((OUT/'before/key_phases/skills_open/capture.json').read_text())
    old={}
    for t in baseline['evaluation']['texts']:
        if t['name']=='ChoicePrintedRule':old[t['path'].split('/Btn_Skill_')[1].split('/')[0]]=t['fullText']
    assert len(old)==3
    c.prepare(fixture);dismiss_hover(c);c.click('Btn_Skills');dismiss_hover(c)
    native=state(c.observation)['audit.costNativeState'];rows=[]
    for i in range(3):
        name='Btn_Skill_'+str(i);c.click(name);dismiss_hover(c);selected=json.loads(state(c.observation)['audit.costFixture'])['uiSkill']
        c.rpc('observe');button=next(x for x in c.observation['controls'] if x['name']==name)
        c.rpc('hover',path=button['path'],expectedVersion=c.observation['version']);r=c.snapshot('skill_'+str(i)+'_hover_top');seen=set();scrolls=0
        for step in range(30):
            body=next(t for t in r['evaluation']['texts'] if t['path'].endswith('/EntityHoverViewport/Body'))
            assert old[str(i)] in body['fullText'] and body['fontSize']>=22
            viewport=next(v['screenRect'] for v in r['evaluation']['presentationSurfaces'] if v['name']=='EntityHoverViewport')
            seen.update(g['sourceIndex'] for g in body['glyphs'] if inside(g['screenRect'],viewport,.1))
            expected={j for j,ch in enumerate(body['fullText']) if not ch.isspace()}
            assert state(c.observation)['audit.costNativeState']==native
            assert json.loads(state(c.observation)['audit.costFixture'])['uiSkill']==selected
            if i==1:
                use=next(x for x in c.observation['controls'] if x['name']=='Btn_UseSkill')
                assert not use['interactable'] and not use['clickable'],'Used round skill must not offer direct repeat activation'
                comparison=next(t for t in r['evaluation']['texts'] if t['name']=='SelectedActionComparison')
                assert '不可用：确认使用技能' in comparison['fullText'] and '可用：确认使用技能' not in comparison['fullText'].replace('不可用：确认使用技能','')
            if expected.issubset(seen):break
            c.rpc('scroll',path=button['path'],expectedVersion=c.observation['version'],scrollY=-3);scrolls+=1;r=c.snapshot('skill_'+str(i)+'_hover_down_'+str(step))
        else:raise AssertionError('Printed rule not completely reachable: '+name)
        rows.append(dict(source_id=selected,original_printed_rule=old[str(i)],full_rule_preserved=True,font_size=body['fontSize'],visible_source_indices=len(seen),required_source_indices=len(expected),actual_scroll_requests=scrolls))
        dismiss_hover(c);c.snapshot('skill_'+str(i)+'_returned');assert state(c.observation)['audit.costNativeState']==native
    c.click('Btn_Skill_0');dismiss_hover(c);before=json.loads(state(c.observation)['audit.costNativeState']);c.snapshot('siege_skill_before')
    assert json.loads(state(c.observation)['audit.costFixture'])['uiSkill']=='hero_skill_pool_005:00'
    c.click('Btn_SkillChoice_0');c.click('Btn_UseSkill');dismiss_hover(c);r=c.snapshot('siege_skill_consumed');after=json.loads(state(c.observation)['audit.costNativeState'])
    save(out/'consumer_native.json',dict(before=before,after=after,scope='Actual selected source and formal skill use; inspected resource assertions separately.'))
    assert len(after['attacks'])==len(before['attacks'])+1
    attack=after['attacks'][-1]
    assert attack['value']==attack['effectiveValue']==1 and attack['type']=='Siege' and attack['element']=='Physical'
    assert set(after['usedSkillsTurn'])==set(before['usedSkillsTurn'])|{'hero_skill_pool_005:00'}
    changed=[k for k in after if after[k]!=before[k]]
    assert set(changed)=={'attacks','usedSkillsTurn'},changed
    use=next(x for x in c.observation['controls'] if x['name']=='Btn_UseSkill')
    assert not use['interactable'] and not use['clickable']
    c.rpc('observe');button=next(x for x in c.observation['controls'] if x['name']=='Btn_Skill_0')
    c.rpc('hover',path=button['path'],expectedVersion=c.observation['version']);r=c.snapshot('siege_skill_used_full_rule_recalled')
    body=next(t for t in r['evaluation']['texts'] if t['path'].endswith('/EntityHoverViewport/Body'))
    comparison=next(t for t in r['evaluation']['texts'] if t['name']=='SelectedActionComparison')
    assert old['0'] in body['fullText'] and body['fontSize']>=22
    assert '不可用：确认使用技能' in comparison['fullText']
    assert json.loads(state(c.observation)['audit.costNativeState'])==after
    dismiss_hover(c)
    save(out/'manifest.json',dict(passed=True,readers=rows,captures=c.captures,selected_source_consumed='hero_skill_pool_005:00',before_native=before,after_native=after,
        changed_native_fields=changed,formal_siege1_and_used_source=True,used_round_and_turn_direct_buttons_disabled=True,consumed_rule_recalled=True,
        scope='Three original rules completely read through actual22pt hover/scroll with selected/native state retention; actual physicalSiege1 and source used exactly, no other native change. Attack serialized sourceSkill is empty, do not infer that field was populated. Permanent-tray frequency/geometry audited separately. Not all70 skills/human acceptance.'))
    print('50 three full skill readers and actual skill consume captured',flush=True)

if __name__=='__main__':main()
