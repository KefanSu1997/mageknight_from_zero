"""Audit seven actual material47 pairs without equating new anatomy dimensions."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1/attempt47'

def read(p):
    return json.loads(p.read_text(encoding='utf-8'))

def main():
    imports = {r['id']: r for r in read(OUT/'candidate_import.json')['records']}
    for r in imports.values():
        assert hashlib.sha256(Path(r['source']).read_bytes()).hexdigest() == r['sha256']
        assert hashlib.sha256(Path(r['target']).read_bytes()).hexdigest() == r['sha256']
    results=[]
    for p in sorted((OUT/'after/key_phases').glob('*/capture.json')):
        old=read(OUT/'before/key_phases'/p.parent.name/'capture.json'); new=read(p)
        a={x['key']:x['value'] for x in old['observation']['state']}
        b={x['key']:x['value'] for x in new['observation']['state']}
        assert a['audit.costNativeState']==b['audit.costNativeState']
        e=new['evaluation']; assert (e['screenWidth'],e['screenHeight'])==(1920,1080)
        controls={x['path']:x for x in old['observation']['controls']}
        for c in new['observation']['controls']:
            prev=controls.pop(c['path'])
            for k in ('x','y','width','height','interactable','clickable'):
                assert c.get(k)==prev.get(k),(p,k,c['path'])
        assert not controls
        texts={x['path']:x for x in old['evaluation']['texts']}; checked=0
        for t in e['texts']:
            if not any('/'+n in t['path'] for n in ('Enemy_','Pools','HeroName','UnitLabel')): continue
            prev=texts[t['path']]
            assert t['fullText']==prev['fullText'] and t['fontSize']>=prev['fontSize'],(p,t['path'])
            assert not t['isTextOverflowing'],(p,t['path'])
            assert ''.join(g['character'] for g in t['glyphs'])==''.join(c for c in t['fullText'] if not c.isspace()),(p,t['path'])
            checked+=1
        images={x['path']:x for x in old['evaluation']['rawImages']}; changes=[]
        for im in e['rawImages']:
            prev=images.pop(im['path']); tex=im['texture']
            assert im['uvRect']==prev['uvRect'] and im['shaderSupported'],(p,im['path'])
            if tex in imports:
                assert im['uvRect']['x']==im['uvRect']['y']==0 and im['uvRect']['width']==im['uvRect']['height']==1
                changes.append(dict(path=im['path'],before=prev['screenRect'],after=im['screenRect']))
            else:
                assert im['texture']==prev['texture'] and im['screenRect']==prev['screenRect'],(p,im['path'])
        assert not images
        results.append(dict(label=p.parent.name,text_blocks_checked=checked,changed_art_and_projection_rects=changes))
    assert len(results)==7
    result=dict(passed=True,pairs=results,source_byte_sha_verified=True,
        scope='Seven same-native actual captures; formal hit rectangles/interactivity unchanged; selected text full glyphs and no reduced fonts; unchanged raw images retain dimensions/UV; two declared art anatomies and decorative projections differ. Not aesthetic, core-alpha overlap, full catalog or human acceptance.')
    (OUT/'geometry_audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print('Passed seven material pairs')

if __name__=='__main__': main()
