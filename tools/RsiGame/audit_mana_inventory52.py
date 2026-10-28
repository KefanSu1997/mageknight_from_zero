"""Same-state resource HUD audit, including real generated quantity glyphs."""
import json,hashlib,shutil,argparse
from pathlib import Path
from structured_ui_ppo import state
from validate_mana_inventory52 import inspect
from validate_entity_hover_reader import intersect,inside
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1/attempt52'
def read(p):return json.loads(p.read_text(encoding='utf8'))
def mana(p):return '/ManaOrb_' in p or p.endswith('/ManaLegend') or '/ManaInventory_' in p or p.endswith('/ManaTokenLegend') or p.endswith('/ManaCrystalLegend')
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--validation',default='validation_quantity');parser.add_argument('--suffix',default='');parser.add_argument('--receipt',default='compile_quantity_receipt.json');args=parser.parse_args()
    rows=[]
    for label in ['three_enemies','source_panel','source_taken','selected_action_payment','source_consumed']:
        a=read(OUT/'before/key_phases'/label/'capture.json');b=read(OUT/args.validation/'key_phases'/label/'capture.json')
        assert state(a['observation'])['audit.costNativeState']==state(b['observation'])['audit.costNativeState']
        ta={t['path']:t for t in a['evaluation']['texts']};tb={t['path']:t for t in b['evaluation']['texts']}
        for p in ta.keys()|tb.keys():
            if mana(p):continue
            assert p in ta and p in tb,p
            for k in ['fullText','fontSize','screenRect','isTextOverflowing']:assert ta[p][k]==tb[p][k],(label,p,k)
        ai={t['path']:t for t in a['evaluation']['rawImages']};bi={t['path']:t for t in b['evaluation']['rawImages']};assert ai.keys()==bi.keys()
        for p in ai:
            for k in ['texture','sourceWidth','sourceHeight','screenRect','uvRect','componentColor','shader','atmosphereSaturation','atmosphereTop','atmosphereGround']:
                assert ai[p][k]==bi[p][k],(label,p,k)
        ac={x['path']:x for x in a['observation']['controls']};bc={x['path']:x for x in b['observation']['controls']};assert ac.keys()==bc.keys()
        for p in ac:
            for k in ['x','y','width','height','interactable','clickable']:assert ac[p][k]==bc[p][k],(label,p,k)
        values=inspect(b);glyphs=[]
        for t in tb.values():
            if t['name'] not in ['ManaTokenAmount','ManaCrystalAmount']:continue
            for g in t['glyphs']:
                assert inside(g['screenRect'],dict(x=0,y=0,width=1920,height=1080));glyphs.append((t['path'],g['screenRect']))
        for i,(p,g) in enumerate(glyphs):
            for q,h in glyphs[i+1:]:
                if p!=q:assert not intersect(g,h),(p,q)
        rows.append(dict(label=label,same_native=True,unchanged_native_controls=len(ac),unchanged_raw_images=len(ai),values=values,quantity_glyphs=len(glyphs)))
    all_records=[]
    for p in sorted((OUT/args.validation/'key_phases').glob('*/capture.json')):
        r=read(p)
        if any(t['name']=='ManaTokenLegend' for t in r['evaluation']['texts']):inspect(r);all_records.append(p.parent.name)
    result=dict(passed=True,paired=rows,all_resource_capture_labels=all_records,scope='Five actual same-native1920pairs: existing actor/card images, geometry/UV/tint and native button hit/availability unchanged; nonresource text content/size/rect unchanged.12 displayed quantity/status blocks source-match,26pt full glyphs/no overlap; all current typical captures with HUD checked. Gold/black zero crystals explicitly marked不可存; observed nonzero stock never suppressed. No human contrast/icon comprehension/full-game acceptance.')
    (OUT/('geometry'+args.suffix+'_audit.json')).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
    d=OUT/('source'+args.suffix+'_final');d.mkdir(exist_ok=True);files=[]
    for rel in ['Assets/Scripts/Battle/PveBattleController.ManaInventory.cs','Assets/Scripts/Battle/PveBattleController.EmbeddedBattle.cs','Assets/Scripts/SceneAutomation/StructuredUiPilot.Evaluation.cs','tools/RsiGame/validate_mana_inventory52.py','tools/RsiGame/audit_mana_inventory52.py']:
        p=ROOT/rel;t=d/rel;t.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,t);files.append(dict(path=rel,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
    (OUT/('source'+args.suffix+'_manifest.json')).write_text(json.dumps(dict(files=files,runtime=read(OUT/args.receipt)),indent=2),encoding='utf8')
    print('52 five exact pairs, all HUD quantities and source archived')
if __name__=='__main__':main()
