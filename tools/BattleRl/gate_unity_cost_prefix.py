"""Validate actual Unity formal prefix replay against archived native snapshots."""
import argparse
import json
from pathlib import Path
import sqlite3
import zipfile
from gate_unity_cost_task_reset import CostDemo
from structured_ui_ppo import ROOT,state,write_json


def differences(expected,actual,path=''):
    errors=[]
    if isinstance(expected,dict):
        if not isinstance(actual,dict):return [dict(path=path,expected=expected,actual=actual)]
        for k,v in expected.items():errors+=differences(v,actual.get(k),path+'.'+k)
        optional_protocol={'world','sharedSkills','paymentSelection','auditSourceOrder','combatConversions','taskProgress'} if path=='' else set()
        for k in actual.keys()-expected.keys()-optional_protocol:
            errors.append(dict(path=path+'.'+k,expected='<absent>',actual=actual[k]))
    elif isinstance(expected,list):
        if not isinstance(actual,list) or len(expected)!=len(actual):return [dict(path=path,expected=expected,actual=actual)]
        for i,(a,b) in enumerate(zip(expected,actual)):errors+=differences(a,b,path+f'[{i}]')
    elif expected!=actual:errors.append(dict(path=path,expected=expected,actual=actual))
    return errors


def main():
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);args=p.parse_args()
    out=args.out.resolve();out.mkdir(parents=True,exist_ok=False)
    db=sqlite3.connect('file:'+str(ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite').replace('\\','/')+'?mode=ro',uri=True)
    rows=db.execute("SELECT id,path,volume,reset_json,prefix_json FROM episodes WHERE cohort LIKE '20261011_fixed5000_experiment/run_recovery2%' ORDER BY id LIMIT 3").fetchall();db.close()
    demo=CostDemo(out/'live');results=[]
    try:
        for eid,path,volume,reset,initial in rows:
            with zipfile.ZipFile('H:/MageKnight_RL_Backups/20261012_before_reward_v2/'+volume) as z:doc=json.loads(z.read(path))
            observation=demo.reset_task(json.loads(reset)['task'])
            expected=doc['curriculum_prefix'][0]['before'] if doc.get('curriculum_prefix') else doc['trace'][0]['before']
            actual=json.loads(state(observation)['audit.costNativeState'])
            error=differences(expected,actual)
            result=dict(episode=eid,path=path,step='root',errors=error,expected=expected,actual=actual)
            results.append(result);write_json(out/'results.json',dict(results=results))
            assert not error,error[:20]
            prefix=[x['action'] for x in doc.get('curriculum_prefix',[])]
            take=min(12,len(doc['trace'])-1)
            prefix += [x['action'] for x in doc['trace'][:take]]
            observation=demo.prepare_prefix(prefix)
            expected=doc['trace'][take]['before'];actual=json.loads(state(observation)['audit.costNativeState'])
            error=differences(expected,actual)
            results.append(dict(episode=eid,path=path,step=take,errors=error,expected=expected,actual=actual))
            write_json(out/'results.json',dict(results=results));assert not error,error[:20]
    finally:demo.close()
    print(json.dumps(dict(roots=len(rows),snapshots=len(results),passed=True)))


if __name__=='__main__':main()
