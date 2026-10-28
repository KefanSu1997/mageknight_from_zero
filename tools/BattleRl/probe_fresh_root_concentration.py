"""Read-only recurrent root-choice audit on CPU using immutable cached vectors."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
import sys,io,json,sqlite3,hashlib,itertools,argparse
from pathlib import Path
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
import numpy as np
import torch
import train_sb3_atomic_entity_text as base
import hierarchical_option_policy as h
import hierarchical_packed_cache as packed
from conditioned_context_policy import ConditionedContextPolicy
from rollout_archive import load
parser=argparse.ArgumentParser();parser.add_argument('--root',required=True);parser.add_argument('--report',required=True);args=parser.parse_args();torch.set_num_threads(1);root=Path(args.root)
raw=(root/'checkpoint.zip').read_bytes();m=base.RecordedRecurrentPPO.load(io.BytesIO(raw),device='cpu');m.policy.set_training_mode(False)
db=sqlite3.connect('file:'+str(root/'contexts/packed_vectors/offsets.sqlite')+'?mode=ro',uri=True)
stream=(root/'contexts/packed_vectors/vectors.bin').open('rb')
def vectors(path,context,node):
 if not context:return np.zeros((2,384),np.float32)
 assert Path(path).resolve()==(root/'contexts').resolve()
 row=db.execute('SELECT metadata FROM entries WHERE context=? AND node=?',(int(context),int(node))).fetchone()
 if row is None:raise RuntimeError('Missing cache: encoder fallback forbidden')
 meta=json.loads(row[0]);stream.seek(meta['offset']);payload=stream.read(meta['bytes']);assert hashlib.sha256(payload).hexdigest()==meta['sha256']
 return np.frombuffer(payload,dtype='<f4').reshape(meta['shape']).copy()
h.node_vectors=vectors;packed.prefetch=lambda path,requests:None
original=h.HierarchicalDistribution.categorical;records={}
def observed(self,row,node):
 result=original(self,row,node);ctx=int(self.contexts[row]);t,_=h.tree(self.policy.context_path,ctx)
 if node==t.branch(0) and ctx not in records:
  v=torch.tensor(vectors(self.policy.context_path,ctx,node)[1:]);option=self.policy.option_head(v)
  query=self.policy.action_net(self.latent[row:row+1]);interaction=(query*option).sum(-1)/(128**.5);bias=self.policy.option_bias(option).flatten()
  keys=list(t.nodes[node]['children']);winner=int(result.probs.argmax())
  records[ctx]=dict(context=ctx,choices=len(keys),winner=keys[winner],max_probability=float(result.probs.max()),entropy=float(result.entropy()),interaction_span=float(interaction.max()-interaction.min()),option_bias_span=float(bias.max()-bias.min()))
 return result
h.HierarchicalDistribution.categorical=observed
archives=list(root.glob(f'rollout_{int(m.completed_updates)-1:04d}_*.archive'))
assert len(archives)==1,('Need exact completed checkpoint rollout',m.completed_updates,archives)
archive=archives[0];b=load(archive);b.device=torch.device('cpu');np.random.seed(7112026)
with torch.no_grad():
 for batch in itertools.islice(b.get(128),4):m.policy.evaluate_actions(batch.observations,batch.actions.long().flatten(),batch.lstm_states,batch.episode_starts)
rows=list(records.values());assert rows
report=dict(checkpoint_update=m.completed_updates,checkpoint_sha=hashlib.sha256(raw).hexdigest(),archive=str(archive),root_states=len(rows),fraction_root_max_above_99=float(np.mean([r['max_probability']>.99 for r in rows])),median_root_entropy=float(np.median([r['entropy'] for r in rows])),median_interaction_span=float(np.median([r['interaction_span'] for r in rows])),median_option_bias_span=float(np.median([r['option_bias_span'] for r in rows])),rows=rows,scope='Up to four saved recurrent minibatches with archived behavior LSTM states, cached vectors only, no optimizer/encoder. Descriptive post-update probe; not a full recurrent replay or fixed evaluation. Nontrivial first branch only; spans are descriptive and do not establish causal responsibility.')
dest=Path(args.report);dest.write_text(json.dumps(report,indent=2));print(json.dumps({k:v for k,v in report.items() if k!='rows'}))
stream.close();db.close()
