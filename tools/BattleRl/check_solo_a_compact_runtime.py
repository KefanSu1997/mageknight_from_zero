import sys,json,base64,copy,time
from pathlib import Path
import numpy as np
repo=Path(__file__).resolve().parents[2];sys.path.insert(0,str(repo/'tools/BattleRl'))
from solo_a_binary_context_candidate import decode
from solo_a_compact_runtime_candidate import CompactRuntime
from hierarchical_option_policy import ChoiceTree
corpus=Path(sys.argv[1]);out=Path(sys.argv[2]);out.mkdir(exist_ok=False)
contexts=[]
for row in json.loads(corpus.read_text()):
 d=decode(row['sha'],base64.b64decode(row['payload']))
 for k in ('descriptors','history_features'):d['support'][k]=np.asarray(d['support'][k],np.float32)
 contexts.append(d)
def reverse(v):
 if isinstance(v,dict):return {k:reverse(v[k]) for k in reversed(list(v))}
 if isinstance(v,list):return [reverse(x) for x in v]
 return v
def equal(a,b):
 if isinstance(a,np.ndarray):return isinstance(b,np.ndarray) and a.shape==b.shape and a.tobytes()==b.tobytes()
 if isinstance(a,dict):return set(a)==set(b) and all(equal(a[k],b[k]) for k in a)
 if isinstance(a,(list,tuple)):return len(a)==len(b) and all(equal(x,y) for x,y in zip(a,b))
 return a==b
binding=dict(gate='CPU_actual32_compact_runtime')
rt=CompactRuntime(out/'contexts',binding,cache_bytes=1024);expected=[]
for d in contexts:
 source=reverse(copy.deepcopy(d));cid=rt.add(source['support'],source['document'])
 expected.append(ChoiceTree(d['support']['operations']).nodes)
 assert rt.tree(cid)[0].nodes==expected[-1]
 assert equal(rt._read(cid),d)
 source['support']['operations'].clear();assert rt.tree(cid)[0].nodes==expected[-1]
rt.close();rt=CompactRuntime(out/'contexts',binding,cache_bytes=1024)
for i,d in enumerate(contexts,1):
 assert equal(rt._read(i),d) and rt.tree(i)[0].nodes==expected[i-1]
rt.close()
try:CompactRuntime(out/'contexts',dict(gate='wrong'))
except ValueError:pass
else:raise AssertionError('Binding guard')
result=dict(passed=True,contexts=len(contexts),cache_bytes=1024,reverse_insertion_order=True,caller_mutation=True,reopened=True,wrong_binding_rejected=True,scope='CPU runtime only; no CUDA PPO or full update timing')
(out/'result.json').write_text(json.dumps(result,indent=2));print(json.dumps(result))
