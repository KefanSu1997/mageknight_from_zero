"""Restore a three-enemy state and capture both profiles, without gameplay clicks."""
import argparse,hashlib,json,shutil,sqlite3,uuid,zipfile
from pathlib import Path
from capture_ui_design_pairs import CaptureDemo
from cost_entity_identity import align
from gate_unity_cost_prefix import differences
from structured_ui_ppo import ROOT,state,write_json

def main():
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);a=p.parse_args();out=a.out.resolve();out.mkdir(parents=True,exist_ok=False)
    source=ROOT/'Assets/Scripts/Battle/PveBattleController.cs';source_sha=hashlib.sha256(source.read_bytes()).hexdigest()
    db=sqlite3.connect('file:'+str(ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite').replace('\\','/')+'?mode=ro',uri=True)
    path,volume,sha,reset=db.execute('select path,volume,sha256,reset_json from episodes where id=8519').fetchone()
    with zipfile.ZipFile('H:/MageKnight_RL_Backups/20261012_before_reward_v2/'+volume) as z:raw=z.read(path)
    assert hashlib.sha256(raw).hexdigest()==sha;doc=json.loads(raw);trace=doc['trace'];pos=55
    prefix=[r['action'] for r in doc.get('curriculum_prefix',[])]+[r['action'] for r in trace[:pos]]
    demo=CaptureDemo(out/('live_'+uuid.uuid4().hex[:8]));records={}
    try:
        for version,opt in [('baseline',False),('candidate',True)]:
            demo.reset_task(json.loads(reset)['task']);obs=demo.prepare_prefix(prefix);obs=demo.design(opt)
            response=demo.call('observe',screenshot=True);obs=response['observation'];s=state(obs);native=json.loads(s['audit.costNativeState'])
            mapped,_=align(trace[pos]['before'],native);assert not differences(trace[pos]['before'],mapped)
            dest=out/(version+'.png');shutil.copy2(response['screenshotPath'],dest)
            record=dict(version=version,episode=8519,step=pos,native_state=native,observation=obs,screenshot=str(dest),screenshot_sha256=hashlib.sha256(dest.read_bytes()).hexdigest(),source_at_capture_sha256=source_sha,hover_preview=s['ui.hoverPreviewVisible'],truncated=s.get('ui.truncated',''),rule_commits_clicked=0)
            write_json(out/(version+'.json'),record);records[version]=record
    finally:demo.close()
    x,y=records['baseline'],records['candidate'];mapped,_=align(x['native_state'],y['native_state']);assert not differences(x['native_state'],mapped)
    old={c['path']:c for c in x['observation']['controls']};dimensions=[]
    for c in y['observation']['controls']:
        if c['name'].startswith(('Btn_Target','Btn_Card_','Btn_Unit_')) and c['path'] in old:
            b=old[c['path']];assert c['width']==b['width'] and c['height']==b['height'];dimensions.append(c['path'])
    result=dict(same_native_state=True,matched_control_dimensions=len(dimensions),canvas=[1920,1080],baseline_truncated=x['truncated'],candidate_truncated=y['truncated'],candidate_no_hover=y['hover_preview']=='False',runtime_readability_pass=not y['truncated'],source_at_capture_sha256=source_sha,scope='Single restored three-enemy boundary, not all phases. Source SHA does not by itself prove Unity loaded the new source.')
    write_json(out/'validation.json',result);print(json.dumps(result))
    assert result['runtime_readability_pass'],'Candidate still reports clipped text; do not claim the source edit is loaded or fixed.'

if __name__=='__main__':main()
