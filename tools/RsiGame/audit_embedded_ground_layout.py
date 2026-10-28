"""Audit actual before/after glyphs and full-UV image geometry after grounding."""
import argparse
import json
from pathlib import Path
import re


def read(path):
    return json.loads(path.read_text(encoding='utf8'))


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--before',type=Path,required=True)
    p.add_argument('--after',type=Path,required=True)
    p.add_argument('--asset-manifest',type=Path,help='Explicitly audited imports; changed art is separated from unchanged-image size checks.')
    p.add_argument('--capture-group',help='Limit to a named capture group, e.g. asset_cases.')
    p.add_argument('--baseline-group',help='Map that group to its actual separately captured baseline, e.g. before_assets.')
    p.add_argument('--out',type=Path,help='Separate output when auditing more than one comparison scope.')
    args=p.parse_args()
    if args.baseline_group and not args.capture_group:p.error('--baseline-group requires --capture-group')
    replacements={x['id'] for x in read(args.asset_manifest)['records']} if args.asset_manifest else set()
    records=[]
    for candidate in sorted(args.after.glob('*/key_phases/*/capture.json')):
        relative=candidate.relative_to(args.after)
        if args.capture_group and relative.parts[0]!=args.capture_group:continue
        prior=args.before/((Path(args.baseline_group)/Path(*relative.parts[1:])) if args.baseline_group else relative)
        assert prior.resolve()!=candidate.resolve(),'A capture cannot be its own baseline'
        if not prior.exists():
            continue
        a,b=read(prior),read(candidate)
        before_state={x['key']:x['value'] for x in a['observation']['state']}
        after_state={x['key']:x['value'] for x in b['observation']['state']}
        assert before_state['audit.costNativeState']==after_state['audit.costNativeState'],candidate
        assert (b['evaluation']['screenWidth'],b['evaluation']['screenHeight'])==(1920,1080)
        previous={t['path']:t for t in a['evaluation']['texts']}
        current={t['path']:t for t in b['evaluation']['texts']}
        texts=[]
        for path,t in current.items():
            if not (re.search(r'/Enemy_\d+/(Name|Numbers|State|EnemyIntent)$',path) or path.endswith('/Pools')):
                continue
            assert path in previous,path
            old=previous[path]
            assert t['fullText']==old['fullText'],path
            assert t['fontSize']>=old['fontSize'],path
            assert not t['isTextOverflowing'],(candidate,path)
            expected=''.join(x for x in t['fullText'] if not x.isspace())
            actual=''.join(g['character'] for g in t['glyphs'])
            assert actual==expected,(candidate,path,expected,actual)
            rect=t['screenRect']
            for glyph in t['glyphs']:
                r=glyph['screenRect']
                assert r['x']>=rect['x']-3 and r['x']+r['width']<=rect['x']+rect['width']+3,(candidate,path,glyph)
                assert r['y']>=rect['y']-3 and r['y']+r['height']<=rect['y']+rect['height']+3,(candidate,path,glyph)
            texts.append(dict(path=path,fullText=t['fullText'],fontSize=t['fontSize'],glyphs=len(t['glyphs']),screenRect=rect))
        images={i['path']:i for i in a['evaluation']['rawImages']}
        image_records=[]
        for image in b['evaluation']['rawImages']:
            path=image['path']
            if not path.endswith('/EnemyIllustration') or path not in images:
                continue
            old=images[path]
            index=re.search(r'/Enemy_(\d+)/',path).group(1)
            before_version=before_state.get('ui.enemyArt.'+index+'.resourceVersion')
            after_version=after_state.get('ui.enemyArt.'+index+'.resourceVersion')
            changed=image['texture']!=old['texture'] or before_version!=after_version
            if changed:
                identity=json.loads(after_state['audit.costNativeState'])['enemies'][int(index)]['id']
                assert identity in replacements,(candidate,path,identity,'undeclared replacement')
                assert image['texture']==identity and after_version=='embedded_v1',(candidate,path)
                assert image['uvRect']==old['uvRect'],(candidate,path,'changed UV')
                image_records.append(dict(path=path,texture=image['texture'],uvRect=image['uvRect'],screenRect=image['screenRect'],
                    comparison='Declared original-token to full-body replacement; different anatomy/aspect, size not equated; requires independent visual review.'))
                continue
            assert image['texture']==old['texture'] and image['uvRect']==old['uvRect'],path
            for dimension in ['width','height']:
                assert abs(image['screenRect'][dimension]-old['screenRect'][dimension])<.1,(path,dimension)
            image_records.append(dict(path=path,texture=image['texture'],uvRect=image['uvRect'],screenRect=image['screenRect'],comparison='Same texture, version, UV and dimensions.'))
        records.append(dict(capture=str(candidate),texts=texts,images=image_records))
    assert records,'No matching actual captures'
    result=dict(passed=True,records=records,matched_captures=len(records),
        scope='Actual same native state, glyph text/font/bounds, unchanged-image size/UV; declared replacements separated with full UV preserved. Replacement dimensions require visual review. Not all-catalog, dynamic occlusion or human acceptance.')
    destination=args.out or args.after/'ground_layout_geometry.json'
    with destination.open('x',encoding='utf8') as stream:
        json.dump(result,stream,ensure_ascii=False,indent=2)
    print('Passed',len(records),'actual capture comparisons')


if __name__=='__main__':
    main()
