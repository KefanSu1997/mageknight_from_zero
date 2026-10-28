"""Inspect immutable, SHA-retrieved episodes; counts are not policy probabilities."""
import argparse
from collections import Counter
import hashlib
import json
import statistics
from pathlib import Path

def branch(command):
    fields=[k for k,v in command.items() if v is not None and k not in ('SchemaVersion','ExpectedStep')]
    if len(fields)!=1:raise ValueError('Expected one complete operation')
    key=fields[0]
    value=command[key]
    return key+(':'+str(value['Kind']) if isinstance(value,dict) and 'Kind' in value else '')

def review(root):
    episodes=[]
    for path in sorted(root.glob('episode_*/native.json')):
        result=path.with_name('result.json')
        if not result.exists():continue
        metadata=json.loads(result.read_text())
        if not metadata.get('eligible_complete_episode'):continue
        raw=path.read_bytes()
        digest=hashlib.sha256(raw).hexdigest()
        if digest!=metadata.get('archive',{}).get('sha256'):
            raise ValueError('Episode archive SHA differs: '+str(path))
        records=json.loads(raw)['native']['records']
        chosen=Counter();exposed=Counter();candidate_counts=Counter();end_fractions=[];examples=[]
        for record in records:
            options=[branch(json.loads(c)) for c in record['CommandsJson']]
            counts=Counter(options);selected=options[record['SelectedIndex']]
            chosen[selected]+=1;exposed.update(counts.keys());candidate_counts.update(counts)
            if counts.get('EndTurn') and len(counts)>1:
                fraction=counts['EndTurn']/len(options);end_fractions.append(fraction)
                if fraction>.5 and len(examples)<3:
                    examples.append(dict(step=record['BeforeStep'],counts=dict(counts),selected=selected))
        final=records[-1]['Transition']
        timings=metadata.get('timings',[])
        policy=[t for t in timings if t.get('operation')=='policy']
        episodes.append(dict(path=str(path),sha256=hashlib.sha256(raw).hexdigest(),
            task_id=metadata['task_id'],decisions=len(records),score=final['Score'],
            battles=final['CompletedBattles'],turns=final['CompletedTurns'],
            selected=dict(chosen),states_with_legal_branch=dict(exposed),candidate_counts=dict(candidate_counts),
            mean_endturn_candidate_fraction=sum(end_fractions)/len(end_fractions) if end_fractions else None,
            endturn_majority_examples=examples))
        episodes[-1]['timings']=dict(wall_seconds=metadata['wall_seconds'],
            prepare_seconds=sum(t.get('prepare_seconds',0) for t in policy),
            inference_seconds=sum(t.get('inference_seconds',0) for t in policy),
            native_task_seconds=sum(t.get('seconds',0) for t in timings if t.get('operation')!='policy'))
    return dict(episodes=episodes,complete=len(episodes),
        with_battle=sum(e['battles']>0 for e in episodes),
        with_fame=sum(e['score']['Inventory']['Fame']>0 for e in episodes),
        timing_summary=dict(
            decisions=sum(e['decisions'] for e in episodes),
            summed_prepare_seconds=sum(e['timings']['prepare_seconds'] for e in episodes),
            summed_inference_seconds=sum(e['timings']['inference_seconds'] for e in episodes),
            median_episode_wall=statistics.median(e['timings']['wall_seconds'] for e in episodes) if episodes else None,
            warning='Concurrent episode wall/native durations overlap; sums are not elapsed collection wall time'),
        scope='Completed downloaded training episodes only; candidate fractions are uniform-count proxies, not learned probabilities')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--root',required=True);parser.add_argument('--out',required=True)
    args=parser.parse_args();report=review(Path(args.root))
    Path(args.out).write_text(json.dumps(report,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in report.items() if k!='episodes'}))
    print(json.dumps([dict(task=e['task_id'],battles=e['battles'],fame=e['score']['Inventory']['Fame'],
                          endturn_fraction=e['mean_endturn_candidate_fraction'],selected=e['selected']) for e in report['episodes'][:3]]))
