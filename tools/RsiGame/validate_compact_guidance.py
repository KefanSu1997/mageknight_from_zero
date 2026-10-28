"""Actual short-help bounds versus alpha silhouettes and actor hover feedback."""
import argparse
import json
from pathlib import Path
from PIL import Image
from capture_evaluation_cycle import Collector, save, ROOT
from capture_embedded_asset_cases import dismiss_hover
from structured_ui_ppo import state


def overlap(a,b):
    return min(a['x']+a['width'],b['x']+b['width'])>max(a['x'],b['x']) and min(a['y']+a['height'],b['y']+b['height'])>max(a['y'],b['y'])


def audit(record):
    ev=record['evaluation'];panel=next(x['screenRect'] for x in ev['presentationSurfaces'] if x['name']=='BattleHoverInfo')
    body=next(t for t in ev['texts'] if t['path'].endswith('/BattleHoverInfo/Body'))
    assert body['fontSize']>=21 and not body['isTextOverflowing']
    assert {i for i,ch in enumerate(body['fullText']) if not ch.isspace()}.issubset({g['sourceIndex'] for g in body['glyphs']})
    for g in body['glyphs']:
        r=g['screenRect'];assert r['x']>=panel['x'] and r['x']+r['width']<=panel['x']+panel['width']
        assert r['y']>=panel['y'] and r['y']+r['height']<=panel['y']+panel['height']
    figures=[]
    for image in ev['rawImages']:
        if image['name']!='PartyFigure':continue
        matches=[]
        for category in ['HeroCharacters','UnitCharacters']:
            for version in ['embedded_v2','embedded_v1']:
                path=ROOT/'Assets/Resources/UI'/category/version/(image['texture']+'.png')
                if path.exists():matches.append(path);break
        assert len(matches)==1,(image['texture'],matches)
        # Read-only alpha metadata; no pixels altered or cropped.
        with Image.open(matches[0]) as source:bounds=source.getchannel('A').getbbox()
        r=image['screenRect'];sx=r['width']/image['sourceWidth'];sy=r['height']/image['sourceHeight']
        visible=dict(x=r['x']+bounds[0]*sx,y=r['y']+bounds[1]*sy,width=(bounds[2]-bounds[0])*sx,height=(bounds[3]-bounds[1])*sy)
        assert not overlap(panel,visible),(panel,image['path'],visible)
        figures.append(dict(path=image['path'],alpha_bbox=visible))
    assert figures
    return dict(panel=panel,body_font=body['fontSize'],body_text=body['fullText'],no_party_alpha_bbox_overlap=figures)


def main():
    p=argparse.ArgumentParser(description=__doc__)
    for name in ['out','live','fixture','four-task']:p.add_argument('--'+name,type=Path,required=True)
    args=p.parse_args();args.out.mkdir(parents=True,exist_ok=False)
    c=Collector(args.out.resolve(),args.live.resolve());c.prepare(json.loads(args.fixture.read_text(encoding='utf8'))['fixture'])
    c.click('Btn_Next');dismiss_hover(c);help_checks=[];focus_checks=[]
    def help_check(label,path):
        native=state(c.observation)['audit.costNativeState'];selection=state(c.observation)['audit.costFixture']
        response=c.rpc('hover_info',path=path,expectedVersion=c.observation['version']);assert response['raycastProbes'][0]['valid']
        r=c.snapshot(label);help_checks.append(dict(label=label,**audit(r)))
        assert state(c.observation)['audit.costNativeState']==native and state(c.observation)['audit.costFixture']==selection
        c.rpc('unhover_info',path=path,expectedVersion=c.observation['version']);c.snapshot(label+'_returned')
    help_check('phase_hover','Canvas/BattleTable/PhaseTrack/PhaseHelp')
    c.click('Btn_UnitActor_1');dismiss_hover(c)
    help_check('unit_hover','Canvas/BattleTable/Inspector/Actions/ContextInfoHover')
    for label,name in [('hero','Btn_HeroActor'),('unit','Btn_UnitActor_0'),('enemy','Btn_Target')]:
        control=next(x for x in c.observation['controls'] if x['name']==name and x['clickable']);native=state(c.observation)['audit.costNativeState']
        response=c.rpc('probe',path=control['path'],expectedVersion=c.observation['version']);assert response['raycastProbes'][0]['valid']
        c.rpc('hover',path=control['path'],expectedVersion=c.observation['version']);r=c.snapshot(label+'_focus_hover')
        rings=[x for x in r['evaluation']['presentationSurfaces'] if x['name']=='ActorHoverFocus'];assert len(rings)==1 and rings[0]['path'].startswith(control['path']+'/')
        assert state(c.observation)['audit.costNativeState']==native
        c.rpc('unhover',path=control['path'],expectedVersion=c.observation['version']);r=c.snapshot(label+'_focus_returned')
        assert not any(x['name']=='ActorHoverFocus' for x in r['evaluation']['presentationSurfaces'])
        focus_checks.append(dict(actor=label,path=control['path'],ring=rings[0],native_unchanged=True))
    selected=next(x for x in c.observation['controls'] if x['name']=='Btn_UnitActor_1')
    native=state(c.observation)['audit.costNativeState']
    c.rpc('hover',path=selected['path'],expectedVersion=c.observation['version']);r=c.snapshot('selected_unit_focus_hover')
    assert not any(x['name']=='ActorHoverFocus' for x in r['evaluation']['presentationSurfaces'])
    assert state(c.observation)['audit.costNativeState']==native
    c.rpc('unhover',path=selected['path'],expectedVersion=c.observation['version'])
    focus_checks.append(dict(actor='selected_unit',path=selected['path'],no_white_ring_over_gold=True,native_unchanged=True))
    c.click('Btn_Target');dismiss_hover(c)
    selected=next(x for x in c.observation['controls'] if x['name']=='Btn_Target')
    native=state(c.observation)['audit.costNativeState']
    c.rpc('hover',path=selected['path'],expectedVersion=c.observation['version']);r=c.snapshot('selected_enemy_focus_hover')
    assert not any(x['name']=='ActorHoverFocus' for x in r['evaluation']['presentationSurfaces'])
    assert state(c.observation)['audit.costNativeState']==native
    c.rpc('unhover',path=selected['path'],expectedVersion=c.observation['version'])
    focus_checks.append(dict(actor='selected_enemy',path=selected['path'],no_white_ring_over_gold=True,native_unchanged=True))
    task=json.loads(args.four_task.read_text(encoding='utf8'));c.observation=c.demo.reset_task(task)
    c.observation=c.demo.prepare_prefix([dict(Kind='fight',Card='',Choice='',Targets=0,Parts=0,Option=0,Input='',Key='fight:::0:0:0')])
    c.click('Btn_Next');c.click('Btn_UnitActor_3');dismiss_hover(c)
    help_check('four_unit_phase_hover','Canvas/BattleTable/PhaseTrack/PhaseHelp')
    help_check('four_unit_guidance_hover','Canvas/BattleTable/Inspector/Actions/ContextInfoHover')
    save(args.out/'manifest.json',dict(passed=True,help_checks=help_checks,focus_checks=focus_checks,captures=c.captures,scope='Four actual short-help contexts versus conservative alpha>0 party bounding boxes, full21pt text;3 actual actor focus enter/exit events. Not all heroes/long entity tooltips/human discovery.'))


if __name__=='__main__':main()
