"""Complete-scenario adapter to the installed, unmodified RecurrentPPO.train.

Only input batching/recurrent replay are game-specific. PPO losses, clipping,
entropy, KL stopping, gradient clipping and Adam dispatch remain upstream.
Full current-policy prefixes are replayed with gradients; no battle/round reset
or stale stored hidden state substitutes for the scenario history.
"""
from types import SimpleNamespace
import time
import gymnasium as gym
import numpy as np
import torch
from torch import nn
from sb3_contrib import RecurrentPPO
from stable_baselines3.common.logger import configure
from stable_baselines3.common.utils import FloatSchedule
from solo_episode_data import SoloEpisodeData,verify_behavior_binding
from solo_progress import publish_progress


class SoloSequenceBuffer:
    def __init__(self,episodes,builder,device,seed):
        if not episodes or any(not len(e.records) for e in episodes):
            raise ValueError('Nonempty complete scenario data required')
        self.episodes=episodes;self.builder=builder;self.device=device
        self.rng=np.random.default_rng(seed)
        self.rows=[(e,i) for e in episodes for i in range(len(e.records))]
        self.values=np.concatenate([e.values for e in episodes])
        self.returns=np.concatenate([e.returns for e in episodes])
        self.yielded_sizes=[]

    def get(self,batch_size):
        if type(batch_size) is not int or batch_size<2:raise ValueError('Batch must be at least two')
        windows=[self.rows[i:i+batch_size] for i in range(0,len(self.rows),batch_size)]
        # Upstream uses unbiased std for advantage normalization. Keep the last
        # sample, joining a singleton tail to its predecessor rather than dropping it.
        if len(windows)>1 and len(windows[-1])==1:windows[-2].extend(windows.pop())
        if len(windows[0])<2:raise ValueError('Singleton rollout cannot normalize advantages')
        for number in self.rng.permutation(len(windows)):
            rows=windows[int(number)];self.yielded_sizes.append(len(rows))
            tensor=lambda x:torch.as_tensor(x,device=self.device,dtype=torch.float32)
            yield SimpleNamespace(observations=(rows,self.builder),
                actions=torch.tensor([e.actions[i] for e,i in rows],device=self.device,dtype=torch.long),
                old_values=tensor([e.values[i] for e,i in rows]),
                old_log_prob=tensor([e.old_log_probs[i] for e,i in rows]),
                advantages=tensor([e.advantages[i] for e,i in rows]),
                returns=tensor([e.returns[i] for e,i in rows]),
                mask=torch.ones(len(rows),device=self.device),lstm_states=None,episode_starts=None)


class SoloOptimizationPolicy(nn.Module):
    def __init__(self,network,learning_rate):
        super().__init__();self.network=network
        self.optimizer=torch.optim.Adam(self.parameters(),lr=learning_rate,eps=1e-5)
        self.accepted_steps=0
        self.progress_callback=None
        self.forward_calls=0;self.replayed_decisions=0
        self.prepare_host_seconds=0.;self.forward_host_seconds=0.
        self.post_forward_through_adam_host_seconds=0.;self.last_forward_finished=None
        self.optimizer.register_step_post_hook(self._accepted)

    def _accepted(self,*_):
        self.accepted_steps+=1
        if self.last_forward_finished is not None:
            self.post_forward_through_adam_host_seconds+=time.perf_counter()-self.last_forward_finished
            self.last_forward_finished=None
        if self.progress_callback:self.progress_callback('adam_completed')
    def set_training_mode(self,mode):self.train(mode)

    def evaluate_actions(self,obs,actions,lstm_states,episode_starts):
        if lstm_states is not None or episode_starts is not None:
            raise ValueError('This adapter replays the complete scenario prefix')
        rows,builder=obs;groups={}
        forward_started=time.perf_counter()
        for destination,(episode,index) in enumerate(rows):
            groups.setdefault(id(episode),(episode,[]))[1].append((destination,index))
        outputs=[None]*len(rows)
        for episode,selections in groups.values():
            end=max(index for _,index in selections)+1
            def prepare():
                for record in episode.records[:end]:
                    started=time.perf_counter()
                    item=builder.prepare_record(record)
                    self.prepare_host_seconds+=time.perf_counter()-started
                    yield item
            prepared=prepare()
            values,logs,entropy,_=self.network.evaluate_sequence(prepared,episode.actions[:end])
            self.replayed_decisions+=end
            for destination,index in selections:
                if int(actions[destination])!=int(episode.actions[index]):
                    raise ValueError('Minibatch action differs from archived support')
                outputs[destination]=(values[index],logs[index],entropy[index])
        result=tuple(torch.stack([row[k] for row in outputs]) for k in range(3))
        self.forward_calls+=1
        self.forward_host_seconds+=time.perf_counter()-forward_started
        self.last_forward_finished=time.perf_counter()
        if self.progress_callback:self.progress_callback('forward_completed')
        return result


