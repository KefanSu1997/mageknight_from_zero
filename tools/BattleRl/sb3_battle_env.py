"""Gymnasium adapter over the existing real .NET rules, with durable evidence."""
import copy
import json
from pathlib import Path
import gymnasium as gym
import numpy as np
from train_ppo import Rules
from task_feasibility import digest
from trajectory_audit import audit
from sequence_training_reward import TerminalSequenceReward
from progress_potential_reward import ProgressPotentialReward
from planning_revisit_reward import PlanningRevisitReward
from shared_planning_revisit_reward import SharedPlanningRevisitReward
from single_battle_reward import SingleBattleReward


class SharedCatalogRules(Rules):
    _catalog = None

    def close(self):
        if self.p.poll() is None:
            super().close()

    def __init__(self):
        super().__init__()
        if SharedCatalogRules._catalog is None:
            SharedCatalogRules._catalog = self.actions
        elif self.actions != SharedCatalogRules._catalog:
            self.close()
            raise ValueError('Worker action catalog mismatch')
        self.actions = SharedCatalogRules._catalog
        self.info['catalog'] = self.actions


class BattleEnv(gym.Env):
    quarantined_tasks = set()
    def __init__(self, rows, output, worker, history_features, seed=6112026,
                 reward_config=None, fixed_order=False):
        self.rules = SharedCatalogRules()
        self.rows, self.output, self.worker = rows, Path(output), worker
        self.output.mkdir(parents=True, exist_ok=True)
        self.history_features = history_features
        self.rng = np.random.default_rng(seed + worker)
        self.config = reward_config or dict(version=SingleBattleReward.version)
        self.fixed_order = fixed_order
        self.results = {}
        self.episode = 0
        self.total_steps = 0
        self.current = None
        self.trace = []
        self.previous = np.zeros(history_features.shape[1]+1, dtype=np.float32)
        self.action_space = gym.spaces.Discrete(len(self.rules.actions))
        initial = self.rules.reset_task(rows[0]['task']['definition'])
        self.observation_space = gym.spaces.Dict({
            'state': gym.spaces.Box(-np.inf, np.inf, (len(initial['features'])+len(self.previous),), np.float32),
            'legal_bits': gym.spaces.Box(0, 255, ((self.action_space.n+7)//8,), np.uint8)})

    def _observation(self):
        mask = np.zeros(self.action_space.n, dtype=np.uint8)
        mask[self.current['legal']] = 1
        if not mask.any():
            if not self.current['done']: raise ValueError('Nonterminal state has empty legal set')
            mask[0] = 1  # Terminal observation is never acted on.
        return dict(state=np.concatenate((self.current['features'], self.previous)).astype(np.float32),
                    legal_bits=np.packbits(mask, bitorder='little'))

    def _objectives(self):
        if self.config.get('version') == SingleBattleReward.version:
            self.objective = SingleBattleReward()
            return
        self.objective = TerminalSequenceReward()
        self.progress = ProgressPotentialReward(self.config['progress'])
        self.planning = PlanningRevisitReward(self.config['planning'])
        self.shared = SharedPlanningRevisitReward(self.config['shared'])

    def reset(self, *, seed=None, options=None):
        super().reset(seed=seed)
        if seed is not None: self.rng = np.random.default_rng(seed)
        if self.fixed_order:
            if self.episode >= len(self.rows): raise RuntimeError('Fixed evaluation exhausted')
            index = self.episode
        else:
            counts = np.array([self.results.get(r['task_id'], [0,0]) for r in self.rows])
            rate = (counts[:,1]+1)/(counts[:,0]+2)
            frontier = 4*rate*(1-rate)
            novel = (counts[:,0]<4).astype(float)
            novel = novel/novel.sum() if novel.sum() else np.full(len(self.rows),1/len(self.rows))
            p = .25/len(self.rows)+.25*novel+.5*frontier/frontier.sum()
            # Evaluation assigns one fixed row at a time: never drop its denominator.
            if len(self.rows)>1:
                p *= [r['task_id'] not in self.quarantined_tasks for r in self.rows]
                if p.sum()==0: raise RuntimeError('All training tasks quarantined')
                p /= p.sum()
            index = int(self.rng.choice(len(self.rows), p=p))
        self.row = self.rows[index]
        self.current = self.rules.reset_task(self.row['task']['definition'])
        if digest(self.current['state']) != self.row['task']['initial_state_sha256']:
            raise ValueError('Task initial state binding changed')
        self.trace = []
        self.previous[:] = 0
        self._objectives()
        return self._observation(), {'task_id':self.row['task_id']}

    def _reward(self, before, following, action):
        after = following['state']
        if self.config.get('version') == SingleBattleReward.version:
            raw=self.objective.step(before, after, following['done'])
            scale=self.config.get('scale',1.)
            self.objective.metrics['objective_return']=self.objective.total
            self.objective.metrics['training_return']=self.objective.total*scale
            self.objective.metrics['reward_scale']=scale
            return raw*scale
        return (self.objective.step(before, after, following['reward'], following['done'])
                + self.progress.step(before, after, following['done'])
                + self.planning.step(action, before, after)
                + self.shared.step(action, before, after) - self.config['step'])

    def step(self, action):
        action = int(action)
        if action not in self.current['legal']: raise ValueError('Policy selected illegal global action')
        before = self.current
        try:
            following = self.rules.step(action)
        except Exception as error:
            self._write('failure', {'error':str(error), 'attempted_action':self.rules.actions[action],
                                    'current_state':before['state']})
            raise
        reward = self._reward(before['state'], following, self.rules.actions[action])
        self.total_steps += 1
        self.trace.append(dict(worker_step=self.total_steps, before=before['state'], after=following['state'],
            action=self.rules.actions[action], action_index=action, legal=before['legal'],
            features=before['features'], reward=following['reward'], training_reward=reward))
        self.previous = np.concatenate((self.history_features[action], [reward])).astype(np.float32)
        self.current = following
        info = {'task_id':self.row['task_id'], 'worker_step':self.total_steps}
        if following['done']:
            report = audit(self.trace)
            won = bool(following['state'].get('taskProgress',{}).get('sequenceCompleted'))
            performance = getattr(self.objective, 'metrics', {})
            self._write('episode', {'report':report, 'won':won, 'performance':performance})
            with (self.output/f'worker_{self.worker}_performance.jsonl').open('a',encoding='utf-8') as stream:
                stream.write(json.dumps(dict(task_id=self.row['task_id'],episode=self.episode,**performance))+'\n')
            if report['errors']:
                self.quarantined_tasks.add(self.row['task_id'])
                self.episode += 1
                # Original evidence is already durable. This result is NOT a valid loss
                # for PPO: the runner rejects the entire containing rollout before train().
                performance=dict(performance,won=False,audit_valid=False,
                                 audit_errors=report['errors'])
                info.update(audit_failure=report['errors'],is_success=False,performance=performance)
                return self._observation(),float(reward),True,False,info
            result = self.results.setdefault(self.row['task_id'], [0,0])
            result[0] += 1; result[1] += int(won)
            self.episode += 1
            info.update(episode={'r':sum(t['training_reward'] for t in self.trace), 'l':len(self.trace)}, is_success=won,performance=performance)
        # Native failure/step cap retains the existing task boundary semantics.
        return self._observation(), float(reward), bool(following['done']), False, info

    def _write(self, kind, extra):
        path = self.output/f'worker_{self.worker}_{kind}_{self.episode}.json'
        if path.exists(): raise FileExistsError(path)
        doc = dict(certificate=self.row, trace=self.trace, worker=self.worker,
                   rng_state=self.rng.bit_generator.state, **extra)
        pending = path.with_suffix('.pending')
        pending.write_text(json.dumps(doc,ensure_ascii=False),encoding='utf-8')
        pending.replace(path)

    def snapshot(self):
        return copy.deepcopy(dict(row=self.row, current=self.current, trace=self.trace,
            previous=self.previous, rng=self.rng.bit_generator.state,
            results=self.results, episode=self.episode, total_steps=self.total_steps,
            quarantined_tasks=sorted(self.quarantined_tasks)))

    def restore(self, snapshot):
        self.quarantined_tasks.update(snapshot.get('quarantined_tasks',[]))
        self.row = snapshot['row']; self._objectives()
        current = self.rules.reset_task(self.row['task']['definition'])
        for t in snapshot['trace']:
            if current['state'] != t['before']: raise ValueError('Resume before-state mismatch')
            following = self.rules.step(t['action_index'])
            if following['state'] != t['after'] or following['reward'] != t['reward']:
                raise ValueError('Resume after-state/reward mismatch')
            if self._reward(current['state'], following, t['action']) != t['training_reward']:
                raise ValueError('Resume training reward mismatch')
            current = following
        if current != snapshot['current']: raise ValueError('Resume full protocol mismatch')
        self.current=current;self.trace=snapshot['trace'];self.previous=snapshot['previous']
        self.rng.bit_generator.state=snapshot['rng'];self.results=snapshot['results'];self.episode=snapshot['episode']
        self.total_steps=snapshot['total_steps']

    def close(self):
        if self.current is not None and self.trace and not self.current['done']:
            self._write('partial', {'scope':'unfinished episode retained; not counted as loss or success'})
        self.rules.close()
