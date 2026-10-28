"""Read actual entity hover text through its mask, return to the same selection."""
import argparse
import json
from pathlib import Path
from PIL import Image
from capture_evaluation_cycle import Collector, save, ROOT
from capture_embedded_asset_cases import dismiss_hover
from structured_ui_ppo import state


def intersect(a,b):
    return min(a['x']+a['width'],b['x']+b['width'])>max(a['x'],b['x']) and min(a['y']+a['height'],b['y']+b['height'])>max(a['y'],b['y'])


def inside(a,b,slack=0):
    return a['x']>=b['x']-slack and a['y']>=b['y']-slack and a['x']+a['width']<=b['x']+b['width']+slack and a['y']+a['height']<=b['y']+b['height']+slack


def inspect(record):
    ev=record['evaluation'];panel=next(x['screenRect'] for x in ev['presentationSurfaces'] if x['name']=='BattleHoverInfo')
    viewport=next(x['screenRect'] for x in ev['presentationSurfaces'] if x['name']=='EntityHoverViewport')
    body=next(t for t in ev['texts'] if t['path'].endswith('/EntityHoverViewport/Body'))
    assert body['fontSize']>=21 and body['hasVisibleGlyphs'] and not body['isTextOverflowing']
    assert inside(panel,dict(x=0,y=0,width=1920,height=1080))
    assert panel['y']+panel['height']<=700.1
    assert '乌顿守卫' in next(t['fullText'] for t in ev['texts'] if t['name']=='HoverContext')
    glyphs={g['sourceIndex'] for g in body['glyphs'] if inside(g['screenRect'],viewport,.1)}
    figures=[]
    for im in ev['rawImages']:
        if im['name'] not in ['PartyFigure','EnemyIllustration']:continue
        category='EnemyCharacters' if im['name']=='EnemyIllustration' else 'HeroCharacters' if 'HeroActor' in im['path'] else 'UnitCharacters'
        paths=[ROOT/'Assets/Resources/UI'/category/version/(im['texture']+'.png') for version in ['embedded_v2','embedded_v1']]
        path=next(p for p in paths if p.exists())
        with Image.open(path) as image:b=image.getchannel('A').getbbox()
        r=im['screenRect'];sx=r['width']/im['sourceWidth'];sy=r['height']/im['sourceHeight']
        alpha=dict(x=r['x']+b[0]*sx,y=r['y']+b[1]*sy,width=(b[2]-b[0])*sx,height=(b[3]-b[1])*sy)
        assert not intersect(panel,alpha),(im['path'],panel,alpha)
        figures.append(dict(path=im['path'],alpha_bbox=alpha))
    return body,viewport,glyphs,dict(panel=panel,viewport=viewport,figures=figures)


def compare_actions(record,source):
    texts={t['path']:t for t in source['evaluation']['texts']}
    buttons=[b for b in source['observation']['controls'] if '/Inspector/' in b['path'] and '/Actions/' in b['path']]
    captions=[]
    for b in buttons:
        label=texts[b['path']+'/Label']['fullText']
        captions.append(('可用：' if b['interactable'] else '不可用：')+label)
    status=next((t['fullText'] for t in texts.values() if '/Inspector/' in t['path'] and t['name']=='UnitAvailability'),'')
    expected=(status+'\n' if status else '')+'\n'.join(captions)
    ev=record['evaluation'];actual=next(t for t in ev['texts'] if t['name']=='SelectedActionComparison')
    assert actual['fullText']==expected,(actual['fullText'],expected)
    assert actual['fontSize']>=22 and not actual['isTextOverflowing']
    assert {i for i,ch in enumerate(expected) if not ch.isspace()}.issubset({g['sourceIndex'] for g in actual['glyphs']})
    panel=next(s['screenRect'] for s in ev['presentationSurfaces'] if s['name']=='BattleHoverInfo')
    viewport=next(s['screenRect'] for s in ev['presentationSurfaces'] if s['name']=='EntityHoverViewport')
    assert viewport['height']>=160 and not intersect(actual['screenRect'],viewport)
    assert all(inside(g['screenRect'],panel) for g in actual['glyphs'])
    return dict(full_actual_caption_copy=expected,native_button_count=len(buttons),font_size=actual['fontSize'],all_source_glyphs_visible=True,viewport_height=viewport['height'])