class SoloPPOUpdate(RecurrentPPO):
    def __init__(self,network,*,learning_rate=1e-4,epochs=4,target_kl=.03,device='cuda'):
        super().__init__('MultiInputLstmPolicy',None,learning_rate=learning_rate,
                         batch_size=128,n_epochs=epochs,gamma=1.,gae_lambda=1.,
                         target_kl=target_kl,device=device,_init_setup_model=False)
        # Transport indices only, never a fixed policy output/action catalogue.
        self.action_space=gym.spaces.Box(0,np.iinfo(np.int64).max,shape=(),dtype=np.int64)
        self.policy=SoloOptimizationPolicy(network.to(self.device),learning_rate)
        self._setup_lr_schedule();self.clip_range=FloatSchedule(self.clip_range)
        self.set_logger(configure(folder=None,format_strings=[]))

    def update_complete(self,episodes,builder,seed,*,progress_path=None):
        if self.device.type!='cuda':raise ValueError('RL optimization is remote CUDA only')
        if not episodes or any(not isinstance(e,SoloEpisodeData) for e in episodes):
            raise ValueError('Only audited complete scenario data may enter PPO')
        checked=set()
        for episode in episodes:
            if episode.collection_directory not in checked:
                verify_behavior_binding(episode,self.policy.network,builder)
                checked.add(episode.collection_directory)
        self.rollout_buffer=SoloSequenceBuffer(episodes,builder,self.device,seed)
        before=self.policy.accepted_steps;torch.cuda.synchronize();start=time.perf_counter()
        counters=('forward_calls','replayed_decisions','prepare_host_seconds','forward_host_seconds',
                  'post_forward_through_adam_host_seconds')
        initial={key:getattr(self.policy,key) for key in counters}
        def snapshot(stage):
            record=dict(stage=stage,elapsed_seconds=time.perf_counter()-start,
                        accepted_optimizer_steps=self.policy.accepted_steps-before,
                        evaluated_minibatches=len(self.rollout_buffer.yielded_sizes),
                        selected_samples=sum(self.rollout_buffer.yielded_sizes),
                        total_rollout_samples=len(self.rollout_buffer.rows),epochs_requested=self.n_epochs,
                        timing_scope='Host-observed intervals; forward includes prepare; post-forward includes loss/backward/clipping/Adam. Not CUDA kernel timings.',
                        **{key:getattr(self.policy,key)-initial[key] for key in counters})
            if progress_path is not None:publish_progress(progress_path,record)
            return record
        self.policy.progress_callback=snapshot
        snapshot('started')
        # Do not copy or modify the installed optimizer implementation.
        try:super().train()
        finally:self.policy.progress_callback=None
        torch.cuda.synchronize()
        diagnostics=snapshot('completed')
        return dict(optimization_seconds=time.perf_counter()-start,
                    diagnostics=diagnostics,
                    accepted_optimizer_steps=self.policy.accepted_steps-before,
                    evaluated_minibatch_sizes=self.rollout_buffer.yielded_sizes,
                    upstream_metrics={k:float(v) if np.isfinite(v) else None for k,v in self.logger.name_to_value.items()
                                      if isinstance(v,(float,int,np.number))})

    def learn(self,*args,**kwargs):
        raise RuntimeError('Use audited complete native collection and update_complete; generic collection is incompatible')
