"""Retain all selected-sample attempts; only validated routes get costs."""
import argparse
import hashlib
import json
from pathlib import Path
import sqlite3
from structured_ui_ppo import ROOT
from ui_cost_views import panel


def main():
    p=argparse.ArgumentParser();p.add_argument('results',type=Path);p.add_argument('--plan',type=Path,default=ROOT/'AutomationOutputs/UiFluency/20261012_sample5_selected');args=p.parse_args()
    raw=args.results.read_bytes();doc=json.loads(raw);sha=hashlib.sha256(raw).hexdigest()
    gate=json.loads((args.results.parent/'strict_effect_verification_final.json').read_text(encoding='utf-8'))
    assert gate['status']=='passed' and gate.get('result_sha256')==sha,'Verify the exact immutable result snapshot before ingestion'
    assert gate['native_effect_snapshots']==2*sum(r['status']=='complete' for r in doc['samples'])
    path=args.plan.resolve()/'selected.sqlite'
    db=sqlite3.connect(path)
    db.executescript('''CREATE TABLE IF NOT EXISTS attempts(group_id TEXT,episode INTEGER,a_step INTEGER,
       result_sha TEXT,source_path TEXT,status TEXT,error TEXT,PRIMARY KEY(group_id,episode,a_step,result_sha));
       CREATE TABLE IF NOT EXISTS costs(group_id TEXT,episode INTEGER,a_step INTEGER,
       result_sha TEXT,source_path TEXT,a_clicks INTEGER,b_clicks INTEGER,pair_clicks INTEGER,
       internal_a_px REAL,transition_px REAL,panels INTEGER,frame_ms REAL,
       PRIMARY KEY(group_id,episode,a_step));''')
    completed=0
    db.execute('''CREATE TABLE IF NOT EXISTS clickability_evidence(group_id TEXT,episode INTEGER,a_step INTEGER,
        result_sha TEXT,source_path TEXT,non_center_clicks INTEGER,PRIMARY KEY(group_id,episode,a_step))''')
    for r in doc['samples']:
        key=(r['group'],r['episode'],r['a_step'])
        assert db.execute('SELECT COUNT(*) FROM samples WHERE group_id=? AND episode=? AND a_step=?',key).fetchone()[0]==1
        db.execute('INSERT OR IGNORE INTO attempts VALUES(?,?,?,?,?,?,?)',(*key,sha,str(args.results.resolve()),r['status'],r.get('error')))
        if r['status']=='complete':
            assert r['pointer_steps'] and r['pair_clicks']==len(r['pointer_steps'])
            assert all(s['actual']['success'] and s['actual']['click']['hitObject'] for s in r['pointer_steps'])
            assert r['distance_internal_a_px']>=0 and r['distance_a_commit_to_b_commit_px']>=0
            db.execute('INSERT OR REPLACE INTO costs VALUES(?,?,?,?,?,?,?,?,?,?,?,?)',
                (*key,sha,str(args.results.resolve()),r['a_clicks'],r['b_clicks'],r['pair_clicks'],
                 r['distance_internal_a_px'],r['distance_a_commit_to_b_commit_px'],sum(panel(s['actual']['before'])!=panel(s['actual']['observation']) for s in r['pointer_steps']),r['b_first_frame_ms']))
            db.execute("UPDATE samples SET status='complete' WHERE group_id=? AND episode=? AND a_step=?",key);completed+=1
            non_center=sum(abs(s['actual']['click']['clickX']-s['actual'].get('controlCenterX',s['actual']['click']['clickX']))>.001
                or abs(s['actual']['click']['clickY']-s['actual'].get('controlCenterY',s['actual']['click']['clickY']))>.001 for s in r['pointer_steps'])
            db.execute('INSERT OR REPLACE INTO clickability_evidence VALUES(?,?,?,?,?,?)',(*key,sha,str(args.results.resolve()),non_center))
        else:
            db.execute("UPDATE samples SET status='needs_adapter' WHERE group_id=? AND episode=? AND a_step=? AND status!='complete'",key)
    db.commit();db.close();print(json.dumps(dict(attempts=len(doc['samples']),complete=completed,result_sha=sha)))


if __name__=='__main__':main()
