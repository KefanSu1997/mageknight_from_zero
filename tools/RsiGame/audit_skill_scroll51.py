"""Measured geometry with explicit optional-viewport path normalization."""
import json,hashlib,shutil
from pathlib import Path
from structured_ui_ppo import state
from validate_entity_hover_reader import inside
ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'
OUT=BASE/'attempt51'
def read(p):return json.loads(p.read_text(encoding='utf8'))
def key(p):return p.replace('/ChoiceViewport/ChoiceContent/','/')
def main():
    rows=[]
    for label in ['neutral','skills_open','skills_selected','roster_open','roster_selected']:
        a=read(BASE/'attempt50/after_wide_final/key_phases'/label/'capture.json');z=read(OUT/'after/key_phases'/label/'capture.json')
        ea=a['evaluation'];ez=z['evaluation']
        assert state(a['observation'])['audit.costNativeState']==state(z['observation'])['audit.costNativeState']
        assert (ez['screenWidth'],ez['screenHeight'])==(1920,1080)
        ai={key(i['path']):i for i in ea['rawImages']};zi={key(i['path']):i for i in ez['rawImages']};assert ai.keys()==zi.keys()
        for p in ai:
            for k in ['texture','screenRect','uvRect','componentColor','sourceWidth','sourceHeight','shader','atmosphereSaturation','atmosphereTop','atmosphereGround']:
                assert ai[p][k]==zi[p][k],(label,p,k)
        at={key(t['path']):t for t in ea['texts']};zt={key(t['path']):t for t in ez['texts']};removed=at.keys()-zt.keys()
        assert all(p.endswith('/ChoiceRuleHint') or '/Btn_ChoiceScroll' in p for p in removed),(label,removed)
        empty_hits=[]
        for p in at.keys()&zt.keys():
            if p.endswith('/Label') and not at[p]['fullText'] and not zt[p]['fullText']:
                empty_hits.append(p);continue
            for k in ['fontSize','screenRect','fullText']:
                if p.endswith('/SkillLabel') and k=='fullText':continue
                assert at[p][k]==zt[p][k],(label,p,k)
        if label.startswith('skills'):
            t=next(t for t in ez['texts'] if t['name']=='SkillLabel');assert not t['isTextOverflowing']
            required={i for i,ch in enumerate(t['fullText']) if not ch.isspace()}
            assert required.issubset({g['sourceIndex'] for g in t['glyphs']})
            assert all(inside(g['screenRect'],t['screenRect'],3) for g in t['glyphs'])
        rows.append(dict(label=label,same_native=True,unchanged_raw_images=len(ai),removed_text_paths=sorted(removed),empty_button_labels_with_intentional_hit_height_change=empty_hits))
    result=dict(passed=True,pairs=rows,scope='Five same-native1920pairs; optional viewport hierarchy normalized because3skills fit without scroll. Actor/icon image identities, UV and dimensions unchanged. Visible common text full content/rect/font unchanged except global header text. Repeated hints and unnecessary3skill arrows removed. Empty Button labels follow intentional row-hit height change; not stationary hits. Five-skill full selected-hit and20block mask reachability audited separately. Not whole-game acceptance.')
    (OUT/'geometry_audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
    dest=OUT/'source';dest.mkdir(exist_ok=True);files=[]
    for rel in ['Assets/Scripts/Battle/PveBattleController.PartyChoices.cs','tools/RsiGame/validate_skill_inventory50.py','tools/RsiGame/audit_skill_scroll51.py']:
        p=ROOT/rel;t=dest/rel;t.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,t);files.append(dict(path=rel,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
    (OUT/'source_manifest.json').write_text(json.dumps(dict(files=files,base='attempt50/source_final',runtimeMvid='919e01d2-01bb-4033-b52b-cb33c0f2827a'),indent=2),encoding='utf8')
    print('51 same-native geometry and source archived')
if __name__=='__main__':main()
