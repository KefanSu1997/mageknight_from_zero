"""Fixed, without-replacement sampling, <=20 original edges per group.

Build both Kind-pair and full argument/phase-pair plans. Hashed priorities
are uniform over source edges and are frozen before measurements/failures.
"""
import argparse
import hashlib
import json
from pathlib import Path
import sqlite3

ROOT=Path(__file__).resolve().parents[2]


def main():
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);p.add_argument('--cap',type=int,default=20)
    args=p.parse_args();assert 1<=args.cap<=20
    source=ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset'
    manifest=json.loads((source/'manifest.json').read_text(encoding='utf-8'))
    out=args.out.resolve();out.mkdir(parents=True,exist_ok=False)
    original=sqlite3.connect('file:'+str(source/'workset.sqlite').replace('\\','/')+'?mode=ro',uri=True)
    db=sqlite3.connect(out/'sampling.sqlite')
    db.executescript('''PRAGMA temp_store=FILE;
      CREATE TABLE candidates(episode INTEGER,a_step INTEGER,a TEXT,b TEXT,a_phase TEXT,b_phase TEXT,
       a_kind TEXT,b_kind TEXT,priority INTEGER,PRIMARY KEY(episode,a_step));''')
    rows=original.execute('''SELECT t.episode,t.a_step,t.a_action,t.b_action,t.a_end_phase,t.b_start_phase,
       a.kind,b.kind,e.sha256 FROM transitions t JOIN actions a ON a.id=t.a_action
       JOIN actions b ON b.id=t.b_action JOIN episodes e ON e.id=t.episode ORDER BY t.episode,t.a_step''')
    for i,row in enumerate(rows):
        payload=f"ui-cost-sample-v1:{manifest['corpus_sha256']}:{row[-1]}:{row[1]}".encode()
        priority=int.from_bytes(hashlib.blake2b(payload,digest_size=8).digest(),'big') & ((1<<63)-1)
        db.execute('INSERT INTO candidates VALUES(?,?,?,?,?,?,?,?,?)',(*row[:-1],priority))
        if i%20000==0:db.commit()
    db.commit()
    plans={}
    for name,group in [('kind_pair','a_kind,b_kind'),('argument_phase_pair','a,b,a_phase,b_phase')]:
        db.execute(f'''CREATE TABLE {name}_samples AS SELECT episode,a_step,a,b,a_phase,b_phase,a_kind,b_kind,priority,
          sample_rank,'pending' AS status FROM (SELECT *,row_number() OVER
          (PARTITION BY {group} ORDER BY priority,episode,a_step) AS sample_rank FROM candidates)
          WHERE sample_rank<=?''',(args.cap,))
        db.execute(f'CREATE INDEX {name}_episodes ON {name}_samples(episode,a_step)')
        count=db.execute(f'SELECT count(*) FROM {name}_samples').fetchone()[0]
        sizes=[r[0] for r in db.execute(f'SELECT count(*) FROM {name}_samples GROUP BY {group}')]
        assert max(sizes)<=args.cap
        plans[name]=dict(groups=len(sizes),samples=count,max_group_samples=max(sizes),
            episodes=db.execute(f'SELECT count(distinct episode) FROM {name}_samples').fetchone()[0])
    db.commit()
    result=dict(cap=args.cap,plans=plans,source_corpus_sha256=manifest['corpus_sha256'],
        sampling='uniform hashed priorities without replacement; group occurrences with <=cap all retained',
        failure_rule='failed selected samples stay in denominator; never resample until successful',
        low_count_rule='use all available occurrences; disclose n, not pad to twenty',
        measured_costs=0,grouping_selection='await user grouping reply; both frozen plans available')
    (out/'manifest.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(result,ensure_ascii=False))
    db.close();original.close()


if __name__=='__main__':main()