def main():
    p=argparse.ArgumentParser(description=__doc__)
    for name in ['out','live','fixture','before']:p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--before-suffix',default='_focus_hover')
    p.add_argument('--compare-actions',action='store_true')
    args=p.parse_args();args.out.mkdir(parents=True,exist_ok=False);c=Collector(args.out.resolve(),args.live.resolve())
    c.prepare(json.loads(args.fixture.read_text(encoding='utf8'))['fixture']);c.click('Btn_Next');c.click('Btn_UnitActor_1');dismiss_hover(c)
    results=[]
    try:
        for label,name in [('hero','Btn_HeroActor'),('unit','Btn_UnitActor_0'),('enemy','Btn_Target')]:
            control=next(x for x in c.observation['controls'] if x['name']==name and x['clickable'])
            prior=state(c.observation);source=c.snapshot(label+'_before')
            response=c.rpc('probe',path=control['path'],expectedVersion=c.observation['version']);assert response['raycastProbes'][0]['valid']
            c.rpc('hover',path=control['path'],expectedVersion=c.observation['version']);record=c.snapshot(label+'_top')
            body,viewport,seen,geometry=inspect(record);full=body['fullText']
            comparison=compare_actions(record,source) if args.compare_actions else None
            old=json.loads((args.before/'key_phases'/(label+args.before_suffix)/'capture.json').read_text(encoding='utf8'))
            old_state=state(old['observation']);assert old_state['audit.costNativeState']==prior['audit.costNativeState']
            old_body=next(t for t in old['evaluation']['texts'] if '/BattleHoverInfo/' in t['path'] and t['path'].endswith('/Body'))
            assert full.startswith(old_body['fullText']),label
            printed_rules=[]
            if label=='unit':
                row=next(x for x in json.loads((ROOT/'resources/text_json/army.json').read_text(encoding='utf8')) if x['id']=='army_008')
                for ability in row['abilities'][1:]:
                    rule=ability['effect'] if isinstance(ability,dict) else ability
                    assert rule in full,rule
                    fee=ability['mana_cost']+'色魔力' if isinstance(ability,dict) else '免费'
                    assert rule+'\n印刷费用：'+fee in full,(rule,fee)
                    printed_rules.append(rule)
            assert '悬浮查看' in next(t['fullText'] for t in record['evaluation']['texts'] if t['name']=='Heading')
            advance=next(x for x in c.observation['controls'] if x['name']=='Btn_Next')
            probe=c.rpc('probe',path=advance['path'],expectedVersion=c.observation['version']);assert probe['raycastProbes'][0]['valid']
            for index in range(30):
                if body['screenRect']['y']+body['screenRect']['height']<=viewport['y']+viewport['height']+.2:break
                c.rpc('scroll',path=control['path'],expectedVersion=c.observation['version'],scrollY=-3)
                record=c.snapshot(label+'_down_'+str(index));body,viewport,visible,_=inspect(record);seen|=visible
                assert body['fullText']==full
            else:raise AssertionError('Reader did not reach final line')
            end=c.snapshot(label+'_end')
            expected={i for i,ch in enumerate(full) if not ch.isspace()};assert expected.issubset(seen),(label,sorted(expected-seen))
            for index in range(30):
                if body['screenRect']['y']>=viewport['y']-.2:break
                c.rpc('scroll',path=control['path'],expectedVersion=c.observation['version'],scrollY=3)
                record=c.snapshot(label+'_up_'+str(index));body,viewport,_,_=inspect(record)
            else:raise AssertionError('Reader did not return to first line')
            c.snapshot(label+'_top_returned')
            assert state(c.observation)['audit.costNativeState']==prior['audit.costNativeState']
            assert state(c.observation)['audit.costFixture']==prior['audit.costFixture']
            c.rpc('unhover',path=control['path'],expectedVersion=c.observation['version']);record=c.snapshot(label+'_returned')
            assert not any(x['name']=='BattleHoverInfo' for x in record['evaluation']['presentationSurfaces'])
            assert state(c.observation)['audit.costFixture']==prior['audit.costFixture']
            results.append(dict(actor=label,path=control['path'],full_text=full,old_fulltext_preserved_as_prefix=True,printed_rules_from_source=printed_rules,expected_glyphs=len(expected),visible_through_mask=len(seen),native_and_selection_unchanged=True,phase_button_probe=probe['raycastProbes'][0],geometry=geometry,selected_comparison=comparison))
        before=state(c.observation);c.click('Btn_UnitAbility_1');dismiss_hover(c);c.snapshot('resumed_guard_block4')
        assert int(state(c.observation)['block'])-int(before['block'])==4
        assert not json.loads(state(c.observation)['audit.costNativeState'])['units'][1]['ready']
        if args.compare_actions:
            source=c.snapshot('exhausted_guard_before')
            control=next(x for x in c.observation['controls'] if x['name']=='Btn_UnitActor_0')
            prior=state(c.observation)
            c.rpc('hover',path=control['path'],expectedVersion=c.observation['version']);record=c.snapshot('exhausted_guard_comparison')
            disabled=compare_actions(record,source);assert '不可用：' in disabled['full_actual_caption_copy']
            assert state(c.observation)['audit.costNativeState']==prior['audit.costNativeState']
            c.rpc('unhover',path=control['path'],expectedVersion=c.observation['version'])
            save(args.out/'disabled_comparison.json',dict(passed=True,comparison=disabled))
        save(args.out/'manifest.json',dict(passed=True,results=results,captures=c.captures,resumed_guard_block4=True,scope='3 actual actors, old full text preserved plus native printed unit abilities, mask-aware glyph coverage, down/up EventSystem scroll, exit restores selection and formal guard consumer; no physical OS pointer or all-entity/human acceptance.'))
    except Exception as error:
        save(args.out/'failure.json',dict(error=repr(error),captures=c.captures));raise


if __name__=='__main__':main()
