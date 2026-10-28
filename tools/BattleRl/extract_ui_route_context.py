"""Join accepted route IDs to immutable atlas public states for geometry eligibility."""
import hashlib,json,sqlite3
from pathlib import Path
from iterate_ui_cost_design import ROOT
ATLAS=Path('H:/MageKnight_RL_Backups/UiFluency_future/20261013_offline_atlas1/atlas.sqlite')
OUT=ROOT/'AutomationOutputs/UiFluency/design_review/route_public_context.jsonl'
def main():
    db=sqlite3.connect('file:'+ATLAS.as_posix()+'?mode=ro',uri=True)
    rows=db.execute('select e.group_id,e.episode,e.a_step,e.ordinal,r.source_sha,n.public_context_json from edges e join routes r using(group_id,episode,a_step) join nodes n on n.id=e.before_node')
    contexts={}
    for group,episode,step,ordinal,sha,context in rows:
        state=json.loads(context)['public_state']
        contexts[(group,episode,step,ordinal)]=dict(source_sha=sha,unit_count=int(state.get('units.count','0')),unit_page=int(state.get('ui.unitArtCount','0'))>0,phase=state.get('phase'),unit_selected=state.get('ui.unit'),attack_slot=state.get('attackSlot'),units_allowed=state.get('combat.unitsAllowed'),source_faces=state.get('source.faces','').split(','),ordinary_used=state.get('source.ordinaryUsed'))
    matched=missing=mismatch=0
    with OUT.open('w',encoding='utf8') as output:
        for line in (ROOT/'AutomationOutputs/UiFluency/ui_iteration/original_routes.jsonl').open(encoding='utf8'):
            r=json.loads(line);values=[]
            for i,s in enumerate(r['steps']):
                v=contexts.get((*r['key'],i))
                if v and v['source_sha']!=r['source_sha']:v=None;mismatch+=1
                if v:matched+=1
                else:missing+=1
                values.append(v)
            output.write(json.dumps(dict(key=r['key'],steps=values),ensure_ascii=False)+'\n')
    result=dict(matched_steps=matched,missing_steps=missing,source_mismatch_steps=mismatch,context_sha256=hashlib.sha256(OUT.read_bytes()).hexdigest(),scope='Exact sample key, ordinal and accepted source SHA; missing metadata cannot authorize shortcut substitution')
    OUT.with_suffix('.manifest.json').write_text(json.dumps(result,indent=2),encoding='utf8');print(result)
if __name__=='__main__':main()
