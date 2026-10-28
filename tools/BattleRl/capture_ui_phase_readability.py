"""Capture restored representative phases without clicking gameplay actions."""
import argparse,hashlib,json,shutil,sqlite3,uuid,zipfile
from pathlib import Path
from capture_ui_design_pairs import CaptureDemo
from cost_entity_identity import align
from gate_unity_cost_prefix import differences
from structured_ui_ppo import ROOT,state,write_json

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--out',type=Path,required=True);parser.add_argument('--fixture',action='append',help='label:episode:step; replaces default fixtures');args=parser.parse_args();out=args.out.resolve();out.mkdir(parents=True,exist_ok=False)
    db=sqlite3.connect('file:'+str(ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite').replace('\\','/')+'?mode=ro',uri=True)
    demo=CaptureDemo(out/('live_'+uuid.uuid4().hex[:8]));pairs=[];errors=[]
    fixtures=[('ranged',1959,1),('block',14407,2),('assign_damage',6803,4),('melee_two_enemies',14617,5),('won_result',6803,17),('three_enemy_details',8519,55),('split_block_after_first',5616,27)]
    if args.fixture:
        fixtures=[(parts[0],int(parts[1]),int(parts[2])) for parts in (value.split(':') for value in args.fixture)]
    try:
        for label,eid,pos in fixtures:
            path,volume,digest,reset=db.execute('select path,volume,sha256,reset_json from episodes where id=?',(eid,)).fetchone()
            with zipfile.ZipFile('H:/MageKnight_RL_Backups/20261012_before_reward_v2/'+volume) as z:raw=z.read(path)
            assert hashlib.sha256(raw).hexdigest()==digest;doc=json.loads(raw);trace=doc['trace'];prefix=[x['action'] for x in doc.get('curriculum_prefix',[])]+[x['action'] for x in trace[:pos]];records={}
            for version,opt in [('baseline',False),('candidate',True)]:
                demo.reset_task(json.loads(reset)['task']);obs=demo.prepare_prefix(prefix);obs=demo.design(opt);response=demo.call('observe',screenshot=True);obs=response['observation'];s=state(obs);native=json.loads(s['audit.costNativeState'])
                mapped,_=align(trace[pos]['before'],native);assert not differences(trace[pos]['before'],mapped)
                assert s['ui.hoverPreviewVisible']=='False'
                destination=out/version/(label+'.png');destination.parent.mkdir(exist_ok=True);shutil.copy2(response['screenshotPath'],destination)
                rec=dict(label=label,version=version,context=dict(episode=eid,step=pos,source_sha256=digest,role='restored_phase_no_gameplay_clicks'),native_state=native,discard_slots=json.loads(s['audit.costFixture']).get('turnFlow',{}).get('discardSlots',[]),observation=obs,screenshot=str(destination),screenshot_sha256=hashlib.sha256(destination.read_bytes()).hexdigest(),rule_commits_clicked=0)
                write_json(destination.with_suffix('.json'),rec);records[version]=rec
                if version=='candidate' and s.get('ui.truncated'):errors.append(dict(label=label,truncated=s['ui.truncated']))
            x,y=records['baseline'],records['candidate'];mapped,_=align(x['native_state'],y['native_state']);assert not differences(x['native_state'],mapped)
            pairs.append(dict(label=label,context=x['context'],baseline=x['screenshot'],candidate=y['screenshot'],same_native_state=True,hover_preview=False))
            write_json(out/'paired_manifest.json',dict(canvas=[1920,1080],pairs=pairs,errors=errors))
    finally:demo.close()
    print(json.dumps(dict(pairs=len(pairs),candidate_truncation_errors=errors,rule_commits_clicked=0)))
    assert not errors,'Readability must be fixed without hiding the failed captures.'

if __name__=='__main__':main()
