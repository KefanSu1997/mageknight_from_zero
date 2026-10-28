import os,sys,json,argparse
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--arm',required=True);p.add_argument('--root',required=True);a=p.parse_args()
os.environ.update(OMP_NUM_THREADS='1',OPENBLAS_NUM_THREADS='1',BATTLE_RL_CANDIDATE_ARM=a.arm)
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
root=Path(a.root);os.environ['BATTLE_RL_CONTEXT_STORE']=str((root/'contexts').resolve());os.environ['BATTLE_RL_TEXT_ASSETS']=str(Path('H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets').resolve())
import torch,numpy as np
from unittest.mock import patch
import train_sb3_atomic_entity_text as b
from candidate_option_runtime import runtime
from rollout_archive import load
from structured_hierarchy_core import StructuredHierarchy
from atomic_action_registry import registry
from atomic_entity_text import EntityTextPolicy
from candidate_option_policy import CandidateDistribution
torch.set_num_threads(1)
assert json.loads((root/'complete.json').read_text())['updates']==2
m=b.RecordedRecurrentPPO.load(root/'checkpoint.zip',device='cpu');m.policy.set_training_mode(False)
initial=b.RecordedRecurrentPPO.load(root/'initial_checkpoint.zip',device='cpu')
buffer=load(sorted(root.glob('rollout_*.archive'))[-1]);buffer.device=torch.device('cpu');batch=next(buffer.get(128))
def probe(model):
 with torch.no_grad():return model.policy.evaluate_actions(batch.observations,batch.actions.long().flatten(),batch.lstm_states,batch.episode_starts)
normal=probe(m);rt=runtime(m.policy.context_path);orig=rt.vectors
with patch.object(rt,'vectors',lambda requests:[np.zeros_like(v) for v in orig(requests)]):altered=probe(m)
delta=float((normal[1]-altered[1]).abs().max());assert delta>1e-7,delta
changed={n:float((v-initial.policy.state_dict()[n]).abs().max()) for n,v in m.policy.state_dict().items() if n.startswith(('option_','argument_encoder'))}
assert all(any(v>0 for n,v in changed.items() if n.startswith(prefix)) for prefix in ['option_text','option_state','option_out','argument_encoder'])
assert all(torch.isfinite(v).all() for st in m.policy.optimizer.state.values() for v in st.values() if torch.is_tensor(v))
m.policy_kwargs.update(candidate_arm=a.arm,context_path=m.policy.context_path)
m.save(root/'reload_probe');other=b.RecordedRecurrentPPO.load(root/'reload_probe.zip',device='cpu');other.policy.set_training_mode(False);check=probe(other)
assert torch.equal(normal[0],check[0]) and torch.equal(normal[1],check[1]);assert b.parameter_sha(m.policy)==b.parameter_sha(other.policy)
# Synthetic finite logits over an actual legal tree: exact zero-residual factorization.
documents=[json.loads(line) for line in (root/'contexts/contexts.jsonl').read_text().splitlines()]
context=next(i+1 for i,d in enumerate(documents) if 16<=len(d['operations'])<=256);tree,doc=rt.tree(context)
assert tree.leaves>=16
x=torch.linspace(-2,2,tree.leaves,requires_grad=True);core=StructuredHierarchy(tree,x)
p=torch.stack([core.log_prob(i).exp() for i in range(tree.leaves)])
err=float((p-x.softmax(0)).abs().max());assert err<1e-5
# Same shared scorer follows vector reordering; encoder position effect is separate.
v=torch.randn(7,384);latent=torch.randn(128);perm=torch.tensor([6,1,4,0,2,5,3])
with torch.no_grad():reorder=float((m.policy.option_residual(v,latent)[perm]-m.policy.option_residual(v[perm],latent)).abs().max())
assert reorder<1e-6
report=dict(passed=True,arm=a.arm,actual_updates=2,metrics=[json.loads(x) for x in (root/'metrics.jsonl').read_text().splitlines()],text_ablation_logprob_delta=delta,changed=changed,adam_states=len(m.policy.optimizer.state),reload_logprob_error=0,reload_value_error=0,zero_residual_leaf_count=tree.leaves,zero_residual_probability_error=err,shared_scorer_reorder_error=reorder,sources=json.loads((root/'manifest.json').read_text())['candidate_sources'],scope='Integration and fixed-vector reorder only; actual text permutation, full encoder freeze SHA and resource gate pending')
(root/'integration_gate.json').write_text(json.dumps(report,indent=2));print(json.dumps({k:v for k,v in report.items() if k not in ['metrics','changed']}))
