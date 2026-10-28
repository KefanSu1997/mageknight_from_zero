"""Check review label coverage and exact inherited-image identity."""
import argparse,hashlib,json
from pathlib import Path
from structured_ui_ppo import write_json

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def labels(review):
    return set(review.get('actual_labels_inspected',review.get('inspected_labels',[])))
def main():
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);out=p.parse_args().out.resolve()
    manifest=json.loads((out/'paired_manifest.json').read_text(encoding='utf8'));pairs={x['label']:x for x in manifest['pairs']}
    inherited={k for k,v in pairs.items() if v.get('inherited_from')};covered=set();sources=[];errors=[];disputed=set()
    for f in sorted((out.parent/'v6_direct_actions').glob('review_*.json')):
        d=json.loads(f.read_text(encoding='utf8'));eligible=labels(d)&inherited;verified=[]
        for label in eligible:
            equal=all(sha(Path(pairs[label][v]))==sha(out.parent/'v6_direct_actions'/v/(label+'.png')) for v in ['baseline','candidate'])
            if equal:verified.append(label);covered.add(label)
            else:errors.append(dict(label=label,reason='Inherited review image bytes differ'))
        sources.append(dict(review=str(f),sha256=sha(f),verified_labels=sorted(verified),kind='historical_review_exact_images'))
    for f in sorted(out.glob('review_*.json')):
        if f.name=='review_inheritance.json':continue
        d=json.loads(f.read_text(encoding='utf8'))
        if not isinstance(d,dict):continue
        pending={r['label'] for r in d.get('pairs',[]) if r.get('verification_status','').startswith('pending')}
        disputed|=pending
        seen=(labels(d)&set(pairs))-pending;covered|=seen;sources.append(dict(review=str(f),sha256=sha(f),verified_labels=sorted(seen),pending_display_consistency_labels=sorted(pending),kind='current_pair_review'))
    result=dict(pairs=len(pairs),covered_pairs=len(covered),unreviewed_labels=sorted(set(pairs)-covered),disputed_display_labels=sorted(disputed),errors=errors,review_sources=sources,all_supplied_pairs_reviewed=not (set(pairs)-covered) and not errors,scope='Evidence that each supplied pair was reviewed with consistent image delivery, not proof that every reachable game interface has been captured or all review findings are resolved.',goal_complete=False)
    write_json(out/'visual_review_coverage.json',result)
    print(json.dumps({k:v for k,v in result.items() if k!='review_sources'}))
if __name__=='__main__':main()
