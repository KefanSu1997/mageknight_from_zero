import os,sys,json,hashlib
from pathlib import Path
os.environ.update(OMP_NUM_THREADS='1',OPENBLAS_NUM_THREADS='1',BATTLE_RL_CANDIDATE_ARM='dot')
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
r=Path('remote_candidate_dot_gate');os.environ['BATTLE_RL_TEXT_ASSETS']=str(Path('H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets').resolve());os.environ['BATTLE_RL_CONTEXT_STORE']=str((r/'contexts').resolve())
import torch,numpy as np
import train_sb3_atomic_entity_text as b
from candidate_option_runtime import runtime,OptionReader,node_document
from joint_option_text import encode_document
m=b.RecordedRecurrentPPO.load(r/'checkpoint.zip',device='cpu');rt=runtime(m.policy.context_path)
docs=[json.loads(x) for x in (r/'contexts/contexts.jsonl').read_text().splitlines()];context=next(i+1 for i,d in enumerate(docs) if 16<=len(d['operations'])<=256)
t,base=rt.tree(context);node=t.branch(0);doc=node_document(t,base,node)
reader=OptionReader()
def sha():
 h=hashlib.sha256()
 for p in reader.model.parameters():h.update(p.detach().cpu().contiguous().view(torch.uint8).numpy().tobytes())
 return h.hexdigest()
before=sha();reverse=dict(doc,options=list(reversed(doc['options'])));edit=dict(doc,rules='诊断替换：所有卡牌无法产生攻击、格挡或资源。')
a,z,e=reader.encode_many([doc,reverse,edit],batch_size=1)
with torch.no_grad():
 latent=torch.zeros(128);score=m.policy.option_residual(torch.tensor(a[1:]),latent);rev=m.policy.option_residual(torch.tensor(z[1:]),latent).flip(0);edited=m.policy.option_residual(torch.tensor(e[1:]),latent)
 delta=float((score-edited).abs().max());order=float((score-rev).abs().max())
assert delta>1e-7;assert before==sha();assert all(not p.requires_grad and p.grad is None for p in reader.model.parameters())
report=dict(passed=True,encoder_sha=before,encoder_frozen=True,real_rule_edit_score_delta=delta,actual_text_order_score_delta=order,options=len(doc['options']),tokens=len(encode_document(reader.tok,doc)[0]),scope='Text sensitivity and unchanged encoder; ordering measured, not assumed invariant; no policy superiority')
(r/'text_gate.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
