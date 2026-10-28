"""Formal seven-hero14 learned readers plus legal level10/five-skill pressure."""
import json,copy,argparse
from pathlib import Path
from capture_evaluation_cycle import Collector,save
from capture_embedded_asset_cases import dismiss_hover
from validate_four_unit_battle_layout import FIGHT
from validate_entity_hover_reader import inside
from structured_ui_ppo import state

ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'
OUT=BASE/'attempt50'

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--out',type=Path,default=OUT/'inventory_validation');parser.add_argument('--live',type=Path,default=OUT/'live_summary');parser.add_argument('--pressure-only',action='store_true');args=parser.parse_args()
    live=args.live;out=args.out;rows=[]
    summaries={e['sourceId']:e for e in json.loads((ROOT/'Assets/Resources/UI/SkillDecisionSummaries.json').read_text())['entries']}
    for hero in ([] if args.pressure_only else ['Arythea','Braevalar','Goldyx','Krang','Norowas','Tovak','Wolfhawk']):
        c=Collector((out/hero).resolve(),live.resolve())
        source=OUT/'seven_hero_corrected'/hero/'formal_entry.json'
        task=json.loads(source.read_text())['task']
        if hero=='Wolfhawk':
            source=BASE/'attempt46/real_wolfhawk_task.json';task=json.loads(source.read_text())['task']
        c.demo.reset_task(task);c.demo.prepare_prefix([FIGHT]);c.demo.design(False);c.rpc('observe');dismiss_hover(c)
        native=state(c.observation)['audit.costNativeState'];n=json.loads(native);assert n['hero']==hero and len(n['skills'])==2
        save(c.out/'formal_entry.json',dict(task=task,prefix=[FIGHT],source=str(source),native=n))
        c.click('Btn_Skills');dismiss_hover(c);r=c.snapshot('skills_open');readers=[]
        for i,sid in enumerate(n['skills']):
            rule=summaries[sid]['originalRule'];summary=summaries[sid]['summary'];name='Btn_Skill_'+str(i)
            text=next(t for t in r['evaluation']['texts'] if t['path'].endswith('/'+name+'/ChoiceEffectSummary'))
            assert text['fullText']==summary,(hero,sid,'runtime original-rule match failed')
            c.rpc('observe');button=next(x for x in c.observation['controls'] if x['name']==name and x['clickable'])
            c.rpc('hover',path=button['path'],expectedVersion=c.observation['version']);record=c.snapshot('skill_'+str(i)+'_reader_top');seen=set();scrolls=0
            for step in range(30):
                body=next(t for t in record['evaluation']['texts'] if t['path'].endswith('/EntityHoverViewport/Body'))
                assert rule in body['fullText'] and body['fontSize']>=22,(hero,sid)
                viewport=next(v['screenRect'] for v in record['evaluation']['presentationSurfaces'] if v['name']=='EntityHoverViewport')
                seen.update(g['sourceIndex'] for g in body['glyphs'] if inside(g['screenRect'],viewport,.1))
                required={j for j,ch in enumerate(body['fullText']) if not ch.isspace()}
                assert state(c.observation)['audit.costNativeState']==native
                if required.issubset(seen):break
                c.rpc('scroll',path=button['path'],expectedVersion=c.observation['version'],scrollY=-3);scrolls+=1;record=c.snapshot('skill_'+str(i)+'_reader_down_'+str(step))
            else:raise AssertionError('Complete native skill rule unreachable: '+sid)
            readers.append(dict(source_id=sid,source_rule=rule,summary=summary,font_size=body['fontSize'],required_glyphs=len(required),seen_glyphs=len(seen),scroll_requests=scrolls))
            dismiss_hover(c);r=c.snapshot('skill_'+str(i)+'_returned')
        def restore_choices():
            for _ in range(6):
                if any(x['name'].startswith('Btn_Skill_') and x['clickable'] for x in c.observation['controls']):return
                if any(x['name']=='Btn_Back' and x['clickable'] for x in c.observation['controls']):c.click('Btn_Back')
                elif any(x['name']=='Btn_Skills' and x['clickable'] for x in c.observation['controls']):c.click('Btn_Skills')
                else:raise AssertionError('No formal return path from skill entry')
                assert state(c.observation)['audit.costNativeState']==native;dismiss_hover(c)
            raise AssertionError('Skill return did not converge')
        for i,sid in enumerate(n['skills']):
            restore_choices();c.click('Btn_Skill_'+str(i));dismiss_hover(c);c.snapshot('skill_'+str(i)+'_entry')
            assert state(c.observation)['audit.costNativeState']==native
            restore_choices()
        rows.append(dict(hero=hero,readers=readers,actual_entries_opened=len(n['skills']),captures=c.captures));print(hero,'two source-matched summary/full readers and entries passed',flush=True)
    if rows:
        save(out/'seven_hero_manifest.json',dict(passed=True,rows=rows,scope='Seven formal hero builds,14 current learned skills: exact original-rule-bound summary and actual22pt all-glyph full-rule reading. Not all70 runtime activation/consumer coverage.'))
    c=Collector((out/'five_skills').resolve(),live.resolve())
    fixture=json.loads((BASE/'attempt34/validation_corrected/setups/three_enemies.json').read_text())['fixture'];task=copy.deepcopy(fixture['task'])
    task['Build']['Fame']=99;task['Build']['SkillIds']=['hero_skill_pool_005:'+f'{i:02}' for i in range(5)]
    c.demo.reset_task(task);c.demo.prepare_prefix([FIGHT]);c.demo.design(False);c.rpc('observe');dismiss_hover(c)
    native=state(c.observation)['audit.costNativeState'];n=json.loads(native);assert n['level']==10 and len(n['skills'])==5,(n['level'],n['skills'])
    save(c.out/'formal_entry.json',dict(task=task,prefix=[FIGHT],native=n,scope='Conditional legal level10 five learned slots, not historical trajectory frequency.'))
    c.click('Btn_Skills');dismiss_hover(c);records=[c.snapshot('top')];selected=[]
    for i in range(5):
        name='Btn_Skill_'+str(i)
        for step in range(4):
            if any(x['name']==name and x['clickable'] for x in c.observation['controls']):break
            c.click('Btn_ChoiceScrollDown');dismiss_hover(c);records.append(c.snapshot('seek_'+str(i)+'_'+str(step)))
        c.click(name);dismiss_hover(c);records.append(c.snapshot('selected_'+str(i)))
        record=records[-1]
        viewport=next(v['screenRect'] for v in record['evaluation']['presentationSurfaces'] if v['name']=='ChoiceViewport')
        control=next(v for v in record['observation']['controls'] if v['name']==name)
        # Pilot controls use screen-center coordinates with Unity's bottom origin;
        # evaluation rectangles use the top-left origin of the PNG.
        hit=dict(x=control['x']-control['width']/2,y=record['evaluation']['screenHeight']-control['y']-control['height']/2,width=control['width'],height=control['height'])
        assert inside(hit,viewport,.1),('Selected skill not fully in viewport',name,hit,viewport)
        assert state(c.observation)['audit.costNativeState']==native
        assert json.loads(state(c.observation)['audit.costFixture'])['uiSkill']==n['skills'][i]
        selected.append(n['skills'][i])
    c.click('Btn_ChoiceScrollDown');dismiss_hover(c);records.append(c.snapshot('bottom'))
    seen={};expected={};fonts={}
    for record in records:
        viewport=next(v['screenRect'] for v in record['evaluation']['presentationSurfaces'] if v['name']=='ChoiceViewport')
        for text in record['evaluation']['texts']:
            if text['name'] not in ['ChoiceIdentity','ChoiceUsage','ChoiceFrequency','ChoiceEffectSummary','ChoiceRuleHint']:continue
            key=text['path'];expected[key]={j for j,ch in enumerate(text['fullText']) if not ch.isspace()};fonts[key]=text['fontSize']
            seen.setdefault(key,set()).update(g['sourceIndex'] for g in text['glyphs'] if inside(g['screenRect'],viewport,.1))
    assert len(expected)==20,len(expected)
    assert not any(t['name']=='ChoiceRuleHint' for record in records for t in record['evaluation']['texts'])
    assert all('悬浮技能查看完整规则与费用' in next(t['fullText'] for t in record['evaluation']['texts'] if t['name']=='SkillLabel') for record in records)
    for key,required in expected.items():
        assert required.issubset(seen[key]),(key,required-seen[key]);assert fonts[key]>=22
    save(c.out/'manifest.json',dict(passed=True,actual_level=n['level'],selected_sources=selected,captures=c.captures,complete_visible_text_blocks=len(expected),native_unchanged=True,
        all_selected_hits_fully_in_viewport=True,single_global_full_rule_hint=True,
        scope='Legal level10 five-skills top-to-bottom actual navigation and all5 pointer selections.20 complete name/status/frequency/effect blocks seen across measured viewport; selected hit rectangle fully inside, one global full-rule hint replaces repeated per-item text. Not all5 consumers or all layouts.'))
    print('Legal five-skill pressure: full selected rows and20 text blocks passed',flush=True)

if __name__=='__main__':main()
