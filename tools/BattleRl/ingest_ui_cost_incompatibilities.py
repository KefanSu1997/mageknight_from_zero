"""Recheck paired native proof and fixed IDs before recording UI incompatibility."""
import argparse
import hashlib
import json
from pathlib import Path
import sqlite3
import zipfile
from measure_sampled_ui_pairs import game_difference
from structured_ui_ppo import ROOT,state

def main():
    p=argparse.ArgumentParser();p.add_argument('results',type=Path);p.add_argument('--plan',type=Path,default=ROOT/'AutomationOutputs/UiFluency/20261012_sample5_selected');args=p.parse_args()
    raw=args.results.read_bytes();doc=json.loads(raw);sha=hashlib.sha256(raw).hexdigest()
    pointer_raw=Path(doc['pointer_results_path']).read_bytes();pointer_sha=hashlib.sha256(pointer_raw).hexdigest()
    pointer={(r['group'],r['episode'],r['a_step']):r for r in json.loads(pointer_raw)['samples']}
    work=sqlite3.connect('file:'+str(ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite').replace('\\','/')+'?mode=ro',uri=True)
    db=sqlite3.connect(args.plan.resolve()/'selected.sqlite')
    db.execute('CREATE TABLE IF NOT EXISTS incompatibilities(group_id TEXT,episode INTEGER,a_step INTEGER,category TEXT,result_sha TEXT,source_path TEXT,PRIMARY KEY(group_id,episode,a_step))')
    checked=0
    for r in doc['cases']:
        if r['status']!='verified_incompatible':continue
        key=(r['group'],r['episode'],r['a_step']);assert pointer[key]['status']=='failed'
        assert r['completion_cost'] is None and r['pointer_failure_result_sha256']==pointer_sha
        path,volume=work.execute('SELECT path,volume FROM episodes WHERE id=?',(r['episode'],)).fetchone()
        with zipfile.ZipFile('H:/MageKnight_RL_Backups/20261012_before_reward_v2/'+volume) as archive:source=archive.read(path)
        assert hashlib.sha256(source).hexdigest()==r['source_sha256'];trace=json.loads(source)['trace'][r['failing_step']]
        for phase in ('before','after'):
            assert not game_difference(trace[phase],json.loads(state(r['native_'+phase])['audit.costNativeState']))
        assert db.execute('SELECT COUNT(*) FROM samples WHERE group_id=? AND episode=? AND a_step=?',key).fetchone()[0]==1
        db.execute('INSERT OR REPLACE INTO incompatibilities VALUES(?,?,?,?,?,?)',(*key,r['category'],sha,str(args.results.resolve())))
        db.execute("UPDATE samples SET status='verified_incompatible' WHERE group_id=? AND episode=? AND a_step=? AND status!='complete'",key)
        checked+=1
    db.commit();db.close();work.close();print(json.dumps(dict(independently_verified=checked,completion_cost=None,result_sha256=sha)))

if __name__=='__main__':main()
