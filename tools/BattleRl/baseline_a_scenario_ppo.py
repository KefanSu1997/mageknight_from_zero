"""Pinned upstream recurrent PPO update for complete solo games; no prefix replay."""
import time
import gymnasium as gym
import numpy as np
import torch
from sb3_contrib import RecurrentPPO
from stable_baselines3.common.logger import configure
from baseline_a_scenario_policy import BaselineAScenarioPolicy
from baseline_a_scenario_buffer import complete_scenario_buffer


class SpaceOnlyEnv(gym.Env):
    def __init__(self,observation_space):
        self.observation_space=observation_space;self.action_space=gym.spaces.Discrete(2**24-1)
    def reset(self,*,seed=None,options=None):raise RuntimeError('Use native complete-game collector')
    def step(self,action):raise RuntimeError('Use native complete-game collector')


def finite_metrics(values,variance):
    metrics={}
    for key,value in values.items():
        if not isinstance(value,(int,float,np.number)):continue
        if not np.isfinite(value):
            if key=='train/explained_variance' and variance==0:metrics[key]=None
            else:raise ValueError('Nonfinite upstream optimization metric: '+key)
        else:metrics[key]=float(value)
    return metrics


def fresh_model(inputs,entity_vectors,device='cuda',seed=7112026):
    model=RecurrentPPO(BaselineAScenarioPolicy,SpaceOnlyEnv(inputs.space),device=device,seed=seed,
        n_steps=128,batch_size=128,n_epochs=4,learning_rate=1e-4,gamma=1.,gae_lambda=1.,
        clip_range=.2,ent_coef=.01,vf_coef=.5,max_grad_norm=.5,target_kl=.03,
        policy_kwargs=dict(entity_vectors=entity_vectors),verbose=0)
    model.set_logger(configure(format_strings=[]))
    return model


def update_complete(model,episodes,runtime):
    if model.device.type!='cuda':raise ValueError('Actual optimization is remote CUDA only')
    started=time.perf_counter()
    buffer=complete_scenario_buffer(episodes,model.observation_space,model.action_space,device=model.device)
    model.policy.bind_runtime(runtime)
    reference={}
    for episode in episodes:
        for row in episode:
            key=(int(row['observation']['context_id'][0]),int(row['action']))
            if key in reference and reference[key]!=row['log_probability']:
                raise ValueError('Context aliased across distinct recurrent histories')
            reference[key]=row['log_probability']
    # Original A uses the sampled-action importance-weighted entropy estimator.
    model.policy._entropy_reference=reference
    model.rollout_buffer=buffer;model.num_timesteps+=buffer.buffer_size;model._current_progress_remaining=1.
    count=[0]
    handle=model.policy.optimizer.register_step_post_hook(lambda *_:count.__setitem__(0,count[0]+1))
    try:model.train()
    finally:
        handle.remove();model.policy._entropy_reference=None
    if count[0]<1:raise ValueError('No accepted PPO optimizer step')
    if any(not torch.isfinite(p).all() for p in model.policy.parameters()):raise ValueError('Nonfinite policy')
    values=buffer.values.reshape(-1);targets=buffer.returns.reshape(-1)
    variance=0. if np.ptp(targets)==0 else float(np.var(targets));error=targets-values
    metrics=finite_metrics(model.logger.name_to_value,variance)
    return dict(episodes=len(episodes),decisions=buffer.buffer_size,accepted_optimizer_steps=count[0],
        seconds=time.perf_counter()-started,pre_ev=None if variance==0 else float(1-np.var(error)/variance),
        pre_mse=float(np.mean(error**2)),pre_bias=float(np.mean(values-targets)),target_variance=variance,
        upstream_metrics=metrics,
        ev_scope='Pre-update behavior predictions versus complete-game gamma1 returns; not fixed evaluation')
