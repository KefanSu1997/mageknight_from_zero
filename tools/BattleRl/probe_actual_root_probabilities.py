"""Saved recurrent sample root probabilities, remote CPU/cache only."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
import sys,io,json,sqlite3,hashlib,math
from pathlib import Path
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
import torch,numpy as np
import train_sb3_atomic_entity_text as base
import hierarchical_option_policy as h
import hierarchical_packed_cache as packed
from rollout_archive import load
from types import SimpleNamespace
from sb3_contrib.common.recurrent.type_aliases import RNNStates
torch.set_num_threads(1);root=Path('remote_public_state71')
raw=(root/'checkpoint.zip').read_bytes();m=base.RecordedRecurrentPPO.load(io.BytesIO(raw),device='cpu');m.policy.set_training_mode(False)
b=load(next(root.glob('rollout_0077_*.archive')))
starts=np.flatnonzero(b.episode_starts.ravel());assert len(starts)==128
zero=lambda:torch.zeros((1,len(starts),256))
batch=SimpleNamespace(observations={k:torch.tensor(v.reshape((-1,)+m.observation_space.spaces[k].shape)[starts],dtype=torch.float32) for k,v in b.observations.items()},actions=torch.tensor(b.actions.ravel()[starts]),lstm_states=RNNStates(pi=(zero(),zero()),vf=(zero(),zero())),episode_starts=torch.ones(len(starts)),mask=torch.ones(len(starts)))
db=sqlite3.connect('file:'+str(root/'contexts/packed_vectors/offsets.sqlite')+'?mode=ro',uri=True);stream=(root/'contexts/packed_vectors/vectors.bin').open('rb')
def vectors(path,ctx,node):
    if not ctx:return np.zeros((2,384),np.float32)
    row=db.execute('SELECT metadata FROM entries WHERE context=? AND node=?',(int(ctx),int(node))).fetchone();assert row
    meta=json.loads(row[0]);stream.seek(meta['offset']);payload=stream.read(meta['bytes']);assert hashlib.sha256(payload).hexdigest()==meta['sha256']
    return np.frombuffer(payload,dtype='<f4').reshape(meta['shape']).copy()
h.node_vectors=vectors;packed.prefetch=lambda *args:None
m.policy._capture_entropy_distribution=True
rows=[]
with torch.no_grad():
    m.policy.evaluate_actions(batch.observations,batch.actions.long().flatten(),batch.lstm_states,batch.episode_starts)
    dist=m.policy._root_entropy_distribution
    for i,ctx in enumerate(dist.contexts):
        if not batch.mask[i] or not ctx:continue
        tree,_=h.tree(m.policy.context_path,int(ctx));node=tree.branch(0)
        if tree.nodes[node]['leaf'] is not None:continue
        cat=dist.categorical(i,node);probs=cat.probs.cpu().tolist();edges=list(tree.nodes[node]['children'])
        state=batch.observations['state'][i]
        phase=next((n for j,n in enumerate(['Ranged','Block','AssignDamage','Melee','End'],7) if state[j]>.5),'Outside')
        rows.append(dict(context=int(ctx),phase=phase,options=[list(e) for e in edges],probabilities=probs,max_probability=max(probs),normalized_entropy=float(cat.entropy())/math.log(len(probs))))
assert rows
report=dict(checkpoint_sha=hashlib.sha256(raw).hexdigest(),completed=int(m.completed_updates),sampled_states=len(rows),max_probability_median=float(np.median([r['max_probability'] for r in rows])),normalized_entropy_median=float(np.median([r['normalized_entropy'] for r in rows])),rows=rows,scope='128 real episode initial states from rollout77, exact zero LSTM/current saved checkpoint. Diagnostic probabilities, not historical policy or fixed evaluation. No encoder/optimizer/environment calls.')
target=root/('initial_root_probs_'+report['checkpoint_sha'][:12]+'.json');target.write_text(json.dumps(report,indent=2));print(json.dumps({k:v for k,v in report.items() if k!='rows'}))
