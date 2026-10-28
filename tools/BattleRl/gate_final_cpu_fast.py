import os,sys,json,time,hashlib
from pathlib import Path
os.environ['OPENBLAS_NUM_THREADS']='1'
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
from final_cpu_fast import Codec,install
import final_token_runtime as rt
import final_token_option as option
from hierarchical_option_policy import ChoiceTree
root=Path('remote_final_semantic300');files=sorted((root/'episodes').glob('*.json'))[:128]
raw=[p.read_text() for p in files];docs=[json.loads(x) for x in raw]
codec=Codec()
for text,doc in zip(raw,docs):
    assert codec.loads(text)==doc
    assert json.loads(codec.dumps(doc,ensure_ascii=False))==doc
    assert codec.loads(json.dumps(doc))==doc
big={'x':2**127+123,'minus':-2**130,'f':1.2345678901234567,'unicode':'汉字/🙂'}
assert json.loads(codec.dumps(big))==big
timing={}
for name,c in [('old',json),('new',codec),('new_reverse',codec),('old_reverse',json)]:
    start=time.perf_counter()
    for text,doc in zip(raw,docs):c.loads(text);c.dumps(doc,ensure_ascii=False)
    timing[name]=time.perf_counter()-start
contexts=[]
with (root/'contexts/contexts.jsonl').open() as f:
    for i,line in enumerate(f):
        if i>=256:break
        contexts.append(json.loads(line))
before=[]
for doc in contexts:
    tree=ChoiceTree(doc['operations']);nodes=[i for i,n in enumerate(tree.nodes) if len(n['children'])>1]
    before.append((tree,doc,nodes,[rt.node_document(tree,doc,n) for n in nodes]))
restore=install();checked=0
for tree,doc,nodes,expected in before:
    for node,value in zip(nodes,expected):assert rt.node_document(tree,doc,node)==value;checked+=1
restore()
report=dict(passed=True,episode_files=len(files),full_documents_exact=True,big_integer_roundtrip=True,node_documents_exact=checked,codec_seconds=timing,source_sha=hashlib.sha256(Path('optimization_stage_20261014/final_cpu_fast.py').read_bytes()).hexdigest())
Path('optimization_stage_20261014/final_cpu_fast_gate.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
