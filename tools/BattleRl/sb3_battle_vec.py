"""Threaded IO to independent .NET processes; one central batched GPU policy."""
from concurrent.futures import ThreadPoolExecutor
from copy import deepcopy
from stable_baselines3.common.vec_env import DummyVecEnv


class ThreadedRulesVecEnv(DummyVecEnv):
    def __init__(self, env_fns):
        super().__init__(env_fns)
        self.executor = ThreadPoolExecutor(max_workers=self.num_envs)

    def step_async(self, actions):
        self.futures = [self.executor.submit(env.step, action) for env, action in zip(self.envs, actions)]

    def step_wait(self):
        for i, future in enumerate(self.futures):
            obs, self.buf_rews[i], terminated, truncated, self.buf_infos[i] = future.result()
            self.buf_dones[i] = terminated or truncated
            self.buf_infos[i]['TimeLimit.truncated'] = truncated and not terminated
            if self.buf_dones[i]:
                self.buf_infos[i]['terminal_observation'] = obs
                obs, self.reset_infos[i] = self.envs[i].reset()
            self._save_obs(i, obs)
        return self._obs_from_buf(), self.buf_rews.copy(), self.buf_dones.copy(), deepcopy(self.buf_infos)

    def close(self):
        self.executor.shutdown(wait=True)
        super().close()
