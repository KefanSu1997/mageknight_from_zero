"""Collect complete episodes with ready-worker GPU batches and async resets."""
import json
from concurrent.futures import ThreadPoolExecutor, wait, FIRST_COMPLETED
import numpy as np
import torch
from sb3_contrib.common.recurrent.type_aliases import RNNStates
from compact_recurrent_buffer import CompactRecurrentDictRolloutBuffer


def collect_rollouts(self, env, callback, rollout_buffer, n_rollout_steps):
    self.policy.set_training_mode(False)
    callback.on_rollout_start()
    envs = env.envs
    active, pending, resetting = {}, {}, {}
    accepted, metadata = [], []
    rejected = 0
    layers, hidden = self.policy.lstm_actor.num_layers, self.policy.lstm_actor.hidden_size
    limits = getattr(self, 'benchmark_worker_limits', None)
    finished = [0] * len(envs)
    zero = lambda: torch.zeros((layers, 1, hidden), device=self.device)
    with ThreadPoolExecutor(max_workers=len(envs)) as pool:
        for i in range(min(len(envs), self.episodes_per_update)):
            resetting[i] = pool.submit(envs[i].reset)
        while active or pending or resetting:
            for i in list(resetting):
                if not resetting[i].done():
                    continue
                obs, _ = resetting.pop(i).result()
                active[i] = dict(obs=obs, states=RNNStates((zero(), zero()), (zero(), zero())), rows=[], start=True)
            for i in list(pending):
                future, record = pending[i]
                if not future.done():
                    continue
                del pending[i]
                obs, reward, done, _, info = future.result()
                slot = active[i]
                old_obs, action, start_flag, value, log, old_states = record
                slot['rows'].append((old_obs, action, reward, start_flag, value, log, old_states))
                slot.update(obs=obs, start=False)
                self.num_timesteps += 1
                if done:
                    with (self.output / 'performance.jsonl').open('a') as f:
                        f.write(json.dumps(dict(worker=i, update=self.completed_updates, task_id=info['task_id'], **info['performance'])) + '\n')
                    if 'audit_failure' not in info:
                        accepted.append(slot['rows'])
                        metadata.append(dict(worker=i, task_id=info['task_id'], episode=envs[i].episode-1))
                        self.ep_info_buffer.append(info['episode'])
                        finished[i] += 1
                    else:
                        rejected += 1
                        with (self.output / 'audit_quarantine.jsonl').open('a') as f:
                            f.write(json.dumps(dict(worker=i, behavior_update=self.completed_updates, task_id=info['task_id'], errors=info['audit_failure'], rejected_steps=len(slot['rows']))) + '\n')
                    del active[i]
                    if (limits is None or finished[i] < limits[i]) and len(accepted) + len(active) + len(resetting) < self.episodes_per_update:
                        resetting[i] = pool.submit(envs[i].reset)
            ids = [i for i in sorted(active) if i not in pending][:16]
            outstanding = [f for f, _ in pending.values()] + list(resetting.values())
            # Briefly accumulate useful batches rather than launching singleton
            # forwards while other independent rule workers are almost ready.
            if outstanding and len(ids) < min(8, len(active) + len(resetting)):
                wait(outstanding, timeout=.005, return_when=FIRST_COMPLETED)
                if not ids or any(f.done() for f in outstanding):
                    continue
            if not ids:
                continue
            observations = {k: np.stack([active[i]['obs'][k] for i in ids]) for k in active[ids[0]]['obs']}
            states = RNNStates(*(tuple(torch.cat([getattr(active[i]['states'], field)[j] for i in ids], dim=1) for j in range(2)) for field in ('pi', 'vf')))
            starts = np.array([active[i]['start'] for i in ids], dtype=np.float32)
            with torch.no_grad():
                actions, values, logs, next_states = self.policy.forward(self.policy.obs_to_tensor(observations)[0], states, torch.as_tensor(starts, device=self.device))
            actions, values, logs = actions.cpu().numpy(), values.cpu(), logs.cpu()
            old_states = RNNStates(*(tuple(t.cpu() for t in pair) for pair in states))
            for j, i in enumerate(ids):
                slot = active[i]
                record = (slot['obs'], int(actions[j]), slot['start'], values[j:j+1], logs[j:j+1], RNNStates(*(tuple(t[:, j:j+1, :].clone() for t in pair) for pair in old_states)))
                slot['states'] = RNNStates(*(tuple(t[:, j:j+1, :] for t in pair) for pair in next_states))
                pending[i] = (pool.submit(envs[i].step, int(actions[j])), record)
    assert len(accepted) == self.episodes_per_update
    total = sum(map(len, accepted))
    buffer = CompactRecurrentDictRolloutBuffer(total, self.observation_space, self.action_space, (total, layers, 1, hidden), device=self.device, gamma=self.gamma, gae_lambda=self.gae_lambda, n_envs=1)
    for episode in accepted:
        for obs, action, reward, start, value, log, states in episode:
            buffer.add({k: v[None] for k, v in obs.items()}, np.array([[action]]), np.array([reward]), np.array([start]), value, log, lstm_states=states)
    buffer.compute_returns_and_advantage(torch.zeros(1, device=self.device), np.ones(1))
    self.rollout_buffer = buffer
    self._last_obs = {k: np.stack([e._observation()[k] for e in envs]) for k in envs[0]._observation()}
    self._last_episode_starts = np.ones(len(envs), dtype=bool)
    zero = lambda: torch.zeros((layers, len(envs), hidden), device=self.device)
    self._last_lstm_states = RNNStates((zero(), zero()), (zero(), zero()))
    with (self.output / 'episode_batches.jsonl').open('a') as f:
        f.write(json.dumps(dict(update=self.completed_updates+1, accepted_episodes=len(accepted), rejected_episodes=rejected, transitions=total, lengths=list(map(len, accepted)), policy_fixed=True, episodes=metadata, scheduling='ready_workers_batch16')) + '\n')
    callback.on_rollout_end()
    return True
