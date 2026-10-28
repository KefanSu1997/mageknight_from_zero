"""Same-native actor preservation and saved physical-index/pointer mapping."""
import json,hashlib,shutil
from pathlib import Path
from structured_ui_ppo import state
from validate_mana_inventory52 import inspect
from validate_battle_source53 import texts
from validate_entity_hover_reader import inside
ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'
OUT=BASE/'attempt53'
def read(p):return json.loads(p.read_text(encoding='utf8'))
def main():
    rows=[]
    for label in ['three_enemies','source_taken','selected_action_payment','source_consumed']:
        a=read(OUT/'before/key_phases'/label/'capture.json');b=read(OUT/'after_final/key_phases'/label/'capture.json')
        assert state(a['observation'])['audit.costNativeState']==state(b['observation'])['audit.costNativeState']
        ai={x['path']:x for x in a['evaluation']['rawImages']};bi={x['path']:x for x in b['evaluation']['rawImages']};assert ai.keys()==bi.keys()
        for p in ai:
            for k in ['texture','sourceWidth','sourceHeight','screenRect','uvRect','componentColor','shader','atmosphereSaturation','atmosphereTop','atmosphereGround']:assert ai[p][k]==bi[p][k],(label,p,k)
        at={x['path']:x for x in a['evaluation']['texts']};bt={x['path']:x for x in b['evaluation']['texts']}
        for p in at.keys()|bt.keys():
            if '/Mana' in p or '/PhaseTrack/' in p:continue
            assert p in at and p in bt,p
            for k in ['fullText','fontSize','screenRect']:
                if k=='screenRect' and '/ResolutionReceipt/' in p:continue
                assert at[p][k]==bt[p][k],(label,p,k)
        inspect(b);checked=texts(b)
        for t in b['evaluation']['texts']:
            if t['name'] in ['ManaColorIdentity','ManaHoverCue']:
                assert all(inside(g['screenRect'],dict(x=0,y=0,width=1920,height=1080),.1) for g in t['glyphs']),t['path']
        rows.append(dict(label=label,same_native=True,unchanged_raw_images=len(ai),new_labels=checked))
    pairs=[]
    for version in ['before','after_collision_fixed']:
        intent=read(OUT/version/'source_intent.json');events=read(OUT/version/'pointer_events.json')['events'];lookup={r['request_id']:r for r in events}
        mapped=[]
        for rid in intent['event_ids']:
            e=lookup[rid];n=json.loads(state(e['before'])['audit.costNativeState']);physical=None
            if e['name'].startswith('Btn_Source_'):
                index=int(e['name'].rsplit('_',1)[1]);physical=dict(index_at_click=index,color=n['auditSourceOrder'][index],ordered_faces=n['auditSourceOrder'],identity_scope='Actual ordered native source index, not a stable global die ID')
            mapped.append(dict(name=e['name'],path=e['path'],point=e['point_unity_bottom_left'],native_before_sha256=hashlib.sha256(state(e['before'])['audit.costNativeState'].encode()).hexdigest(),physical_die=physical,request=rid))
        pairs.append(dict(version=version,clicks=intent['clicks'],known_pointer_distance_pixels=intent['within_path_distance_pixels'],events=mapped))
    (OUT/'action_mapping_collision_fixed.json').write_text(json.dumps(dict(paths=pairs,scope='One actual public-mana→same enhanced card chain per version; real native ordered die closure and raycast points preserved. First pointer segment unknown. No global shortest path/human movement or all-transition claim.'),ensure_ascii=False,indent=2),encoding='utf8')
    result=dict(passed=True,pairs=rows,canvas=[1920,1080],declared_layout_changes='HUD grows80→110px to retain22pt six-color names and hover cue; phase indicator y82→112; receipt y132→166 with explicit phase/intent nonintersection gate in collision_repair/geometry.json. Actor/card size, UV and positions unchanged. Source selection drawer intentionally changes source hit positions; not stationary source controls.',scope='Four actual same-native closed-state pairs; complete current quantity glyphs and six-color labels. Selection/extra/Black→White/Gold native tests separately saved. Not all populations, six-die or all tactics.' )
    (OUT/'geometry_audit_collision_fixed.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
    dest=OUT/'source_collision_fixed';dest.mkdir(exist_ok=True);files=[]
    before=OUT/'source_before'
    paths=['Assets/Scripts/Battle/'+p.name for p in before.glob('*.cs')]+['Assets/Scripts/Battle/PveBattleController.BattleSource.cs','Assets/Scripts/Battle/PveBattleController.BattleSource.cs.meta','Assets/Scripts/SceneAutomation/StructuredUiPilot.Evaluation.cs','tools/RsiGame/validate_battle_source53.py','tools/RsiGame/audit_source53.py','tools/RsiGame/audit_source53_collision_fixed.py','tools/RsiGame/validate_source53_collision_repair.py']
    for rel in paths:
        p=ROOT/rel;t=dest/rel;t.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,t);files.append(dict(path=rel,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
    (OUT/'source_manifest_collision_fixed.json').write_text(json.dumps(dict(files=files,runtime=read(OUT/'collision_repair/compile_receipt.json')),indent=2),encoding='utf8')
    print('53 geometry, die→action→click mapping and source archived')
if __name__=='__main__':main()
