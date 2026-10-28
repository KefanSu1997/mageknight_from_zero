"""Exhaustive, disk-backed demand ledger for one frozen historical corpus.

Every rule step and within-episode transition has a source reference. No
unmeasured item is assigned a cost. This is input to complete UI replay, not
a cost matrix or an assertion that replay adapters already exist.
"""
import argparse
import collections
import hashlib
import json
from pathlib import Path
import sqlite3
import zipfile

ROOT=Path(__file__).resolve().parents[2]
BACKUP=Path('H:/MageKnight_RL_Backups/20261012_before_reward_v2')
FIELDS=('Kind','Card','Choice','Targets','Parts','Option','Input')


def encoded(value):return json.dumps(value,ensure_ascii=False,sort_keys=True,separators=(',',':'))
def sha(value):return hashlib.sha256(value).hexdigest()


def main():
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);args=p.parse_args()
    out=args.out.resolve();out.mkdir(parents=True,exist_ok=False)
    sources_path=ROOT/'AutomationOutputs/UiFluency/20261012_trajectory_priorities/sources.jsonl'
    sources=[json.loads(x) for x in sources_path.read_text(encoding='utf-8').splitlines()]
    expected=json.loads((sources_path.parent/'statistics.json').read_text(encoding='utf-8'))
    db=sqlite3.connect(out/'workset.sqlite')
    db.executescript('''
      PRAGMA journal_mode=DELETE;
      CREATE TABLE episodes(id INTEGER PRIMARY KEY,path TEXT UNIQUE,volume TEXT,sha256 TEXT,
        cohort TEXT,step_count INTEGER,task_id TEXT,reset_json TEXT,prefix_json TEXT,
        reset_kind TEXT,status TEXT DEFAULT 'pending_replay');
      CREATE TABLE actions(id TEXT PRIMARY KEY,kind TEXT,arguments_json TEXT,count INTEGER DEFAULT 0,
        first_episode INTEGER,first_step INTEGER,status TEXT DEFAULT 'unmapped');
      CREATE TABLE steps(episode INTEGER,ordinal INTEGER,action TEXT,before_sha TEXT,after_sha TEXT,
        phase TEXT,PRIMARY KEY(episode,ordinal));
      CREATE TABLE transitions(episode INTEGER,a_step INTEGER,a_action TEXT,b_action TEXT,
        a_end_phase TEXT,b_start_phase TEXT,PRIMARY KEY(episode,a_step));
      CREATE TABLE action_phases(action TEXT,phase TEXT,count INTEGER,
        PRIMARY KEY(action,phase));
      CREATE TABLE pair_variants(a TEXT,b TEXT,a_phase TEXT,b_phase TEXT,count INTEGER,
        first_episode INTEGER,first_step INTEGER,PRIMARY KEY(a,b,a_phase,b_phase));
    ''')
    z=None;volume=None;totals=collections.Counter();reset_counts=collections.Counter()
    try:
        for index,r in enumerate(sources):
            if r.get('local'):raw=(ROOT/r['path']).read_bytes()
            else:
                if volume!=r['volume']:
                    if z:z.close()
                    volume=r['volume'];z=zipfile.ZipFile(BACKUP/volume)
                raw=z.read(r['path'])
            assert sha(raw)==r['sha256'],r['path']
            doc=json.loads(raw);trace=doc['trace'];assert len(trace)==r['steps']
            certificate=doc.get('certificate',{})
            task=certificate.get('task',{}).get('definition')
            reset={'task':task} if task is not None else {'seed':doc.get('seed',certificate.get('task',{}).get('seed'))}
            reset_kind='declared_task' if task is not None else 'legacy_seed' if reset['seed'] is not None else 'missing_reset_metadata'
            reset_counts[reset_kind]+=1
            db.execute('INSERT INTO episodes(id,path,volume,sha256,cohort,step_count,task_id,reset_json,prefix_json,reset_kind) VALUES(?,?,?,?,?,?,?,?,?,?)',
                (index,r['path'],r.get('volume'),r['sha256'],r['cohort'],len(trace),doc.get('task_id',certificate.get('task_id')),
                 encoded(reset),encoded(doc.get('curriculum_prefix',[])),reset_kind))
            previous=None
            for i,t in enumerate(trace):
                a=t['action'];args_={k:a.get(k,'' if k in ('Card','Choice','Input') else 0) for k in FIELDS}
                assert args_['Kind'],r['path']
                data=encoded(args_);key=sha(data.encode())
                db.execute('INSERT INTO actions(id,kind,arguments_json,count,first_episode,first_step) VALUES(?,?,?,?,?,?) ON CONFLICT(id) DO UPDATE SET count=count+1',
                           (key,a['Kind'],data,1,index,i))
                phase=t['before']['phase'];end_phase=t['after']['phase']
                db.execute('INSERT INTO steps VALUES(?,?,?,?,?,?)',(index,i,key,sha(encoded(t['before']).encode()),sha(encoded(t['after']).encode()),phase))
                db.execute('INSERT INTO action_phases VALUES(?,?,1) ON CONFLICT(action,phase) DO UPDATE SET count=count+1',(key,phase))
                if previous:
                    pk,pe=previous
                    db.execute('INSERT INTO transitions VALUES(?,?,?,?,?,?)',(index,i-1,pk,key,pe,phase))
                    db.execute('INSERT INTO pair_variants VALUES(?,?,?,?,1,?,?) ON CONFLICT(a,b,a_phase,b_phase) DO UPDATE SET count=count+1',(pk,key,pe,phase,index,i-1))
                    totals['transitions']+=1
                previous=(key,end_phase)
                totals['steps']+=1
            totals['episodes']+=1
            if index%300==0:
                db.commit();print(json.dumps(dict(processed=index+1,total=len(sources),**totals)),flush=True)
        db.commit()
        actual=db.execute('SELECT kind,SUM(count),COUNT(*) FROM actions GROUP BY kind ORDER BY SUM(count) DESC').fetchall()
        assert {k:n for k,n,_ in actual}==expected['action_counts']
        assert totals['steps']==expected['steps'] and totals['episodes']==expected['episodes']
        assert totals['transitions']==totals['steps']-totals['episodes']
        counts={name:db.execute('SELECT COUNT(*) FROM '+name).fetchone()[0] for name in ('actions','action_phases','pair_variants')}
        db.execute('CREATE INDEX actions_kind ON actions(kind)');db.execute('CREATE INDEX steps_action ON steps(action)');db.commit()
        manifest=dict(scope='complete unique historical trace evidence; future current-UI projection requires native reset and parameter-faithful pointer adapters',
            corpus_sha256=sha(sources_path.read_bytes()),backup=str(BACKUP),totals=dict(totals),variants=counts,
            kind_count=len(actual),reset_types=dict(reset_counts),cost_export_ready=False,
            costs_measured_for_this_workset=0,
            reasons=['Current strict semantic_ui_actions only supports guarded advance/sideways/single-target attack.',
                'StructuredUiPilot can start a demo but cannot yet import all declared training tasks or legacy seed starts.',
                'Historical rules versions differ; current-UI projection must validate replay and preserve incompatible failures.',
                'Historical native states lack UI cursor/selection; sequential UI replay must reconstruct those, not guess them.'],
            cost_contract=dict(resolution=[1920,1080],clicks='actual successful pointer dispatches including navigation and confirmation',
                panels='actual modal/page transitions, not redraws',distance='successive click-centre Euclidean distance across whole replay; initial pointer specified by runner',
                transitions='within episode only; consistent native-commit boundary, no repeated navigation accounting',
                time='software input-to-frame/ready proxy, not fixed RPC sleeps or human thinking',
                validity='actual native effect and original action parameters match; reset/setup/prefix costs stored separately',
                export_gate='all requested episodes/steps/parameter/context requirements replayed and validated; missing mapper is not zero cost'),
            kinds=[dict(kind=k,occurrences=n,argument_variants=v,status='requires_complete_ui_adapter') for k,n,v in actual])
        (out/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
        print(json.dumps(manifest,ensure_ascii=False),flush=True)
    finally:
        if z:z.close()
        db.close()


if __name__=='__main__':main()
