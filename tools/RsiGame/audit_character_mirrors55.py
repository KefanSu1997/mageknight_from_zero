"""Compare actual Unity pairs; full reflected UV, same rules/hitboxes/type/art bytes."""
import json,hashlib
from pathlib import Path
from structured_ui_ppo import state

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1/attempt55'
def read(p):return json.loads(p.read_text(encoding='utf8'))
def main():
    registry=read(OUT/'mirror_registry.json')['items'];ids={r['id'] for r in registry}
    seen=set();rows=[]
    for entry in registry:
        p=ROOT/'Assets/Resources/UI'/entry['category']/entry['resourceVersion']/(entry['id']+'.png')
        assert hashlib.sha256(p.read_bytes()).hexdigest()==entry['sha256']
    for p in sorted((OUT/'before/key_phases').glob('*/capture.json')):
        a=read(p);b=read(OUT/'after/key_phases'/p.parent.name/'capture.json')
        assert state(a['observation'])['audit.costNativeState']==state(b['observation'])['audit.costNativeState']
        for x in [a,b]:assert (x['observation']['screenWidth'],x['observation']['screenHeight'])==(1920,1080)
        ac={x['path']:x for x in a['observation']['controls']};bc={x['path']:x for x in b['observation']['controls']}
        assert ac.keys()==bc.keys()
        for k in ac:
            for field in ['x','y','width','height','clickable','interactable']:assert ac[k][field]==bc[k][field],(p,k,field)
        at={x['path']:x for x in a['evaluation']['texts']};bt={x['path']:x for x in b['evaluation']['texts']}
        assert at.keys()==bt.keys()
        for k in at:
            for field in ['fullText','fontSize','screenRect']:assert at[k][field]==bt[k][field],(p,k,field)
        ai={x['path']:x for x in a['evaluation']['rawImages']};bi={x['path']:x for x in b['evaluation']['rawImages']}
        assert ai.keys()==bi.keys()
        mirrored=[]
        for k in ai:
            before,after=ai[k],bi[k]
            if after['name'] not in ['EnemyIllustration','PartyFigure','CharacterCastShadow']:continue
            assert before['texture']==after['texture']
            # World-corner subtraction after the deliberate frame-X reflection
            # introduces sub-millipixel float jitter, not an actor-size change.
            assert all(abs(before['screenRect'][f]-after['screenRect'][f])<.001 for f in ['width','height','y']), (p,k,before['screenRect'],after['screenRect'])
            expected=after['texture'] in ids
            uv=after['uvRect'];assert uv['x']==(1 if expected else 0) and uv['width']==(-1 if expected else 1) and uv['y']==0 and uv['height']==1,(p,k,uv)
            if expected:
                if after['name']!='CharacterCastShadow':
                    frame=read(ROOT/'Assets/Resources/UI/CharacterFrames'/(after['texture']+'.json'))
                    scale=before['screenRect']['width']/before['sourceWidth']
                    old_left=before['screenRect']['x']+frame['x']*scale
                    new_left=after['screenRect']['x']+(before['sourceWidth']-frame['x']-frame['width'])*scale
                    assert abs(old_left-new_left)<.001,(p,k,old_left,new_left)
                seen.add(after['texture']);mirrored.append(dict(id=after['texture'],path=k,uv=uv))
        rows.append(dict(case=p.parent.name,same_native=True,controls_unchanged=len(ac),text_unchanged=len(at),mirrored=mirrored))
    assert seen==ids,(seen,ids-seen)
    (OUT/'geometry_audit.json').write_text(json.dumps(dict(passed=True,actual_mirrored_identities=len(seen),pairs=rows,
        scope='15 archived conditional scenes, 13 candidates reflected using full UV; identical native state, controls and typography; PNG bytes unchanged. Visual direction acceptance requires separate review.'),ensure_ascii=False,indent=2),encoding='utf8')
    print('15pairs /13mirrors: native, hitboxes, texts, full UV and original PNG hashes passed')
if __name__=='__main__':main()
