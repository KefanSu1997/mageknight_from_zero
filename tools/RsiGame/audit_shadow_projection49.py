"""Actual-scene49 shadow-only contract: no actor, text or formal-hit shrink."""
from pathlib import Path
import json
from structured_ui_ppo import state

ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'
OUT=BASE/'attempt49'

def read(p):return json.loads(p.read_text(encoding='utf-8'))

def main():
    rows=[]
    for p in sorted((OUT/'after/key_phases').glob('*/capture.json')):
        a=read(OUT/'before/key_phases'/p.parent.name/'capture.json');b=read(p)
        assert state(a['observation'])['audit.costNativeState']==state(b['observation'])['audit.costNativeState']
        e=b['evaluation'];assert (e['screenWidth'],e['screenHeight'])==(1920,1080)
        old={c['path']:c for c in a['observation']['controls']};new={c['path']:c for c in b['observation']['controls']};assert old.keys()==new.keys()
        for path,c in new.items():
            for k in ('x','y','width','height','interactable','clickable'):assert c[k]==old[path][k],(p,path,k)
        oldtext={t['path']:t for t in a['evaluation']['texts']};newtext={t['path']:t for t in e['texts']};timed=[]
        for path in oldtext.keys()^newtext.keys():
            assert path.endswith('/BattleFeedback/BattleToast'),(p,path);timed.append(path)
        for path in oldtext.keys()&newtext.keys():
            for k in ('fullText','fontSize','screenRect','isTextOverflowing'):assert oldtext[path][k]==newtext[path][k],(p,path,k)
        oldim={im['path']:im for im in a['evaluation']['rawImages']};newim={im['path']:im for im in e['rawImages']};assert oldim.keys()==newim.keys();shadows=[]
        for path,im in newim.items():
            prev=oldim[path]
            for k in ('texture','sourceWidth','sourceHeight','uvRect','componentColor'):assert im[k]==prev[k],(p,path,k)
            assert im['shaderSupported'],(p,path)
            if im['name']=='CharacterCastShadow':
                assert abs(im['projectionOpacity']-.34)<1e-5
                assert abs(im['projectionShear']-im['screenRect']['width']*.24)<.01
                shadows.append(dict(path=path,before_rect=prev['screenRect'],after_rect=im['screenRect'],opacity=im['projectionOpacity'],shear=im['projectionShear']))
            else:assert im['screenRect']==prev['screenRect'],(p,path)
        assert shadows
        rows.append(dict(label=p.parent.name,controls=len(new),text_blocks=len(newtext),timed_toast_visibility_difference=timed,shadows=shadows))
    assert len(rows)==7
    (OUT/'geometry_audit.json').write_text(json.dumps(dict(passed=True,pairs=rows,
        scope='Seven actual same-native scenes; unchanged actor images/fullUV/size/state tint, text and formal hit geometry. Only decorative projected-shadow geometry changes, actual material opacity/shear captured. Not physical lighting, fullcatalog/performance or aesthetic acceptance.'),ensure_ascii=False,indent=2),encoding='utf-8')
    print('49 seven same-state shadow-only contracts passed',flush=True)

if __name__=='__main__':main()
