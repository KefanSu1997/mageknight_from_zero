"""Selected Kind-pair plan: <=20 samples with phase/argument strata.

If <=20 full variants, cover each. Otherwise coarsen argument IDs into hash
buckets within phases. Each stratum is sampled uniformly; population weights
are retained so phase balancing does not inflate rare phases' overall cost.
"""
import argparse
import collections
import hashlib
import json
from pathlib import Path
import sqlite3

ROOT=Path(__file__).resolve().parents[2]


def main():
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);p.add_argument('--cap',type=int,default=20);args=p.parse_args()
    cap=args.cap
    if not 1<=cap<=20:raise ValueError('Sample cap must be 1..20')
    out=args.out.resolve();out.mkdir(parents=True,exist_ok=False)
    source=ROOT/'AutomationOutputs/UiFluency/20261012_sample20'
    db=sqlite3.connect(out/'selected.sqlite')
    db.execute('ATTACH DATABASE ? AS source',(str(source/'sampling.sqlite'),))
    db.execute('PRAGMA temp_store=FILE')
    db.execute('CREATE TEMP TABLE indexed_candidates AS SELECT * FROM source.candidates')
    db.execute('CREATE INDEX indexed_candidate_kinds ON indexed_candidates(a_kind,b_kind)')
    db.executescript('''CREATE TABLE strata(group_id TEXT,stratum_id TEXT,population INTEGER,
       allocated INTEGER,PRIMARY KEY(group_id,stratum_id));
       CREATE TABLE samples(group_id TEXT,stratum_id TEXT,episode INTEGER,a_step INTEGER,
       a TEXT,b TEXT,a_phase TEXT,b_phase TEXT,priority INTEGER,stratum_population INTEGER,
       stratum_sample_count INTEGER,estimator_weight REAL,status TEXT,
       PRIMARY KEY(group_id,episode,a_step));''')
    groups=db.execute('SELECT a_kind,b_kind,COUNT(*) FROM indexed_candidates GROUP BY a_kind,b_kind').fetchall()
    for ak,bk,n in groups:
        gid=ak+'→'+bk
        rows=db.execute('SELECT episode,a_step,a,b,a_phase,b_phase,priority FROM indexed_candidates WHERE a_kind=? AND b_kind=?',(ak,bk)).fetchall()
        variants={(r[2],r[3],r[4],r[5]) for r in rows}
        buckets=collections.defaultdict(list)
        if len(variants)<=cap:
            for r in rows:
                key=hashlib.sha256(json.dumps(r[2:6]).encode()).hexdigest();buckets[key].append(r)
        else:
            phases=collections.Counter((r[4],r[5]) for r in rows)
            if len(phases)>cap:
                # Coarse buckets preserve positive support for every original
                # variant, but cannot guarantee every phase is sampled.
                for r in rows:
                    h=int(hashlib.sha256(json.dumps(r[2:6]).encode()).hexdigest(),16)
                    buckets['coarse_'+str(h%cap)].append(r)
                slots=None
            else:slots={p:1 for p in phases}
            while slots is not None and sum(slots.values())<cap:
                p=max(phases,key=lambda x:(phases[x]/slots[x],x));slots[p]+=1
            for r in rows:
                if slots is None:break
                phase=(r[4],r[5]);h=int(hashlib.sha256((r[2]+r[3]).encode()).hexdigest(),16)
                key=json.dumps([*phase,h%slots[phase]]);buckets[key].append(r)
        allocated={k:1 for k in buckets}
        while sum(allocated.values())<min(cap,n):
            eligible=[k for k in buckets if allocated[k]<len(buckets[k])]
            if not eligible:break
            key=max(eligible,key=lambda k:(len(buckets[k])/allocated[k],k));allocated[key]+=1
        assert sum(allocated.values())<=cap
        for sid,items in buckets.items():
            k=allocated[sid];items.sort(key=lambda r:(r[6],r[0],r[1]))
            db.execute('INSERT INTO strata VALUES(?,?,?,?)',(gid,sid,len(items),k))
            for r in items[:k]:
                db.execute('INSERT INTO samples VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)',
                    (gid,sid,*r,len(items),k,len(items)/n/k,'pending'))
        db.commit()
    assert db.execute('SELECT MAX(n) FROM (SELECT count(*) n FROM samples GROUP BY group_id)').fetchone()[0]<=cap
    assert all(abs(s-1)<1e-10 for s, in db.execute('SELECT SUM(estimator_weight) FROM samples GROUP BY group_id'))
    result=dict(selected_grouping='kind_pair',user_confirmed=True,groups=len(groups),cap=cap,
        samples=db.execute('SELECT count(*) FROM samples').fetchone()[0],population_transitions=sum(x[2] for x in groups),
        method='cover full variants when <=cap; otherwise phase + parameter hash strata; if phase count exceeds cap, combined hash strata; frozen uniform priorities within strata',
        estimator='pair mean = sum(sample cost * estimator_weight); full mean additionally weights pairs by historical pair frequency',
        failure_rule='same selected samples retained; failures never replaced by convenient successes',
        phase_argument_coverage='preserves each phase when phase count <=cap; otherwise coarse buckets cannot guarantee each phase; cannot enumerate all parameters',
        measurements=0)
    (out/'manifest.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(result,ensure_ascii=False));db.close()


if __name__=='__main__':main()
