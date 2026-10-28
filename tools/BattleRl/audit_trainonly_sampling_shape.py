import os
os.environ['OPENBLAS_NUM_THREADS']='1';os.environ['TOKENIZERS_PARALLELISM']='false'
import sys,json,gc,collections
from pathlib import Path
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
import numpy as np
from rollout_archive import load
from hierarchical_option_policy import ChoiceTree
from joint_option_text import tokenizer,encode_document
root=Path('remote_repaired_trainonly300');groups={};owners={}
for a in sorted(root.glob('rollout_*.archive'))[:3]:
 b=load(a);ids=[int(x) for x in b.observations['context_id'].ravel()];groups[a.name]=ids
 for c in ids:owners[c]=a.name
 del b;gc.collect()
routes={}
with (root/'contexts/selection_paths.jsonl').open(encoding='utf-8') as f:
 for line in f:
  if not line.endswith('\n'):break
  d=json.loads(line)
  if d['context'] in owners:routes[d['context']]=d['route']
sampled={n:set(ids[::max(1,len(ids)//128)][:128]) for n,ids in groups.items()};stats={n:dict(contexts=len(ids),selected_branch_nodes=sum(len(routes.get(c,[])) for c in ids),text_chars=[],tokens=[]) for n,ids in groups.items()}
tok=tokenizer()
with (root/'contexts/contexts.jsonl').open(encoding='utf-8') as f:
 for c,line in enumerate(f,1):
  if c not in owners:continue
  d=json.loads(line);name=owners[c];stats[name]['text_chars'].append(len(d['state'])+len(str(d['rules'])))
  if c in sampled[name]:
   tree=ChoiceTree(d['operations']);node=tree.branch(0);doc=tree.document(d,node)
   if not doc['options']:doc['options']=['唯一完整操作，直接执行']
   ids,_=encode_document(tok,doc,8192);stats[name]['tokens'].append(len(ids))
for s in stats.values():
 for field in ['text_chars','tokens']:
  x=s.pop(field);s[field]=dict(count=len(x),mean=float(np.mean(x)),p50=float(np.median(x)),p95=float(np.percentile(x,95)),max=max(x))
report=dict(batches=stats,scope='All archived context/selected branch counts; 128 deterministic evenly spaced root token lengths per batch. CPU tokenizer only, no encoder/model/optimizer. Does not measure per-component wall time or prove a causal bottleneck.')
Path('optimization_stage_20261014/trainonly_sampling_shape.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
