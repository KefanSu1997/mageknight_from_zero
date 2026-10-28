"""Read-only timing and exposure audit of closed actual-A solo episodes."""
import argparse
import hashlib
import json
from collections import Counter
from pathlib import Path


def review(directory):
    directory = Path(directory)
    totals = Counter()
    statuses = Counter()
    evidence = []
    outcomes = []
    for path in sorted(directory.glob('episode_*/result.json')):
        raw = path.read_bytes()
        row = json.loads(raw)
        statuses[row['status']] += 1
        totals['decisions'] += row['decisions']
        for timing in row.get('timings', []):
            if timing['operation'] == 'policy':
                totals['policy_prepare_wall_allocated'] += timing['prepare_seconds']
                totals['policy_inference_wall_allocated'] += timing['inference_seconds']
            else:
                totals['native_request_seconds_sum'] += timing['seconds']
        if row.get('archive'):
            archive = Path(row['archive']['path'])
            data = archive.read_bytes()
            digest = hashlib.sha256(data).hexdigest()
            if digest != row['archive']['sha256']:
                raise ValueError(f'Archive SHA mismatch: {archive}')
            native = json.loads(data)['native']
            if row['status'] == 'complete':
                assert native['terminal'] and row['audit']['passed']
                last = native['records'][-1]['Transition']
                score = last['Score']
                outcomes.append(dict(task_id=row['task_id'],
                    decisions=row['decisions'], reward=row['return_scaled'],
                    score=score['Total'], fame=score['Inventory']['Fame'],
                    wounds=score['Inventory']['DeckWounds'], cities=score['Cities']))
            totals['archive_wall_seconds_sum'] += row['archive']['save_wall_seconds']
        evidence.append(dict(result=str(path), sha256=hashlib.sha256(raw).hexdigest()))
    collection = directory / 'collection.json'
    finished = json.loads(collection.read_text()) if collection.exists() else None
    policy_time = totals['policy_prepare_wall_allocated'] + totals['policy_inference_wall_allocated']
    return dict(directory=str(directory), statuses=dict(statuses), totals=dict(totals),
        collection_complete=finished is not None,
        collection_wall_seconds=finished['wall_seconds'] if finished else None,
        inference_fraction_of_measured_policy=(totals['policy_inference_wall_allocated']/policy_time
                                               if policy_time else None),
        outcomes=outcomes, evidence=evidence,
        scope=('Closed episodes only until collection_complete. Policy batch wall is allocated '
               'across choices, so summing a fully closed collection counts each batch once. '
               'Native request durations overlap and must not be added to policy wall as total. '
               'Partial closed subsets are completion-biased, not fixed evaluation results.'))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('collection')
    parser.add_argument('--out')
    args = parser.parse_args()
    result = review(args.collection)
    text = json.dumps(result, indent=2, allow_nan=False)
    if args.out:
        Path(args.out).write_text(text, encoding='utf-8')
    else:
        print(text)
