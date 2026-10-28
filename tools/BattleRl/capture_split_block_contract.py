"""Read-only restored boundary for the final split-block UI failure."""
import argparse,hashlib,json,sqlite3,uuid,zipfile
from pathlib import Path
from capture_ui_design_pairs import CaptureDemo
from measure_sampled_ui_pairs import game_difference
from structured_ui_ppo import ROOT,state,write_json

def main():
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);a=p.parse_args();out=a.out.resolve();out.mkdir(parents=True,exist_ok=False)
    db=sqlite3.connect('file:'+str(ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite').replace('\\','/')+'?mode=ro',uri=True)
    path,volume,sha,reset=db.execute('select path,volume,sha256,reset_json from episodes where id=5616').fetchone()
    with zipfile.ZipFile('H:/MageKnight_RL_Backups/20261012_before_reward_v2/'+volume) as z:raw=z.read(path)
    assert hashlib.sha256(raw).hexdigest()==sha;doc=json.loads(raw);t=doc['trace'];prefix=[r['action'] for r in doc.get('curriculum_prefix',[])]+[r['action'] for r in t[:27]]
    demo=CaptureDemo(out/('live_'+uuid.uuid4().hex[:8]));results=[]
    try:
        for version,opt in [('baseline',False),('candidate',True)]:
            demo.reset_task(json.loads(reset)['task']);obs=demo.prepare_prefix(prefix);assert not game_difference(t[27]['before'],json.loads(state(obs)['audit.costNativeState']));obs=demo.design(opt)
            r=demo.call('observe',screenshot=True);fixture=json.loads(state(r['observation'])['audit.costFixture'])
            result=dict(version=version,fixture=fixture,observation=r['observation'],screenshot=r['screenshotPath'],native_before_B_equal=True,source_sha256=sha,rule_commits_clicked=0,ui_clicks=0)
            results.append(result)
            target=next(c for c in r['observation']['controls'] if c['name']=='Btn_Target' and '/Enemy_0/' in c['path'] and c['clickable'])
            try:
                clicked=demo.call('click',r['observation'],target)
                result['target_selection_observation']=clicked['observation']
                result['selection_changes_native']=game_difference(t[27]['before'],json.loads(state(clicked['observation'])['audit.costNativeState']))
            except Exception as error:
                result['target_selection_error']=str(error)
            write_json(out/'contract.json',dict(results=results))
            demo.reset_task(json.loads(reset)['task']);obs=demo.prepare_prefix(prefix+[t[27]['action']]);assert not game_difference(t[27]['after'],json.loads(state(obs)['audit.costNativeState']));result['native_B_legal_and_equal']=True
        write_json(out/'contract.json',dict(results=results))
    finally:demo.close()
    for r in results:print(json.dumps(dict(version=r['version'],cards=r['fixture']['cardChoices'],units=r['fixture']['unitAbilities'],skills=r['fixture']['skillBindings']),ensure_ascii=False))
if __name__=='__main__':main()
