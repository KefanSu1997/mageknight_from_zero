"""Append actual training measurements and render dependency-free local curves."""
import csv
import hashlib
import json
from pathlib import Path


def record(output, summary, episodes):
    output = Path(output)
    lengths = [len(e['trace']) for e in episodes]
    repeats = 0
    for episode in episodes:
        seen = set()
        for step in episode['trace']:
            key = hashlib.sha256(json.dumps([step['before'], step['action']], sort_keys=True).encode()).digest()
            repeats += key in seen
            seen.add(key)
    row = dict(summary)
    row.update(batch_episodes=len(episodes), batch_steps=sum(lengths),
        mean_raw_return=sum(sum(s['reward'] for s in e['trace']) for e in episodes)/len(episodes),
        mean_training_return=sum(sum(s['training_reward'] for s in e['trace']) for e in episodes)/len(episodes),
        task_success_rate=summary['completed_declared_tasks']/len(episodes),
        mean_episode_steps=sum(lengths)/len(episodes),
        repeated_state_action_fraction=repeats/max(1,sum(lengths)),
        audit_errors=sum(len(e['report']['errors']) for e in episodes))
    with (output/'metrics.jsonl').open('a',encoding='utf-8') as stream:
        stream.write(json.dumps(row)+'\n')
    rows=[json.loads(line) for line in (output/'metrics.jsonl').read_text().splitlines()]
    with (output/'metrics.csv').open('w',newline='',encoding='utf-8') as stream:
        writer=csv.DictWriter(stream,fieldnames=list(row));writer.writeheader();writer.writerows(rows)
    panels=[]
    for key,label in [('task_success_rate','Training task success rate'),('mean_training_return','Mean training return'),('mean_raw_return','Mean raw return'),('mean_episode_steps','Mean episode steps'),('repeated_state_action_fraction','Exact state/action revisit fraction'),('updated_full_sequence_kl','Updated full-sequence KL'),('value_exp_var_before','Critic explained variance before PPO'),('value_exp_var_after','Critic explained variance after PPO'),('value_mse_after','Critic MSE after PPO'),('value_target_variance','GAE return target variance')]:
        samples=[(i,r.get(key)) for i,r in enumerate(rows) if r.get(key) is not None]
        if not samples:
            panels.append(f'<section><h3>{label}</h3><p>No recorded samples / undefined target variance.</p></section>');continue
        values=[v for _,v in samples];low=min(values);high=max(values);span=high-low or 1
        points=' '.join(f'{45+490*i/max(1,len(rows)-1):.2f},{155-120*(v-low)/span:.2f}' for i,v in samples)
        panels.append(f'<section><h3>{label}</h3><svg viewBox="0 0 580 185"><text x="0" y="25">{high:.4g}</text><text x="0" y="165">{low:.4g}</text><path d="M45 25V155H540" fill="none" stroke="#888"/><polyline points="{points}" fill="none" stroke="#1675ce" stroke-width="2"/><text x="45" y="180">Update 1</text><text x="440" y="180">Update {len(rows)}</text></svg></section>')
    (output/'curves.html').write_text('<!doctype html><meta charset="utf-8"><meta http-equiv="refresh" content="30"><title>PPO training curves</title><style>body{font:16px system-ui;margin:28px;background:#fafafa}main{display:grid;grid-template-columns:repeat(2,minmax(300px,1fr));gap:20px}section{background:white;padding:15px}svg{width:100%}text{font-size:12px}</style>'+f'<h1>{rows[-1].get('collection_workers',8)}-environment PPO: {len(rows)} / {rows[-1].get('target_updates',100)} updates</h1><p>Training mixture measurements, not independent validation. Exact revisits are a diagnostic, not a complete loop detector. Raw returns and shaped returns are separate.</p><main>'+''.join(panels)+'</main>',encoding='utf-8')
