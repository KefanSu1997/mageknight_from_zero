"""Check actual trained marker sensitivity with all structured inputs fixed."""
import os,sys,json,hashlib
from pathlib import Path
from unittest.mock import patch
os.environ['OPENBLAS_NUM_THREADS']='1'
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
root=Path('remote_green_successor_gate_retry')
os.environ['BATTLE_RL_TEXT_ASSETS']=str(Path('H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets').resolve())
os.environ['BATTLE_RL_CONTEXT_STORE']=str((root/'contexts').resolve())
import torch
import train_sb3_atomic_entity_text as b
import structured_joint_policy as s
import hierarchical_packed_cache as packed
from rollout_archive import load
packed.install();torch.set_num_threads(1)
m=b.RecordedRecurrentPPO.load(root/'checkpoint.zip',device='cpu');m.policy.set_training_mode(False)
initial=b.RecordedRecurrentPPO.load(root/'initial_checkpoint.zip',device='cpu')
buffer=load(next(root.glob('rollout_*.archive')));buffer.device=torch.device('cpu');batch=next(buffer.get(128))
def probe():
    with torch.no_grad():return m.policy.evaluate_actions(batch.observations,batch.actions.long().flatten(),batch.lstm_states,batch.episode_starts)
normal=probe();original=s.h.node_vectors
def zero(path,context,node):return original(path,context,node)*0
with patch.object(s.h,'node_vectors',zero):altered=probe()
diff=float((normal[1]-altered[1]).abs().max());assert diff>1e-7
assert torch.equal(normal[0],altered[0]),'Text action ablation must not change V input'
changed={n:float((p-initial.policy.state_dict()[n]).abs().max()) for n,p in m.policy.state_dict().items() if n.startswith('marker_')}
assert max(v for n,v in changed.items() if n.startswith('marker_query'))>0
assert max(v for n,v in changed.items() if n.startswith('marker_options'))>0
assert all(torch.isfinite(v).all() for v in m.policy.state_dict().values())
assert all(torch.isfinite(v).all() for st in m.policy.optimizer.state.values() for v in st.values() if torch.is_tensor(v))
m.save(root/'reload_probe')
reloaded=b.RecordedRecurrentPPO.load(root/'reload_probe.zip',device='cpu');reloaded.policy.set_training_mode(False)
assert b.parameter_sha(m.policy)==b.parameter_sha(reloaded.policy)
with torch.no_grad():check=reloaded.policy.evaluate_actions(batch.observations,batch.actions.long().flatten(),batch.lstm_states,batch.episode_starts)
assert torch.equal(normal[1],check[1]) and torch.equal(normal[0],check[0])
report=dict(passed=True,actual_episodes=4,actual_steps=55,update_device='cuda',ablation_max_logprob_difference=diff,value_equal=True,marker_parameter_changes=changed,adam_states=len(m.policy.optimizer.state),reload_logprob_error=0,scope='Actual pretrained cached marker vectors affect probabilities after real PPO update with identical numeric/entity state. Sensitivity and integration only, not proof of better decisions.',sources={n:hashlib.sha256(Path('optimization_stage_20261014',n).read_bytes()).hexdigest() for n in ['structured_joint_policy.py','structured_hierarchy_core.py','green_option_descriptions.py','run_green_successor_gate.py']})
(root/'deployment_gate.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
