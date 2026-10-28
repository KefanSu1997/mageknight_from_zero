"""Read-only checkpoint/recorded-state ablations. Never steps or updates live training."""
import os
os.environ.setdefault('OMP_NUM_THREADS','1')
import argparse
import hashlib
import json
import shutil
import types
from pathlib import Path
import numpy as np
import torch
from sb3_contrib.common.recurrent.type_aliases import RNNStates
from stable_baselines3.common.save_util import load_from_zip_file
from rollout_archive import load
from public_entity_tokens import PublicEntityTokens,original_vocabulary

def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--run',required=True);parser.add_argument('--assets',required=True);parser.add_argument('--out',required=True);parser.add_argument('--episodes',type=int,default=16)
    a=parser.parse_args();run=Path(a.run);out=Path(a.out);out.mkdir(parents=True,exist_ok=False)
    os.environ['BATTLE_RL_TEXT_ASSETS']=str(Path(a.assets).resolve())
    os.environ['BATTLE_RL_TEXT_ARM']='text'
    torch.set_num_threads(1)
    source=run/'checkpoint.zip';copy=out/'checkpoint.zip'
    before=sha(source);shutil.copy2(source,copy);after=sha(source)
    if before!=after or sha(copy)!=before:raise RuntimeError('Checkpoint changed while copying; evidence not accepted')
    registry=out/'atomic_supports.jsonl';shutil.copy2(run/'atomic_supports.jsonl',registry)
    if not registry.read_bytes().endswith(b'\n'):raise RuntimeError('Incomplete support snapshot')
    data,params,_=load_from_zip_file(copy,device='cpu');update=data['completed_updates']
    kwargs=dict(data['policy_kwargs']);kwargs['registry_file']=str(registry.resolve())
    policy=data['policy_class'](data['observation_space'],data['action_space'],lambda _:1e-4,**kwargs)
    policy.load_state_dict(params['policy']);policy.set_training_mode(False)
    archive=next(run.glob(f'rollout_{update-1:04d}_step*.archive'))
    buffer=load(archive)
    rewards=np.asarray(buffer.rewards).reshape(-1)
    starts=np.asarray(buffer.episode_starts).reshape(-1).astype(bool)
    returns=np.asarray(buffer.returns).reshape(-1)
    expected=np.zeros_like(returns);total=0.
    for i in range(len(rewards)-1,-1,-1):
        if i==len(rewards)-1 or starts[i+1]:total=0.
        total=float(rewards[i])+total;expected[i]=total
    return_error=float(np.max(np.abs(expected-returns)))
    assert return_error<1e-5
    begin=np.flatnonzero(starts);end=np.r_[begin[1:],len(rewards)]
    episodes=[np.arange(b,e) for b,e in zip(begin,end)][:a.episodes]
    assert len(begin)==128
    obs_arrays=buffer.observations
    bank=policy.features_extractor.frozen_text.clone()
    action_text=policy.descriptors[:,-384:].clone()
    captured={};original=policy._get_action_dist_from_latent
    def capture(self,latent):
        dist=original(latent);captured['probs']=dist.distribution.probs.detach().cpu();return dist
    policy._get_action_dist_from_latent=types.MethodType(capture,policy)
    results={};all_values={}
    for variant in ('full','state_text_zero','action_text_zero','both_text_zero'):
        policy.features_extractor.frozen_text.copy_(bank)
        policy.descriptors[:,-384:].copy_(action_text)
        if variant in ('state_text_zero','both_text_zero'):policy.features_extractor.frozen_text.zero_()
        if variant in ('action_text_zero','both_text_zero'):policy.descriptors[:,-384:].zero_()
        outputs={};values={}
        layers=policy.lstm_actor.num_layers;hidden=policy.lstm_actor.hidden_size
        zero=lambda:torch.zeros(layers,len(episodes),hidden)
        memory=RNNStates((zero(),zero()),(zero(),zero()))
        for t in range(max(map(len,episodes))):
            active=[i for i,ep in enumerate(episodes) if t<len(ep)]
            indices=[int(episodes[i][t]) for i in active]
            observation={k:torch.as_tensor(v[indices,0]) for k,v in obs_arrays.items()}
            state=RNNStates(*(tuple(x[:,active] for x in pair) for pair in memory))
            with torch.no_grad():
                _,value,_,next_state=policy.forward(observation,state,torch.tensor([float(t==0)]*len(active)),deterministic=True)
            for field in ('pi','vf'):
                for old,new in zip(getattr(memory,field),getattr(next_state,field)):old[:,active]=new
            for j,index in enumerate(indices):
                probs=captured['probs'][j];probs=probs[probs>0].numpy()
                outputs[index]=probs;values[index]=float(value[j])
        results[variant]=outputs;all_values[variant]=values
    policy.features_extractor.frozen_text.copy_(bank);policy.descriptors[:,-384:].copy_(action_text)
    choice_states=[i for i,p in results['full'].items() if len(p)>1]
    ablations={}
    for variant,outputs in results.items():
        kls=[];distances=[];changed=[]
        for i in choice_states:
            p=results['full'][i];q=outputs[i];assert p.shape==q.shape
            kls.append(float(np.sum(p*(np.log(p)-np.log(q)))))
            distances.append(float(np.abs(p-q).sum()/2));changed.append(bool(p.argmax()!=q.argmax()))
        ablations[variant]=dict(choice_states=len(choice_states),mean_KL=float(np.mean(kls)),
            mean_total_variation=float(np.mean(distances)),argmax_changed_fraction=float(np.mean(changed)),
            return_MSE=float(np.mean([(v-returns[i])**2 for i,v in all_values[variant].items()])))
    initial_data,initial_params,_=load_from_zip_file(run/'initial_checkpoint.zip',device='cpu')
    updates={}
    for key in ('features_extractor.text_projection.weight','features_extractor.attribute_projection.weight','features_extractor.entity_attention.layers.0.self_attn.in_proj_weight','argument_encoder.text.weight','argument_encoder.numeric.weight'):
        old=initial_params['policy'][key];new=params['policy'][key];delta=new-old
        updates[key]=dict(changed=int(torch.count_nonzero(delta)),relative_L2=float(delta.norm()/old.norm().clamp_min(1e-12)))
    vectors=bank.numpy();vectors=vectors/np.linalg.norm(vectors,axis=1,keepdims=True)
    similarities=(vectors@vectors.T)[np.triu_indices(len(vectors),1)]
    result=dict(saved_update=update,checkpoint_sha256=before,recorded_rollout=str(archive),
        rollout_episodes=len(begin),selected_first_episodes=len(episodes),observations=sum(map(len,episodes)),
        terminal_return_max_error=return_error,ablations=ablations,branch_parameter_updates=updates,
        entity_text_pairwise_cosine=dict(p10=float(np.quantile(similarities,.1)),median=float(np.median(similarities)),p90=float(np.quantile(similarities,.9))),
        scope='Recorded public observations and unchanged legal supports; each variant recomputes actor/critic memory from episode start. No native task replay or victory evaluation; sensitivity and same-rollout critic-fit diagnostics only. Live trainer/RNG/weights untouched.')
    (out/'diagnosis.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result),flush=True)

if __name__=='__main__':main()
