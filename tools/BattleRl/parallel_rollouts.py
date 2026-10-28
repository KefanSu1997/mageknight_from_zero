"""Synchronous on-policy PPO collection using independent real rule processes."""
import copy
import hashlib
import json
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
import numpy as np


class ParallelRollouts:
    def __init__(self, rules, workers, task_random_state, states=None):
        from train_ppo import Rules
        if workers < 2:
            raise ValueError('Parallel collection requires at least two real environments')
        if states is not None and len(states) != workers:
            raise ValueError('Restore every worker RNG; do not silently reseed workers')
        self.environments = [rules]
        try:
            for _ in range(workers - 1):
                self.environments.append(Rules())
            for env in self.environments[1:]:
                if env.info['featureSchema'] != rules.info['featureSchema'] or env.actions != rules.actions:
                    raise ValueError('Worker real rule/catalog projection differs')
        except Exception:
            for env in self.environments[1:]:
                env.close()
            raise
        self.random = []
        for index in range(workers):
            seed = int.from_bytes(hashlib.sha256((json.dumps(task_random_state, sort_keys=True) + ':' + str(index)).encode()).digest()[:16], 'little')
            rng = np.random.default_rng(seed)
            if states is not None:
                rng.bit_generator.state = states[index]
            self.random.append(rng)
        self.executor = ThreadPoolExecutor(max_workers=workers)

    def collect(self, policy, jobs, collect, arguments, output, task_seed=None):
        # SharedArgumentPolicy temporarily writes legal wp columns. Each worker
        # needs private parameters/caches; only immutable descriptors may share.
        memo = {id(policy.head.argument_features): policy.head.argument_features} if hasattr(policy.head, 'argument_features') else {}
        for name in ('_conditional_probs', '_kind_probs'):
            cached=getattr(policy.head,name,None)
            if cached is not None:memo[id(cached)]=None
        copies = [copy.deepcopy(policy, memo.copy()) for _ in self.environments]
        for clone in copies:
            if any(np.shares_memory(clone.parameters()[k], value) for k, value in policy.parameters().items()):
                raise ValueError('Worker mutable policy arrays are shared')
        groups = [[] for _ in self.environments]
        for order, (episode_index, certificate) in enumerate(jobs):
            groups[order % len(groups)].append((order, episode_index, certificate))

        def run(worker):
            result = []
            for order, episode_index, certificate in groups[worker]:
                if task_seed is not None:
                    # Fixed evaluation has an independent stream per task,
                    # unaffected by another task's episode length.
                    seed = np.random.SeedSequence([task_seed, int(certificate['task_id'][:16], 16)])
                    self.random[worker].bit_generator.state = np.random.default_rng(seed).bit_generator.state
                rng_before = copy.deepcopy(self.random[worker].bit_generator.state)
                try:
                    episode = collect(self.environments[worker], copies[worker], self.random[worker],
                                      certificate, *arguments(episode_index))
                    episode['worker_id'] = worker
                    from rollout_archive import save_episode
                    save_episode(output, episode_index, certificate, episode, rng_before,
                                 copy.deepcopy(self.random[worker].bit_generator.state))
                    result.append((order, episode))
                except Exception as error:
                    failure = dict(worker=worker, episode=episode_index, task_id=certificate['task_id'],
                        seed=certificate['task']['seed'], certificate=certificate, rng_before=rng_before,
                        rng_after=copy.deepcopy(self.random[worker].bit_generator.state), error=str(error),
                        trace=getattr(error, 'trace', []), attempted_action=getattr(error, 'attempted_action', None))
                    failure['current_state'] = getattr(error, 'current_state', None)
                    (Path(output) / f'worker_{worker}_episode_{episode_index}_failure.json').write_text(json.dumps(failure, ensure_ascii=False), encoding='utf-8')
                    raise
            return result

        futures = [self.executor.submit(run, worker) for worker in range(len(groups))]
        results = [item for future in futures for item in future.result()]
        return [episode for _, episode in sorted(results)]

    def state(self):
        return [copy.deepcopy(rng.bit_generator.state) for rng in self.random]

    def close(self):
        self.executor.shutdown(wait=True)
        for env in self.environments[1:]:
            env.close()
