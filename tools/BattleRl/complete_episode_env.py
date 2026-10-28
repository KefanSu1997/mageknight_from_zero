"""Explicit 128-action task horizon, without changing native rule state."""
import json
from combat_start_env import BattleEnv as BaseEnv
from trajectory_audit import audit

class BattleEnv(BaseEnv):
    horizon=128
    def reset(self, **kwargs):
        self.episode_closed=False
        return super().reset(**kwargs)
    def _reward(self,before,following,action):
        task_terminal=following['done'] or self.objective.steps+1>=self.horizon
        return super()._reward(before,dict(following,done=task_terminal),action)
    def step(self,action):
        obs,reward,done,truncated,info=super().step(action)
        if done:
            self.episode_closed=True
            return obs,reward,done,truncated,info
        if len(self.trace)<self.horizon:return obs,reward,done,truncated,info
        report=audit(self.trace)
        performance=dict(self.objective.metrics,won=False,stop_reason='task_action_limit_128',timeout=True)
        self._write('episode',dict(report=report,won=False,performance=performance,task_terminal=True,native_terminal=False))
        self.episode+=1;self.episode_closed=True
        if report['errors']:
            self.quarantined_tasks.add(self.row['task_id'])
            performance.update(audit_valid=False,audit_errors=report['errors'])
            info['audit_failure']=report['errors']
        else:
            result=self.results.setdefault(self.row['task_id'],[0,0]);result[0]+=1
        info.update(is_success=False,performance=performance,
                    episode={'r':sum(t['training_reward'] for t in self.trace),'l':len(self.trace)})
        # The task definition ends here: no time-limit value bootstrap.
        return obs,reward,True,False,info
    def close(self):
        if getattr(self,'episode_closed',False):self.rules.close()
        else:super().close()
