"""Complete-episode collection; PPO/sequence minibatching remain SB3 upstream."""
import json
from concurrent.futures import ThreadPoolExecutor
import numpy as np
import torch
from sb3_contrib.common.recurrent.type_aliases import RNNStates
from compact_recurrent_buffer import CompactRecurrentDictRolloutBuffer

class CompleteEpisodeCollector:
    episodes_per_update=128
    def collect_rollouts(self,env,callback,rollout_buffer,n_rollout_steps):
        self.policy.set_training_mode(False)
        callback.on_rollout_start()
        envs=env.envs;active={};accepted=[];accepted_meta=[];rejected=0
        layers=self.policy.lstm_actor.num_layers;hidden=self.policy.lstm_actor.hidden_size
        def start(indices,pool):
            futures=[(i,pool.submit(envs[i].reset)) for i in indices]
            for i,f in futures:
                obs,_=f.result()
                zeros=lambda:torch.zeros((layers,1,hidden),device=self.device)
                active[i]=dict(obs=obs,states=RNNStates((zeros(),zeros()),(zeros(),zeros())),rows=[],start=True)
        with ThreadPoolExecutor(max_workers=len(envs)) as pool:
            start(range(min(len(envs),self.episodes_per_update)),pool)
            while active:
                ids=sorted(active)
                observations={k:np.stack([active[i]['obs'][k] for i in ids]) for k in active[ids[0]]['obs']}
                states=RNNStates(*(tuple(torch.cat([getattr(active[i]['states'],field)[j] for i in ids],dim=1) for j in range(2)) for field in ('pi','vf')))
                starts=np.array([active[i]['start'] for i in ids],dtype=np.float32)
                with torch.no_grad():
                    actions,values,logs,next_states=self.policy.forward(self.policy.obs_to_tensor(observations)[0],states,torch.as_tensor(starts,device=self.device))
                actions=actions.cpu().numpy();values=values.cpu();logs=logs.cpu()
                old_states=RNNStates(*(tuple(t.cpu() for t in pair) for pair in states))
                futures=[pool.submit(envs[i].step,int(a)) for i,a in zip(ids,actions)]
                restart=[]
                for j,(i,f) in enumerate(zip(ids,futures)):
                    obs,reward,done,_,info=f.result();slot=active[i]
                    slot['rows'].append((slot['obs'],int(actions[j]),reward,slot['start'],values[j:j+1],logs[j:j+1],
                        RNNStates(*(tuple(t[:,j:j+1,:].clone() for t in pair) for pair in old_states))))
                    slot.update(obs=obs,start=False,states=RNNStates(*(tuple(t[:,j:j+1,:] for t in pair) for pair in next_states)))
                    self.num_timesteps+=1
                    if done:
                        valid='audit_failure' not in info
                        with (self.output/'performance.jsonl').open('a') as stream:
                            stream.write(json.dumps(dict(worker=i,update=self.completed_updates,task_id=info['task_id'],**info['performance']))+'\n')
                        if valid:
                            accepted.append(slot['rows'])
                            accepted_meta.append(dict(worker=i,task_id=info['task_id'],episode=envs[i].episode-1 if hasattr(envs[i],'episode') else None))
                            self.ep_info_buffer.append(info['episode'])
                        else:
                            rejected+=1
                            with (self.output/'audit_quarantine.jsonl').open('a') as stream:
                                stream.write(json.dumps(dict(worker=i,behavior_update=self.completed_updates,task_id=info['task_id'],errors=info['audit_failure'],rejected_steps=len(slot['rows'])))+'\n')
                        del active[i];restart.append(i)
                count=min(len(restart),self.episodes_per_update-len(accepted)-len(active))
                start(restart[:count],pool)
        assert len(accepted)==self.episodes_per_update
        total=sum(map(len,accepted))
        buffer=CompactRecurrentDictRolloutBuffer(total,self.observation_space,self.action_space,
            (total,layers,1,hidden),device=self.device,gamma=self.gamma,gae_lambda=self.gae_lambda,n_envs=1)
        for episode in accepted:
            for obs,action,reward,start,value,log,states in episode:
                buffer.add({k:v[None] for k,v in obs.items()},np.array([[action]]),np.array([reward]),np.array([start]),value,log,lstm_states=states)
        buffer.compute_returns_and_advantage(torch.zeros(1,device=self.device),np.ones(1))
        self.rollout_buffer=buffer
        self._last_obs={k:np.stack([e._observation()[k] for e in envs]) for k in envs[0]._observation()}
        self._last_episode_starts=np.ones(len(envs),dtype=bool)
        zero=lambda:torch.zeros((layers,len(envs),hidden),device=self.device)
        self._last_lstm_states=RNNStates((zero(),zero()),(zero(),zero()))
        with (self.output/'episode_batches.jsonl').open('a') as stream:
            stream.write(json.dumps(dict(update=self.completed_updates+1,accepted_episodes=len(accepted),rejected_episodes=rejected,transitions=total,lengths=list(map(len,accepted)),policy_fixed=True,episodes=accepted_meta))+'\n')
        callback.on_rollout_end()
        return True
