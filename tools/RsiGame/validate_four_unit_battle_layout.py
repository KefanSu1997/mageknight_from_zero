"""Conditional real level-seven four-unit UI, native capacity and pointer checks."""
import argparse
import copy
import json
from pathlib import Path
from capture_evaluation_cycle import Collector, save
from capture_embedded_asset_cases import dismiss_hover, check_unit_names
from structured_ui_ppo import state

FIGHT=dict(Kind='fight',Card='',Choice='',Targets=0,Parts=0,Option=0,Input='',Key='fight:::0:0:0')

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--out',type=Path,required=True);p.add_argument('--live',type=Path,required=True)
    p.add_argument('--task',type=Path,required=True);p.add_argument('--before',type=Path,required=True)
    args=p.parse_args();args.out.mkdir(parents=True,exist_ok=False)
    c=Collector(args.out.resolve(),args.live.resolve());task=json.loads(args.task.read_text(encoding='utf8'))
    def check_ability_caption(record):
        text=next(t for t in record['evaluation']['texts'] if t['path'].endswith('/Btn_UnitAbility_0/Label'))
        assert text['fontSize']>=21 and not text['isTextOverflowing'] and text['hasVisibleGlyphs']
        rect=text['screenRect'];glyphs=text['glyphs'];assert glyphs
        expected={i for i,ch in enumerate(text['fullText']) if not ch.isspace()}
        assert expected.issubset({g['sourceIndex'] for g in glyphs}),text['fullText']
        for glyph in glyphs:
            box=glyph['screenRect']
            assert box['x']>=rect['x']-2 and box['x']+box['width']<=rect['x']+rect['width']+2,box
            assert box['y']>=rect['y']-2 and box['y']+box['height']<=rect['y']+rect['height']+2,box
        return dict(path=text['path'],text=text['fullText'],font_size=text['fontSize'],rect=rect,glyph_count=len(glyphs),all_glyphs_inside=True)
    def setup(definition):
        c.observation=c.demo.reset_task(definition);c.observation=c.demo.prepare_prefix([FIGHT]);dismiss_hover(c)
    try:
        setup(task);r=c.snapshot('four_units_neutral');check_unit_names(r)
        native=state(c.observation)['audit.costNativeState'];n=json.loads(native)
        assert len(n['units'])==4 and n['commandSlots']==4
        old=json.loads(args.before.read_text(encoding='utf8'))
        assert native==state(old['observation'])['audit.costNativeState']
        previous={i['path']:i for i in old['evaluation']['rawImages']}
        unchanged=[]
        for im in r['evaluation']['rawImages']:
            if not im['path'].endswith(('/PartyFigure','/EnemyIllustration')):continue
            prior=previous[im['path']];assert im['texture']==prior['texture'] and im['uvRect']==prior['uvRect']
            for dimension in ['width','height']:assert abs(im['screenRect'][dimension]-prior['screenRect'][dimension])<.1
            unchanged.append(dict(path=im['path'],texture=im['texture'],before=prior['screenRect'],after=im['screenRect']))
        assert len(unchanged)==6
        actors=[x for x in c.observation['controls'] if x['name'].startswith('Btn_UnitActor_')]
        assert len(actors)==4 and all(x['clickable'] for x in actors)
        for i in range(4):
            c.click('Btn_UnitActor_'+str(i));dismiss_hover(c)
            assert state(c.observation)['audit.costNativeState']==native
            assert json.loads(state(c.observation)['audit.costFixture'])['uiUnit']==task['Build']['UnitIds'][i]
        c.snapshot('fourth_unit_selected')
        c.click('Btn_Next');c.click('Btn_UnitActor_3');dismiss_hover(c)
        before=state(c.observation);c.click('Btn_UnitAbility_0');dismiss_hover(c);consumed=c.snapshot('fourth_unit_block5_consumed')
        caption_geometry=check_ability_caption(consumed)
        after=state(c.observation)
        assert int(after['block'])-int(before['block'])==5
        assert not json.loads(after['audit.costNativeState'])['units'][3]['ready']
        button=next(x for x in c.observation['controls'] if x['name']=='Btn_UnitAbility_0')
        assert not button['interactable'] and not button['clickable']
        negatives=[]
        for label,invalid in [('level5_four',copy.deepcopy(task)),('level7_five',copy.deepcopy(task))]:
            if label=='level5_four':invalid['Build']['Fame']=24
            else:invalid['Build']['UnitIds'].append('army_004')
            try:c.demo.reset_task(invalid)
            except RuntimeError as error:
                assert 'Units exceed actual ordinary command slots' in str(error)
                negatives.append(dict(label=label,error=str(error)))
            else:raise AssertionError('Over-capacity fixture accepted')
            c.rpc('observe');assert state(c.observation)['audit.costNativeState']==after['audit.costNativeState']
        multi=copy.deepcopy(task);multi['EnemyGroups']=[['monster_001','monster_009','monster_010']]
        setup(multi);r=c.snapshot('four_units_three_enemies');check_unit_names(r)
        original=state(c.observation)['audit.costNativeState']
        enemies=[x for x in c.observation['controls'] if x['name']=='Btn_Target']
        assert len(enemies)==3 and all(x['clickable'] for x in enemies)
        for i in range(3):
            c.click('Btn_Target',occurrence=i);dismiss_hover(c)
            assert '/Enemy_'+str(i)+'/' in c.events[-1]['path']
            assert i in json.loads(state(c.observation)['audit.costFixture'])['uiTargets']
            assert state(c.observation)['audit.costNativeState']==original
            c.snapshot('four_units_enemy_'+str(i)+'_selected')
        save(args.out/'manifest.json',dict(passed=True,task=task,three_enemy_task=multi,captures=c.captures,
            before_after_same_native_state=True,unchanged_figure_dimensions_and_uv=unchanged,capacity_negatives=negatives,consumed_ability_caption=caption_geometry,
            fourth_unit_block5=True,scope='Conditional real level7 four ordinary slots; not archived PPO, 5+ allies, every combination or human acceptance. Three-enemy captures are additional coverage without a same-state old layout baseline.'))
    except Exception as error:
        save(args.out/'failure.json',dict(error=repr(error),captures=c.captures));raise

if __name__=='__main__':main()
