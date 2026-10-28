"""Frozen full-denominator evaluation; never feeds samples or RNG into PPO."""
import hashlib
import json
import os
from pathlib import Path
import numpy as np
from coverage_tasks import combination, split
from task_feasibility import digest, file_hash
from original_data_signature import data_signature
from value_diagnostics import measure


def validate_evaluation(path, training, rules):
    document = json.loads(Path(path).read_text())
    rows = document['tasks']
    train_keys = {digest(combination(r['task']['definition'])) for r in training}
    keys = [digest(combination(r['task']['definition'])) for r in rows]
    if len(set(keys)) != len(keys) or set(keys) & train_keys:
        raise ValueError('Duplicate or overlapping evaluation composition')
    data = data_signature()
    for row in rows:
        task = row['task']
        if (split(task['definition']) != 'validation' or len(task['definition']['EnemyGroups']) != 1
            or task['rules_sha256'] != file_hash(rules.dll) or row['data_signature'] != data
            or task['catalog_sha256'] != digest(rules.actions) or task['feature_schema'] != rules.info['featureSchema']
            or row['task_id'] != digest(task)):
            raise ValueError('Stale or invalid evaluation task')
    return rows


def evaluate(policy, parallel, rows, output, collect, options, update, checkpoint, cache_source=None):
    output = Path(output)
    output.mkdir(exist_ok=False)
    def signature():
        h = hashlib.sha256()
        for key, value in policy.parameters().items():
            h.update(key.encode()); h.update(value.tobytes())
        return h.hexdigest()
    before = signature()
    rng = parallel.state()
    manifest = dict(update=update, checkpoint=str(checkpoint), checkpoint_sha256=file_hash(checkpoint),
        task_count=len(rows), actor_seed=9112026, per_task_rng=True, optimizer_updates=0,
        selection='all predeclared evaluation tasks, including unknown solvability')
    (output/'manifest.json').write_text(json.dumps(manifest, indent=2))
    cached = {}
    if cache_source is not None:
        source = Path(cache_source)
        old = json.loads((source/'manifest.json').read_text())
        for key in ('update','checkpoint_sha256','task_count','actor_seed','per_task_rng','optimizer_updates'):
            if old[key] != manifest[key]: raise ValueError('Cached evaluation binding changed: '+key)
        prior = json.loads((source.parent/'manifest.json').read_text())
        current = json.loads((output.parent/'manifest.json').read_text())
        for key in ('metadata','reward_mode','mixed_combat_start','planning_revisit_cost','training_step_cost'):
            if prior.get(key) != current.get(key): raise ValueError('Cached collection settings changed: '+key)
        provenance=[]
        for path in sorted((source/'rollouts').glob('episode_*.json')):
            doc=json.loads(path.read_text(encoding='utf-8')); index=doc['episode']
            if not 0 <= index < len(rows) or doc['certificate'] != rows[index] or doc['report']['errors']:
                raise ValueError('Invalid cached evaluation record')
            if index in cached: raise ValueError('Duplicate cached evaluation episode')
            cached[index]=path
            destination=output/'rollouts';destination.mkdir(exist_ok=True)
            for original in (path,path.with_suffix('.npz')):
                os.link(original,destination/original.name)
            provenance.append(dict(episode=index,json_sha256=file_hash(path),npz_sha256=file_hash(path.with_suffix('.npz'))))
        (output/'cache_provenance.json').write_text(json.dumps(dict(source=str(source),records=provenance)))
    records = []
    values, targets = [], []
    try:
        for offset in range(0, len(rows), 128):
            indices=range(offset,min(offset+128,len(rows)))
            jobs = [(i,rows[i]) for i in indices if i not in cached]
            fresh = parallel.collect(policy, jobs, collect, lambda _: options, output, task_seed=9112026) if jobs else []
            by_index=dict(zip((i for i,_ in jobs),fresh))
            episodes=[]
            for i in indices:
                if i not in cached:
                    episodes.append(by_index[i]);continue
                path=cached.pop(i)
                doc=json.loads(path.read_text(encoding='utf-8'))
                with np.load(path.with_suffix('.npz')) as data:
                    episodes.append(dict(trace=doc['trace'],rollout_values=data['rollout_values'].copy(),returns=data['returns'].copy()))
            for episode in episodes:
                records.append(dict(won=bool(episode['trace'][-1]['after'].get('taskProgress', {}).get('sequenceCompleted')),
                    reward=sum(t['training_reward'] for t in episode['trace']),
                    raw=sum(t['reward'] for t in episode['trace']), steps=len(episode['trace'])))
                values.append(episode['rollout_values']); targets.append(episode['returns'])
            del episodes
            (output/'progress.json').write_text(json.dumps(dict(completed=len(records), total=len(rows),
                wins=sum(r['won'] for r in records))))
    finally:
        for random, state in zip(parallel.random, rng):
            random.bit_generator.state = state
    assert signature() == before and parallel.state() == rng
    wins = sum(r['won'] for r in records)
    p = wins / len(records)
    z = 1.96; n = len(records)
    center = (p+z*z/(2*n))/(1+z*z/n)
    half = z*np.sqrt(p*(1-p)/n+z*z/(4*n*n))/(1+z*z/n)
    result = dict(update=update, episodes=n, wins=wins, win_rate=p, win_rate_wilson95=[center-half, center+half],
        mean_training_reward=float(np.mean([r['reward'] for r in records])),
        mean_raw_reward=float(np.mean([r['raw'] for r in records])),
        mean_steps=float(np.mean([r['steps'] for r in records])),
        critic=measure(np.concatenate(values), np.concatenate(targets)),
        frozen_parameters=True, training_rng_restored=True, unknown_failures_are_not_impossibility=True)
    (output/'summary.json').write_text(json.dumps(result, indent=2))
    with (output.parent/'evaluation_metrics.jsonl').open('a') as f:
        f.write(json.dumps(result)+'\n')
    history = [json.loads(line) for line in (output.parent/'evaluation_metrics.jsonl').read_text().splitlines()]
    panels = []
    for key, title in [('win_rate','Frozen evaluation win rate'), ('mean_training_reward','Frozen evaluation reward'), ('critic','Frozen evaluation value explained variance')]:
        points = [(r['update'], r[key]['explained_variance'] if key == 'critic' else r[key]) for r in history]
        points = [(x,y) for x,y in points if y is not None]
        if not points: continue
        low, high = min(y for _,y in points), max(y for _,y in points)
        span = max(high-low, .01)
        coordinates = [(30+6*x, 150-120*(y-low)/span) for x,y in points]
        line = ' '.join(f'{x:.2f},{y:.2f}' for x,y in coordinates)
        dots = ''.join(f'<circle cx="{x:.2f}" cy="{y:.2f}" r="4" fill="#269"/>' for x,y in coordinates)
        panels.append(f'<h2>{title}</h2><p>range {low:.4f} – {high:.4f}; x: PPO updates 0–100</p><svg viewBox="0 0 660 180"><polyline fill="none" stroke="#269" points="{line}"/>{dots}</svg>')
    table = ''.join(f"<tr><td>{r['update']}</td><td>{r['wins']}/{r['episodes']}</td><td>{r['mean_training_reward']:.3f}</td><td>{r['critic']['explained_variance']}</td></tr>" for r in history)
    (output.parent/'train_eval.html').write_text('<meta charset="utf-8"><meta http-equiv="refresh" content="60"><title>Fixed 5000 train/eval</title><style>body{font:16px sans-serif;max-width:1000px;margin:40px auto}svg{width:660px}td,th{padding:10px}</style><h1>4,000 train / 1,000 fixed evaluation tasks</h1><p>Evaluation is sampled, frozen, per-task seeded; unknown solvability remains in the denominator. Not a final test set. A baseline point alone shows no learning.</p><p><a href="curves.html">Training batch curves</a></p>'+''.join(panels)+'<table><tr><th>Update</th><th>Wins</th><th>Reward</th><th>Value EV</th></tr>'+table+'</table>',encoding='utf-8')
    return result
