"""Paired value predictions on later training episodes, complete LSTM replay.
Neither tested checkpoint was optimized on the selected later archive.
This is temporal training-stream validation, not the fixed task-heldout eval.
"""
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
torch.set_num_threads(1);root=Path('remote_conditioned300');stage=Path('optimization_stage_20261014')
parser=argparse.ArgumentParser();parser.add_argument('--update',type=int,choices=[205,210],default=205);parser.add_argument('--archive-update',type=int);parser.add_argument('--diagnose-lstm',action='store_true');a=parser.parse_args()
archive_update=a.update if a.archive_update is None else a.archive_update
assert archive_update>=a.update
if a.diagnose_lstm:assert archive_update==a.update,'Stored LSTM states only reproduce matching collector weights'
candidate=stage/('root_entropy_cpu_probe.zip' if a.update==205 else 'value_probe_checkpoint0210.zip')
paths=[Path('remote_context_pilot200/checkpoint.zip'),candidate]
models=[];bindings=[]
for path in paths:
 raw=path.read_bytes();m=base.RecordedRecurrentPPO.load(io.BytesIO(raw),device='cpu');m.policy.set_training_mode(False)
 m.policy.context_path=str(root/'contexts');m.policy.registry_file=str(root/'atomic_supports.jsonl')
 for f in (m.policy.features_extractor,m.policy.pi_features_extractor,m.policy.vf_features_extractor):f.context_path=str(root/'contexts')
 models.append(m);bindings.append(dict(path=str(path),update=m.completed_updates,sha=hashlib.sha256(raw).hexdigest()))
assert [m.completed_updates for m in models]==[200,a.update]
# Archive label is the pre-optimization update, sampled AFTER candidate weights.
archive=sorted(root.glob(f'rollout_{archive_update:04d}_*.archive'))[0];b=load(archive)
db=sqlite3.connect('file:'+str(root/'contexts/packed_vectors/offsets.sqlite')+'?mode=ro',uri=True);stream=(root/'contexts/packed_vectors/vectors.bin').open('rb')
def vectors(path,context,node):
 if not context:return np.zeros((2,384),np.float32)
 assert Path(path).resolve()==(root/'contexts').resolve()
 row=db.execute('SELECT metadata FROM entries WHERE context=? AND node=?',(int(context),int(node))).fetchone();assert row
 meta=json.loads(row[0]);stream.seek(meta['offset']);payload=stream.read(meta['bytes']);assert hashlib.sha256(payload).hexdigest()==meta['sha256']
 return np.frombuffer(payload,dtype='<f4').reshape(meta['shape']).copy()
h.node_vectors=vectors;packed.prefetch=lambda path,requests:None
starts=np.flatnonzero(b.episode_starts.reshape(-1));assert starts[0]==0 and len(starts)==128
returns=b.returns.reshape(-1);ends=np.r_[starts[1:],len(returns)];predictions=[[],[]];per_episode=[]
with torch.no_grad():
 for start,end in zip(starts,ends):
  obs={k:torch.tensor(v.reshape((-1,)+models[0].observation_space.spaces[k].shape)[start:end]) for k,v in b.observations.items()}
  flags=torch.zeros(end-start);flags[0]=1;errors=[]
  for index,m in enumerate(models):
   zero=torch.zeros(m.policy.lstm_critic.num_layers,1,m.policy.lstm_critic.hidden_size)
   values=m.policy.predict_values(obs,(zero.clone(),zero.clone()),flags).flatten().numpy()
   assert len(values)==end-start and np.isfinite(values).all();predictions[index].append(values)
   errors.append(float(np.mean((values-returns[start:end])**2)))
  per_episode.append(errors)
def metrics(pred):
 error=returns-pred;variance=float(np.var(returns))
 return dict(ev=1-float(np.var(error))/variance if variance else None,mse=float(np.mean(error**2)),bias=float(np.mean(pred-returns)))
scores=[metrics(np.concatenate(p)) for p in predictions]
replay_error=float(np.max(np.abs(np.concatenate(predictions[1])-b.values.reshape(-1)))) if archive_update==a.update else None
stored_metrics=metrics(b.values.reshape(-1))
metric_differences={k:abs(scores[1][k]-stored_metrics[k]) for k in scores[1]} if replay_error is not None else None
report=dict(checkpoints=bindings,archive=str(archive),episodes=len(starts),samples=len(returns),scores=scores,archived_value_replay_max_error=replay_error,replay_tolerance=1e-4,replay_gate_passed=replay_error<1e-4 if replay_error is not None else None,archived_value_metrics=stored_metrics,replay_metric_absolute_differences=metric_differences,episode_mse_pairs=per_episode,scope=f'Same later training-stream episodes unseen by both checkpoint optimizers, full critic LSTM replay from zero for each episode. Data collected by s{archive_update}; not independent task-heldout validation or proof of policy improvement. Archived prediction equality only applies when collector and probe checkpoint match.')
if a.diagnose_lstm:
 m=models[1];hidden=b.hidden_states_vf;cells=b.cell_states_vf
 print(json.dumps(dict(hidden_shape=list(hidden.shape),cell_shape=list(cells.shape))),flush=True)
 assert hidden.shape==cells.shape and hidden.shape==(len(returns),m.policy.lstm_critic.num_layers,1,m.policy.lstm_critic.hidden_size)
 stored_predictions=[]
 with torch.no_grad():
  for start in range(0,len(returns),128):
   end=min(start+128,len(returns));obs={k:torch.tensor(v.reshape((-1,)+m.observation_space.spaces[k].shape)[start:end]) for k,v in b.observations.items()}
   state=tuple(torch.tensor(x[start:end,:,0,:].transpose(1,0,2).copy()) for x in (hidden,cells))
   flags=torch.tensor(b.episode_starts.reshape(-1)[start:end]);stored_predictions.append(m.policy.predict_values(obs,state,flags).flatten().numpy())
 p=np.concatenate(stored_predictions);archived=b.values.reshape(-1);full=np.concatenate(predictions[1])
 report['lstm_diagnosis']=dict(using_archived_state_max_error=float(np.max(np.abs(p-archived))),full_vs_archived_state_max_difference=float(np.max(np.abs(full-p))),using_archived_state_metrics=metrics(p))
suffix='_lstm_diagnosis' if a.diagnose_lstm else ''
if archive_update!=a.update:suffix+=f'_on_{archive_update:04d}'
(stage/f'future_batch_value_{a.update:04d}{suffix}.json').write_text(json.dumps(report,indent=2));print(json.dumps({k:v for k,v in report.items() if k!='episode_mse_pairs'}));stream.close();db.close()
if replay_error is not None:assert replay_error<1e-4,('CPU full-episode replay differs from archived rollout values',replay_error)
