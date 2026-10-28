"""Compare identical fixed cases and verified pointer geometry across capture modes."""
import argparse
import hashlib
import json
from pathlib import Path

def main():
    p=argparse.ArgumentParser();p.add_argument('control',type=Path);p.add_argument('fast',type=Path);p.add_argument('--out',type=Path,required=True);a=p.parse_args()
    docs=[];hashes=[]
    for folder in (a.control,a.fast):
        raw=(folder/'results.json').read_bytes();doc=json.loads(raw);sha=hashlib.sha256(raw).hexdigest()
        gate=json.loads((folder/'strict_effect_verification_final.json').read_text(encoding='utf8'))
        assert gate['status']=='passed' and gate['result_sha256']==sha
        docs.append(doc);hashes.append(sha)
    def key(r):return r['group'],r['episode'],r['a_step']
    left={key(r):r for r in docs[0]['samples']};right={key(r):r for r in docs[1]['samples']}
    assert left.keys()==right.keys(),'Fixed sample IDs changed'
    errors=[];checked=0
    for k,x in left.items():
        y=right[k]
        if x['status']!=y['status'] or x['status']!='complete':errors.append(dict(case=k,error='Incomplete or different outcome'));continue
        for field in ('a_clicks','b_clicks','pair_clicks','panels','distance_internal_a_px','distance_a_commit_to_b_commit_px'):
            if abs(x[field]-y[field])>0.001:errors.append(dict(case=k,field=field,control=x[field],fast=y[field]))
        for u,v in zip(x['pointer_steps'],y['pointer_steps']):
            if u['button']!=v['button'] or u['actual']['click']['buttonPath']!=v['actual']['click']['buttonPath']:errors.append(dict(case=k,error='Different pointer route'))
            for field in ('clickX','clickY'):
                if abs(u['actual']['click'][field]-v['actual']['click'][field])>0.001:errors.append(dict(case=k,error='Different pointer geometry'))
            checked+=1
    before,after=(d['elapsed_seconds'] for d in docs)
    result=dict(status='passed' if not errors else 'failed',samples=len(left),checked_clicks=checked,
        control_seconds=before,fast_seconds=after,speedup=before/after,time_reduction_fraction=1-after/before,
        result_sha256=hashes,scope='same cases, pointer paths, clicks, geometry and independently verified native effects; one ordered timing trial, not a stable throughput guarantee',errors=errors)
    a.out.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8');print(json.dumps(result,ensure_ascii=False))
    if errors:raise SystemExit(1)

if __name__=='__main__':main()
