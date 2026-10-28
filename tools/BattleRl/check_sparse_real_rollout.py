"""Read-only equality gate using recorded recurrent histories and legal masks."""
import json
import time
from pathlib import Path
import torch
from train_sb3_recurrent import RecordedRecurrentPPO, parameter_sha
from sparse_battle_policy import SparseBattleRecurrentPolicy
from rollout_archive import load

torch.set_num_threads(1)
root=Path('H:/MageKnight_RL_Runs/20261012_reward_v2_nocache_100')
model=RecordedRecurrentPPO.load(root/'behavior_0018_step77824.zip',device='cpu')
buffer=load(root/'rollout_0018_step77824.archive');buffer.device=torch.device('cpu')
batch=next(buffer.get(8))
args=(batch.observations,batch.actions.flatten().long(),batch.lstm_states,batch.episode_starts)
before=parameter_sha(model.policy)
with torch.no_grad():dense=model.policy.evaluate_actions(*args)
model.policy.__class__=SparseBattleRecurrentPolicy
started=time.perf_counter()
with torch.no_grad():sparse=model.policy.evaluate_actions(*args)
for a,b in zip(dense,sparse):torch.testing.assert_close(a,b,atol=2e-5,rtol=2e-5)
assert before==parameter_sha(model.policy)
result=dict(padded_rows=len(batch.actions),real_rows=int(batch.mask.sum()),
    max_value_error=float((dense[0]-sparse[0]).abs().max()),
    max_logprob_error=float((dense[1]-sparse[1]).abs().max()),
    max_entropy_error=float((dense[2]-sparse[2]).abs().max()),
    sparse_cpu_seconds=time.perf_counter()-started,parameters_unchanged=True)
Path('H:/MageKnight_RL_Runs/sparse_real_gate.json').write_text(json.dumps(result,indent=2))
print(json.dumps(result))
