"""Observe the final split-block route without clicking either block commit."""
import argparse,hashlib,json,shutil,sqlite3,uuid,zipfile
from pathlib import Path
from capture_ui_design_pairs import CaptureDemo
from measure_sampled_ui_pairs import game_difference
from iterate_ui_cost_design import cost
from structured_ui_ppo import ROOT,state,write_json

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--out',type=Path,required=True);out=parser.parse_args().out.resolve();out.mkdir(parents=True,exist_ok=False)
    db=sqlite3.connect('file:'+str(ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite').replace('\\','/')+'?mode=ro',uri=True)
    path,volume,digest,reset=db.execute('select path,volume,sha256,reset_json from episodes where id=5616').fetchone()
    with zipfile.ZipFile('H:/MageKnight_RL_Backups/20261012_before_reward_v2/'+volume) as z:raw=z.read(path)
    assert hashlib.sha256(raw).hexdigest()==digest;doc=json.loads(raw);trace=doc['trace'];task=json.loads(reset)['task'];root=[r['action'] for r in doc.get('curriculum_prefix',[])];demo=CaptureDemo(out/('live_'+uuid.uuid4().hex[:8]));results=[]
    def native(obs):return json.loads(state(obs)['audit.costNativeState'])
    def verify(expected,obs):assert not game_difference(expected,native(obs))
    def control(obs,name,path=None):
        choices=[c for c in obs['controls'] if c['name']==name and c['clickable'] and (not path or path in c['path'])];assert len(choices)==1;(c,)=choices;return c
    def point(c,intent):return dict(name=c['name'],path=c['path'],x=c['x'],y=c['y'],w=c['width'],h=c['height'],intent=intent,before='Battle.Hand',after='Battle.Hand')
    def snapshot(version,role):
        r=demo.call('observe',screenshot=True);dest=out/(version+'_'+role+'.png');shutil.copy2(r['screenshotPath'],dest);write_json(dest.with_suffix('.json'),dict(observation=r['observation'],screenshot_sha256=hashlib.sha256(dest.read_bytes()).hexdigest(),role='UI-only planning, block commit not clicked'));return r['observation']
    try:
        for version,opt in [('baseline',False),('candidate',True)]:
            steps=[]
            for intent,pos in [('A',26),('B',27)]:
                demo.reset_task(task);obs=demo.prepare_prefix(root+[r['action'] for r in trace[:pos]]);obs=demo.design(opt);verify(trace[pos]['before'],obs)
                c=control(obs,'Btn_Target','/Enemy_0/');steps.append(point(c,intent));obs=demo.call('click',obs,c)['observation'];verify(trace[pos]['before'],obs)
                if intent=='B':
                    assert state(obs)['attackSlot']=='0' and native(obs)['enemies'][0]['attacks'][0]['blocked']
                    for slot in [1,2]:
                        c=control(obs,'Btn_AttackSlot');steps.append(point(c,intent));obs=demo.call('click',obs,c)['observation'];verify(trace[pos]['before'],obs);assert state(obs)['attackSlot']==str(slot)
                    assert state(obs)['blocks.selected']=='1'
                obs=snapshot(version,intent+'_boundary');verify(trace[pos]['before'],obs);steps.append(point(control(obs,'Btn_Resolve'),intent))
                demo.reset_task(task);after=demo.prepare_prefix(root+[r['action'] for r in trace[:pos+1]]);verify(trace[pos]['after'],after)
            results.append(dict(key=['block→block',5616,26],version=version,population_weight=1.,source_sha256=digest,status='offline_planned',steps=steps,cost=cost(steps),rule_commits_clicked=0,archived_after_A_equal=True,archived_after_B_equal=True,equivalence='Observed already-blocked attack0 selection, two UI-only slot cycles to attack2, exactly one eligible split-block contribution. Both formal outcomes independently match archived states.'))
            write_json(out/'planned_routes.json',dict(results=results,scope='UI selections observed; neither block commit clicked. Formal outcomes restored independently.'))
    finally:demo.close()
    print(json.dumps(dict(planned_versions=len(results),rule_commits_clicked=0)))

if __name__=='__main__':main()
