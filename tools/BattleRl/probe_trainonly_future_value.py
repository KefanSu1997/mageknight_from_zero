"""Frozen s20/current pair on subsequent complete training episodes, CPU only."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
import sys,io,json,hashlib,sqlite3,argparse
from pathlib import Path
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
import numpy as np
import torch
import train_sb3_atomic_entity_text as base
import hierarchical_option_policy as h
import hierarchical_packed_cache as packed
from root_entropy_conditioned_policy import RootEntropyConditionedPolicy
from rollout_archive import load
torch.set_num_threads(1)
folder=Path('optimization_stage_20261014/trainonly_value_pair')
binding=json.loads((folder/'binding.json').read_text());root=Path(binding['run'])
parser=argparse.ArgumentParser();parser.add_argument('--archive-update',type=int);parser.add_argument('--diagnose-lstm',action='store_true');args=parser.parse_args()
update=binding['future_archive_update'] if args.archive_update is None else args.archive_update
assert update>=binding['future_archive_update']
if args.diagnose_lstm:assert update==binding['future_archive_update']
matches=list(root.glob(f'rollout_{update:04d}_*.archive'))
if not matches:raise SystemExit('Waiting for complete subsequent rollout; do not substitute a trained batch')
assert len(matches)==1
archive=matches[0];buffer=load(archive);models=[]
for row in binding['checkpoints']:
 raw=Path(row['path']).read_bytes();assert hashlib.sha256(raw).hexdigest()==row['sha']
 model=base.RecordedRecurrentPPO.load(io.BytesIO(raw),device='cpu');model.policy.set_training_mode(False)
 assert model.completed_updates==row['update']<=update
 models.append(model)
db=sqlite3.connect('file:'+str(root/'contexts/packed_vectors/offsets.sqlite')+'?mode=ro',uri=True)
stream=(root/'contexts/packed_vectors/vectors.bin').open('rb')
def vectors(path,context,node):
 if not context:return np.zeros((2,384),np.float32)
 assert Path(path).resolve()==(root/'contexts').resolve()
 found=db.execute('SELECT metadata FROM entries WHERE context=? AND node=?',(int(context),int(node))).fetchone();assert found
 meta=json.loads(found[0]);stream.seek(meta['offset']);raw=stream.read(meta['bytes'])
 assert hashlib.sha256(raw).hexdigest()==meta['sha256']
 return np.frombuffer(raw,dtype='<f4').reshape(meta['shape']).copy()
h.node_vectors=vectors;packed.prefetch=lambda path,requests:None
targets=buffer.returns.reshape(-1);starts=np.flatnonzero(buffer.episode_starts.reshape(-1))
assert len(starts)==128 and starts[0]==0
ends=np.r_[starts[1:],len(targets)];predictions=[[],[]]
with torch.no_grad():
 for start,end in zip(starts,ends):
  obs={k:torch.tensor(v.reshape((-1,)+models[0].observation_space.spaces[k].shape)[start:end]) for k,v in buffer.observations.items()}
  flags=torch.zeros(end-start);flags[0]=1
  for i,model in enumerate(models):
   zero=torch.zeros(model.policy.lstm_critic.num_layers,1,model.policy.lstm_critic.hidden_size)
   values=model.policy.predict_values(obs,(zero.clone(),zero.clone()),flags).flatten().numpy()
   assert np.isfinite(values).all();predictions[i].append(values)
def score(values):
 error=targets-values;var=float(np.var(targets))
 return dict(ev=1-float(np.var(error))/var if var else None,mse=float(np.mean(error**2)),bias=float(np.mean(values-targets)))
predictions=[np.concatenate(p) for p in predictions]
error=float(np.max(np.abs(predictions[1]-buffer.values.reshape(-1)))) if update==binding['future_archive_update'] else None
report=dict(binding=binding,archive=str(archive),episodes=128,samples=len(targets),scores=[score(p) for p in predictions],archived_scores=score(buffer.values.reshape(-1)),replay_max_error=error,replay_tolerance=1e-4,replay_passed=error<1e-4 if error is not None else None,scope='Same new training episodes unseen by either optimizer; full LSTM replay from episode zero. Not fixed evaluation or task-heldout policy measurement. Replay equality applies only when candidate equals collector weights.')
diagnose=args.diagnose_lstm
if diagnose:
 model=models[1];hidden=buffer.hidden_states_vf;cells=buffer.cell_states_vf
 assert hidden.shape==cells.shape==(len(targets),model.policy.lstm_critic.num_layers,1,model.policy.lstm_critic.hidden_size)
 chunks=[]
 with torch.no_grad():
  for start in range(0,len(targets),128):
   end=min(start+128,len(targets))
   obs={k:torch.tensor(v.reshape((-1,)+model.observation_space.spaces[k].shape)[start:end]) for k,v in buffer.observations.items()}
   state=tuple(torch.tensor(x[start:end,:,0,:].transpose(1,0,2).copy()) for x in (hidden,cells))
   flags=torch.tensor(buffer.episode_starts.reshape(-1)[start:end])
   chunks.append(model.policy.predict_values(obs,state,flags).flatten().numpy())
 pred=np.concatenate(chunks)
 report['stored_lstm_diagnosis']=dict(max_error=float(np.max(np.abs(pred-buffer.values.reshape(-1)))),scores=score(pred),full_vs_stored_max_error=float(np.max(np.abs(pred-predictions[1]))))
name='future_value_lstm_diagnosis.json' if diagnose else 'future_value_report.json'
if update!=binding['future_archive_update']:name=f'future_value_on_{update:04d}.json'
(folder/name).write_text(json.dumps(report,indent=2));print(json.dumps(report))
stream.close();db.close()
if error is not None:assert error<1e-4,('Replay tolerance exceeded; preserve report',error)
