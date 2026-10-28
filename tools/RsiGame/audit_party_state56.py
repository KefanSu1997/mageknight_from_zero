"""Same-state regression audit: symbols replace state prose, never shrink actors."""
import json,hashlib,shutil
from pathlib import Path
from structured_ui_ppo import state
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1/attempt56'
def read(p):return json.loads(p.read_text(encoding='utf8'))
def main():
    assert read(OUT/'after_final/party_feedback_manifest.json')['passed']
    assert read(OUT/'symbol_validation_verified/manifest.json')['passed']
    rows=[]
    for p in sorted((OUT/'before/key_phases').glob('*/capture.json')):
        a=read(p);b=read(OUT/'after_final/key_phases'/p.parent.name/'capture.json')
        assert state(a['observation'])['audit.costNativeState']==state(b['observation'])['audit.costNativeState'],p
        assert (b['observation']['screenWidth'],b['observation']['screenHeight'])==(1920,1080)
        ac={c['path']:c for c in a['observation']['controls']};bc={c['path']:c for c in b['observation']['controls']}
        assert ac.keys()==bc.keys()
        for k in ac:
            for f in ['x','y','width','height','clickable','interactable']:assert ac[k][f]==bc[k][f],(p,k,f)
        ai={i['path']:i for i in a['evaluation']['rawImages']};bi={i['path']:i for i in b['evaluation']['rawImages']}
        assert ai.keys()==bi.keys()
        for k in ai:
            for f in ['texture','screenRect','uvRect']:assert ai[k][f]==bi[k][f],(p,k,f)
        at={t['path']:t for t in a['evaluation']['texts']};bt={t['path']:t for t in b['evaluation']['texts']}
        # Toasts expire in wall-clock time; retain their actual captures, but
        # don't confuse expiry with removal of persistent information.
        transient=[k for k in at.keys()|bt.keys() if '/BattleFeedback/' in k and k.endswith('/BattleToast')]
        for k in transient:at.pop(k,None);bt.pop(k,None)
        removed=at.keys()-bt.keys();added=bt.keys()-at.keys()
        assert all(at[k]['name']=='Condition' for k in removed),removed
        assert all(bt[k]['name']=='WoundCount' for k in added),added
        for k in at.keys()&bt.keys():
            for f in ['fullText','fontSize','screenRect']:assert at[k][f]==bt[k][f],(p,k,f)
        for k in added:
            t=bt[k];assert t['fontSize']>=23 and t['hasVisibleGlyphs']
            assert {j for j,ch in enumerate(t['fullText']) if not ch.isspace()}.issubset({g['sourceIndex'] for g in t['glyphs']})
        rows.append(dict(label=p.parent.name,same_native=True,controls_unchanged=len(ac),art_geometry_uv_unchanged=len(ai),replaced_prose_rows=len(removed),real_wound_numerals=len(added),wall_clock_toast_excluded=transient))
    assert len(rows)==25,len(rows)
    (OUT/'geometry_audit.json').write_text(json.dumps(dict(passed=True,pairs=rows,scope='25actual same-state pairs, all control geometry/enabledness and actor/fullUV unchanged; only partyCondition prose replaced by semantic glyphs and23pt real numeric wounds. Other text content/font/position unchanged. No human recognition claim.'),ensure_ascii=False,indent=2),encoding='utf8')
    paths=[ROOT/'Assets/Scripts/Battle'/n for n in ['PveBattleController.EmbeddedBattle.cs','PveBattleController.BattlefieldReadability.cs']]
    paths += [ROOT/'Assets/Scripts/SceneAutomation/StructuredUiPilot.Evaluation.cs',Path(__file__),ROOT/'tools/RsiGame/validate_party_state_symbols56.py']
    dest=OUT/'source_final';dest.mkdir(exist_ok=True);files=[]
    manifest=OUT/'source_manifest.json'
    if not manifest.exists():
        for p in paths:shutil.copy2(p,dest/p.name);files.append(dict(path=str(p),sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
        manifest.write_text(json.dumps(dict(compile=read(OUT/'compile_final.json'),files=files),indent=2),encoding='utf8')
    else:
        for row in read(manifest)['files']:
            saved=dest/Path(row['path']).name
            assert hashlib.sha256(saved.read_bytes()).hexdigest()==row['sha256'],saved
    print('25same-native pairs: controls, art geometry/UV and remaining texts exact; wound numerals actual')
if __name__=='__main__':main()
