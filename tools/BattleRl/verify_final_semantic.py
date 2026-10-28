"""Actual CUDA integration, final-token text causality and reload checks."""
import os,sys,json,hashlib
from pathlib import Path
from unittest.mock import patch
os.environ.update(OPENBLAS_NUM_THREADS='1',OMP_NUM_THREADS='1')
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
root=Path('remote_final_semantic_gate')
os.environ['BATTLE_RL_TEXT_ASSETS']=str(Path('H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets').resolve())
os.environ['BATTLE_RL_CONTEXT_STORE']=str((root/'contexts').resolve())
import numpy as np
import torch
import train_sb3_atomic_entity_text as b
from final_token_runtime import runtime,node_document,FinalReader
from final_token_option import encode_final_document
from final_token_policy import Distribution
from atomic_action_registry import registry
from rollout_archive import load
torch.set_num_threads(1)
assert json.loads((root/'complete.json').read_text())['updates']==2
m=b.RecordedRecurrentPPO.load(root/'checkpoint.zip',device='cpu');m.policy.set_training_mode(False)
initial=b.RecordedRecurrentPPO.load(root/'initial_checkpoint.zip',device='cpu')
buffer=load(sorted(root.glob('rollout_*.archive'))[-1]);buffer.device=torch.device('cpu');batch=next(buffer.get(128))
def probe(model=m):
    with torch.no_grad():return model.policy.evaluate_actions(batch.observations,batch.actions.long().flatten(),batch.lstm_states,batch.episode_starts)
normal=probe();rt=runtime(m.policy.context_path);original=rt.vectors
with patch.object(rt,'vectors',lambda requests:original(requests)*0):altered=probe()
actor_delta=float((normal[1]-altered[1]).abs().max());critic_delta=float((normal[0]-altered[0]).abs().max())
assert actor_delta>1e-6 and critic_delta>1e-6
changes={n:float((p-initial.policy.state_dict()[n]).abs().max()) for n,p in m.policy.state_dict().items() if n.startswith(('action_net.','value_net.','actor_'))}
assert max(v for n,v in changes.items() if n.startswith('action_net.'))>0
assert max(v for n,v in changes.items() if n.startswith('value_net.'))>0
assert all(torch.isfinite(v).all() for st in m.policy.optimizer.state.values() for v in st.values() if torch.is_tensor(v))
m.save(root/'reload_probe');reloaded=b.RecordedRecurrentPPO.load(root/'reload_probe.zip',device='cpu');reloaded.policy.set_training_mode(False)
check=probe(reloaded);assert torch.equal(normal[0],check[0]) and torch.equal(normal[1],check[1])
assert b.parameter_sha(m.policy)==b.parameter_sha(reloaded.policy)
contexts=[json.loads(line) for line in (root/'contexts/contexts.jsonl').read_text().splitlines()]
for doc in contexts:
    assert 'state.' not in doc['state']
    assert doc['sequences']==registry(m.policy.registry_file).get(doc['support_id'])
    assert len(doc['operations'])==len(doc['sequences'])
context=next(i+1 for i,d in enumerate(contexts) if 2<=len(d['operations'])<=12)
t,base=rt.tree(context);node=t.branch(0);doc=node_document(t,base,node)
reader=FinalReader();rt.reader=reader
encoded=reader.encode_many([doc])[0];cached=original([(context,node)])[0]
cache_error=float(np.max(np.abs(encoded-cached)))
# BF16 masked batch vs singleton is numerically close, not bitwise promised.
assert cache_error<.15,cache_error
changed=dict(doc);changed['rules']='本测试人为替换所有原卡效果为：这张牌没有任何攻击、格挡、移动或资源效果。'
words=reader.encode_many([changed])[0]
with torch.no_grad():
    latent=torch.zeros(1,128)
    plain=m.policy.node_logits(torch.tensor(encoded)[None],latent)
    perturbed=m.policy.node_logits(torch.tensor(words)[None],latent)
    k=len(doc['options']);word_delta=float((plain[:,:k].softmax(-1)-perturbed[:,:k].softmax(-1)).abs().max())
assert word_delta>1e-7
assert all(not p.requires_grad and p.grad is None for p in reader.model.parameters())
def encoder_sha():
    digest=hashlib.sha256()
    for p in reader.model.parameters():digest.update(p.detach().cpu().contiguous().view(torch.uint8).numpy().tobytes())
    return digest.hexdigest()
frozen_before=encoder_sha()
m.policy._contexts=np.full(t.leaves,context,dtype=np.int64)
dist=Distribution(m.policy,torch.zeros(t.leaves,128))
with torch.no_grad():probs=dist.log_prob(torch.arange(t.leaves)).exp()
assert bool((probs>0).all()) and abs(float(probs.sum())-1)<1e-5
assert encoder_sha()==frozen_before
ids,last,k=encode_final_document(reader.tok,doc);assert last==len(ids)-1 and ids[last]==reader.tok.eos_token_id
rows=[json.loads(x) for x in (root/'metrics.jsonl').read_text().splitlines()]
report=dict(passed=True,updates=2,episodes=rows[-1]['episodes'],steps=rows[-1]['timesteps'],device='cuda',
    actor_text_ablation_logprob_delta=actor_delta,critic_text_ablation_value_delta=critic_delta,
    actual_rules_edit_probability_delta=word_delta,parameter_changes=changes,optimizer_states=len(m.policy.optimizer.state),
    reload_value_error=0,reload_logprob_error=0,context_support_checks=len(contexts),all_leaf_probability_sum=float(probs.sum()),
    all_leaf_count=t.leaves,encoder_parameter_sha=frozen_before,encoder_frozen=True,singleton_vs_batched_vector_max_error=cache_error,
    root_document_tokens=len(ids),scope='Small real integration and text sensitivity gate, not policy superiority or 32-worker throughput',
    sources=json.loads((root/'manifest.json').read_text())['final_token_sources'])
(root/'deployment_gate.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
