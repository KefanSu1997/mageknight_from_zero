"""Read-only tokenizer gate on real selected hierarchical nodes; no GPU."""
import os,sys,json,hashlib
from pathlib import Path
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
os.environ['BATTLE_RL_TEXT_ASSETS']=str(Path('H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets').resolve())
import hierarchical_option_policy as h
import green_option_descriptions as descriptions
from option_only_text import without_numeric_text
from joint_option_text import tokenizer,encode_document
descriptions.install();tok=tokenizer()
root=Path('remote_green_enhanced300/contexts');docs={}
for i,line in enumerate((root/'contexts.jsonl').open(),1):
    docs[i]=json.loads(line)
    if i==128:break
cases=[]
for line in (root/'selection_paths.jsonl').open():
    record=json.loads(line);context=record['context']
    if context not in docs:continue
    base=docs[context];new=without_numeric_text(base)
    assert all(new[k]==v for k,v in base.items() if k!='state')
    tree=h.ChoiceTree(base['operations'])
    for step in record['route']:
        olddoc=tree.document(base,step['node']);newdoc=tree.document(new,step['node'])
        assert olddoc['options']==newdoc['options'] and olddoc['rules']==newdoc['rules']
        assert 'state.' not in newdoc['state'] and '上一步完整操作特征及奖励' not in newdoc['state']
        oldids,oldpos=encode_document(tok,olddoc);newids,newpos=encode_document(tok,newdoc)
        assert len(oldpos)==len(newpos)==step['branch_count']
        cases.append(dict(context=context,node=step['node'],old_tokens=len(oldids),new_tokens=len(newids),options=len(newpos)))
    if len(cases)>=64:break
assert len(cases)>=64
report=dict(cases=cases,mean_old_tokens=sum(c['old_tokens'] for c in cases)/len(cases),mean_new_tokens=sum(c['new_tokens'] for c in cases)/len(cases),scope='Same actual legal options, rules and locked prefixes; raw numeric/history text removed only. Tokenizer gate, not CUDA speed or policy equivalence.')
Path('optimization_stage_20261014/option_only_lengths.json').write_text(json.dumps(report,indent=2));print(json.dumps({k:v for k,v in report.items() if k!='cases'}))
