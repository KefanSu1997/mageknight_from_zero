"""Saved A300 LSTM gate: minibatch boundaries, targets, coverage and gradients."""
import io
import json
import zipfile
from pathlib import Path
import gymnasium as gym
import numpy as np
import torch
from torch import nn
from sb3_contrib.common.recurrent.policies import RecurrentActorCriticPolicy
from sb3_contrib.common.recurrent.type_aliases import RNNStates
from baseline_a_scenario_buffer import complete_scenario_buffer


def run(checkpoint):
    torch.set_num_threads(2);torch.manual_seed(7112026);np.random.seed(7112026)
    with zipfile.ZipFile(checkpoint) as z:
        source=torch.load(io.BytesIO(z.read('policy.pth')),map_location='cpu',weights_only=True)
    def extract(prefix):return {k[len(prefix):]:v for k,v in source.items() if k.startswith(prefix)}
    actor=nn.LSTM(128,256);critic=nn.LSTM(128,256)
    actor.load_state_dict(extract('lstm_actor.'));critic.load_state_dict(extract('lstm_critic.'))
    mlp=nn.Sequential(nn.Linear(256,128),nn.Tanh());mlp.load_state_dict(extract('mlp_extractor.value_net.'))
    value_head=nn.Linear(128,1);value_head.load_state_dict(extract('value_net.'))
    episodes=[];row_number=0
    # Deliberately crosses 128 boundaries and exercises a 1-sample last tail.
    for length in (170,87):
        zero=lambda:torch.zeros(1,1,256)
        states=RNNStates((zero(),zero()),(zero(),zero()));episode=[]
        for i in range(length):
            features=torch.randn(1,128)*.1;before=RNNStates(*(tuple(v.clone() for v in pair) for pair in states))
            with torch.no_grad():
                _,pi=actor(features[None],states.pi);vf,vf_state=critic(features[None],states.vf)
                value=float(value_head(mlp(vf[0,0])).squeeze())
            row_number+=1
            episode.append(dict(observation={'state':features[0].numpy(),'row_id':np.array([row_number],np.int64)},
                action=0,reward=.01 if i==length-1 else -.0001,value=value,log_probability=0.,
                episode_start=i==0,terminated=i==length-1,lstm_before=before))
            states=RNNStates(pi,vf_state)
        episodes.append(episode)
    space=gym.spaces.Dict({'state':gym.spaces.Box(-np.inf,np.inf,(128,),np.float32),
                          'row_id':gym.spaces.Box(0,2**40,(1,),np.int64)})
    actions=gym.spaces.Discrete(1)
    buffer=complete_scenario_buffer(episodes,space,actions,device='cpu')
    expected_returns=np.concatenate([np.cumsum([r['reward'] for r in e][::-1])[::-1] for e in episodes])
    np.testing.assert_allclose(buffer.returns[:,0],expected_returns,atol=1e-6)
    seen=[];sizes=[];maximum=0.;replayed=0
    for batch in buffer.get(128):
        keep=batch.mask.bool();ids=batch.observations['row_id'].flatten()[keep].long().tolist()
        seen.extend(ids);sizes.append(len(ids))
        outputs,_=RecurrentActorCriticPolicy._process_sequence(batch.observations['state'],
                    batch.lstm_states.vf,batch.episode_starts,critic)
        predictions=value_head(mlp(outputs)).flatten()
        maximum=max(maximum,float((predictions[keep]-batch.old_values[keep]).abs().max().detach()))
        predictions[keep].square().mean().backward();replayed+=len(batch.mask)
    assert sorted(seen)==list(range(1,258)) and len(seen)==len(set(seen))
    assert sizes==[128,129] and maximum<1e-5
    gradient=sum(float(p.grad.abs().sum()) for p in critic.parameters() if p.grad is not None)
    assert gradient>0 and all(p.grad is None or torch.isfinite(p.grad).all() for p in critic.parameters())
    rejected=[]
    for name,field,index in (('midgame reset','episode_start',1),('incomplete game','terminated',-1)):
        old=episodes[0][index][field];episodes[0][index][field]=not old
        try:complete_scenario_buffer(episodes,space,actions,device='cpu')
        except ValueError:rejected.append(name)
        else:raise AssertionError('Accepted invalid memory boundary')
        finally:episodes[0][index][field]=old
    return dict(passed=True,samples=257,minibatch_real_sizes=sizes,padded_rows_evaluated=replayed,
                value_max_abs=maximum,critic_gradient_l1=gradient,negative_cases_rejected=rejected,
                optimizer_steps=0,scope='Saved A300 critic with controlled sequences; not actual solo rollout/PPO acceptance')


if __name__=='__main__':
    root=Path('H:/MageKnight_RL_Runs/20261022_A_baseline_spec')
    result=run(root/'reference_checkpoint.zip')
    (root/'scenario_recurrent_buffer_gate.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps(result))
