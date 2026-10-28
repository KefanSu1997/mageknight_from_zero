"""Read-only verification of a complete optimization after speed deployment."""
import io, json, hashlib, zipfile, math
from pathlib import Path
import torch

root = Path('remote_final_eval_resume300')
paths = [root/'checkpoint.zip', root/'continuation.pt', root/'metrics.jsonl']
before = {str(p): (p.stat().st_size, p.stat().st_mtime_ns) for p in paths}
row = json.loads(paths[2].read_text().splitlines()[-1])
assert row['update'] > 89
with zipfile.ZipFile(paths[0]) as z:
    weights = torch.load(io.BytesIO(z.read('policy.pth')), map_location='cpu', weights_only=True)
    optimizer = torch.load(io.BytesIO(z.read('policy.optimizer.pth')), map_location='cpu', weights_only=True)
state = torch.load(paths[1], map_location='cpu', weights_only=False)
assert state['completed_updates'] == row['update']
assert len(state['environments']) == 32 and all(s['closed_episode'] for s in state['environments'])
assert all(torch.isfinite(t).all() for t in weights.values())
assert len(optimizer['state']) == 65
assert all(torch.isfinite(t).all() for s in optimizer['state'].values() for t in s.values() if isinstance(t, torch.Tensor))
assert all(math.isfinite(row[k]) and row[k] > 0 for k in ('collection_seconds','optimization_seconds'))
assert row['value_after']['ev'] is None or math.isfinite(row['value_after']['ev'])
batch = [json.loads(x) for x in (root/'episode_batches.jsonl').read_text().splitlines() if json.loads(x)['update']==row['update']][0]
assert batch['accepted_episodes'] == 128 and sum(batch['lengths']) == batch['transitions']
assert before == {str(p): (p.stat().st_size, p.stat().st_mtime_ns) for p in paths}, 'Checkpoint changed during read; retry.'
result = dict(passed=True, update=row['update'], accepted_episodes=128,
              transitions=batch['transitions'], rejected_episodes=batch['rejected_episodes'],
              collection_seconds=row['collection_seconds'], optimization_seconds=row['optimization_seconds'],
              value_before=row['value_before'], value_after=row['value_after'],
              optimizer_states=65, finite_weights_optimizer=True, closed_native_workers=32,
              checkpoint_sha=hashlib.sha256(paths[0].read_bytes()).hexdigest())
(root/'parallel_formal_gate.json').write_text(json.dumps(result, indent=2))
print(json.dumps(result))

