"""All70 formal skill identities: actual original-art binding and complete rule readers."""
import argparse,copy,json,hashlib
from pathlib import Path
from capture_evaluation_cycle import Collector,save
from capture_embedded_asset_cases import dismiss_hover
from validate_four_unit_battle_layout import FIGHT
from validate_entity_hover_reader import inside
from structured_ui_ppo import state

ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--live',type=Path,required=True);ap.add_argument('--out',type=Path,required=True)
    ap.add_argument('--heroes',nargs='+',default=['Arythea','Braevalar','Goldyx','Krang','Norowas','Tovak','Wolfhawk']);args=ap.parse_args()
    catalog=json.loads((BASE/'attempt54/skill_icon_generation/prompts.json').read_text())['items']
    assert len(catalog)==len({x['sourceId'] for x in catalog})==70
    rows=[]
    for hero in args.heroes:
        source=BASE/'attempt50/seven_hero_corrected'/hero/'formal_entry.json'
        if hero=='Wolfhawk':source=BASE/'attempt46/real_wolfhawk_task.json'
        original=json.loads(source.read_text())['task'];pool=original['Build']['SkillIds'][0].split(':')[0]
        skills=[x for x in catalog if x['sourceId'].startswith(pool+':')];assert len(skills)==10,(hero,pool)
        for x in skills:assert (ROOT/'Assets/Resources/UI/SkillIcons/redrawn_v1'/(x['sourceId'].replace(':','_')+'.png')).is_file(),x['sourceId']
        for group in range(2):
            selected=skills[group*5:(group+1)*5];c=Collector((args.out/hero/str(group)).resolve(),args.live.resolve())
            task=copy.deepcopy(original);task['Build']['Fame']=99;task['Build']['SkillIds']=[x['sourceId'] for x in selected]
            c.demo.reset_task(task);c.demo.prepare_prefix([FIGHT]);c.demo.design(False);c.rpc('observe');dismiss_hover(c)
            native=state(c.observation)['audit.costNativeState'];n=json.loads(native)
            assert n['hero']==hero and n['level']==10 and n['skills']==task['Build']['SkillIds'],(hero,n['skills'])
            save(c.out/'formal_entry.json',dict(task=task,prefix=[FIGHT],source=str(source),native=n,scope='Conditional legal5skills atlevel10, formal reset/fight; no historicalfrequency claim.'))
            c.click('Btn_Skills');dismiss_hover(c);c.snapshot('top')
            for i,entry in enumerate(selected):
                name='Btn_Skill_'+str(i)
                for seek in range(4):
                    if any(x['name']==name and x['clickable'] for x in c.observation['controls']):break
                    c.click('Btn_ChoiceScrollDown');dismiss_hover(c)
                c.rpc('observe');button=next(x for x in c.observation['controls'] if x['name']==name and x['clickable'])
                r=c.snapshot('icon_'+str(i));path=button['path']+'/RedrawnSkillIcon'
                icon=next(x for x in r['evaluation']['rawImages'] if x['path']==path)
                assert state(r['observation'])['ui.skillRedrawnIconCount']=='5'
                assert state(r['observation'])['ui.skillOriginalPictogramCount']=='0'
                assert icon['texture']==entry['sourceId'].replace(':','_')
                assert icon['uvRect']['x']==icon['uvRect']['y']==0 and icon['uvRect']['width']==icon['uvRect']['height']==1
                assert not any(x['name']=='OriginalSkillPictogram' for x in r['evaluation']['rawImages'])
                assert not any(x['name']=='SkillFallbackGlyph' for x in r['evaluation']['presentationSurfaces'])
                viewport=next(x['screenRect'] for x in r['evaluation']['presentationSurfaces'] if x['name']=='ChoiceViewport')
                assert inside(icon['screenRect'],viewport,.1),(entry['sourceId'],icon['screenRect'],viewport)
                c.rpc('hover',path=button['path'],expectedVersion=c.observation['version']);r=c.snapshot('rule_'+str(i)+'_top');seen=set()
                for scroll in range(30):
                    body=next(t for t in r['evaluation']['texts'] if t['path'].endswith('/EntityHoverViewport/Body'))
                    assert entry['originalRule'] in body['fullText'] and body['fontSize']>=22,entry['sourceId']
                    view=next(x['screenRect'] for x in r['evaluation']['presentationSurfaces'] if x['name']=='EntityHoverViewport')
                    seen.update(g['sourceIndex'] for g in body['glyphs'] if inside(g['screenRect'],view,.1))
                    needed={j for j,ch in enumerate(body['fullText']) if not ch.isspace()}
                    assert state(c.observation)['audit.costNativeState']==native
                    if needed.issubset(seen):break
                    c.rpc('scroll',path=button['path'],expectedVersion=c.observation['version'],scrollY=-3);r=c.snapshot('rule_'+str(i)+'_down_'+str(scroll))
                else:raise AssertionError('Rule unreadable: '+entry['sourceId'])
                dismiss_hover(c)
                rows.append(dict(hero=hero,sourceId=entry['sourceId'],name=entry['name'],texture=icon['texture'],full_uv=True,icon_rect=icon['screenRect'],all_rule_glyphs_read=len(needed),font_size=body['fontSize'],original_rule=entry['originalRule'],native_unchanged=True))
                print('actual icon + complete rule',entry['sourceId'],flush=True)
            c.snapshot('bottom');save(c.out/'manifest.json',dict(passed=True,captures=c.captures,rows=rows[-5:]))
    assert len(rows)==len({x['sourceId'] for x in rows})==10*len(args.heroes)
    save(args.out/'manifest.json',dict(passed=True,heroes=args.heroes,actual_source_count=len(rows),rows=rows,scope='Actual source identities in formal legal5skill builds, full-UV generated icons and complete22pt printed-rule hover reading; no native mutation. Not activation consumers or human iconrecognition/visualacceptance.'))

if __name__=='__main__':main()
