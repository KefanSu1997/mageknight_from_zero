"""CPU interface/length gate; no pretrained inference or training claims."""
import os,sys,json
from pathlib import Path
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
os.environ['BATTLE_RL_TEXT_ASSETS']=str(Path('H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets').resolve())
import torch
import hierarchical_option_policy as h
import green_option_descriptions as descriptions
from joint_option_text import tokenizer
from final_token_option import semantic_document,encode_final_document,FinalTokenHeads
descriptions.install();tok=tokenizer();torch.set_num_threads(1)
root=Path('remote_green_enhanced300/contexts');docs={}
for i,line in enumerate((root/'contexts.jsonl').open(),1):
    docs[i]=json.loads(line)
    if i==128:break
cases=[]
for line in (root/'selection_paths.jsonl').open():
    row=json.loads(line)
    if row['context'] not in docs:continue
    original=docs[row['context']];base=semantic_document(original);tree=h.ChoiceTree(original['operations'])
    for step in row['route']:
        doc=tree.document(base,step['node']);ids,pos,k=encode_final_document(tok,doc)
        assert pos==len(ids)-1 and ids[pos]==tok.eos_token_id and k==step['branch_count']
        assert 'state.' not in doc['state']
        cases.append(dict(context=row['context'],node=step['node'],tokens=len(ids),options=k))
    if len(cases)>=64:break
head=FinalTokenHeads();text=torch.randn(4,384);structured=torch.randn(4,128);counts=torch.tensor([1,2,25,255])
logits=head.logits(text,structured,counts);p=logits.softmax(-1)
assert torch.allclose(p.sum(-1),torch.ones(4))
assert all(bool((p[i,int(n):]==0).all()) for i,n in enumerate(counts))
v=head.value(text,structured);assert v.shape==(4,)
loss=-logits.log_softmax(-1)[:,0].mean()+v.square().mean();loss.backward()
assert all(t.grad is not None and torch.isfinite(t.grad).all() for t in head.parameters())
report=dict(cases=cases,mean_tokens=sum(c['tokens'] for c in cases)/len(cases),max_options=max(c['options'] for c in cases),heads_finite_gradient=True,scope='Real archived node descriptions and tokenizer; synthetic hidden vectors for 255 logits/scalar V. No pretrained forward, semantic field audit, order invariance, PPO update or deployment proof.')
Path('optimization_stage_20261014/final_token_interface_gate.json').write_text(json.dumps(report,indent=2));print(json.dumps({k:v for k,v in report.items() if k!='cases'}))
