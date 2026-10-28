"""One battle, complete operation parameters, original micro-step audit evidence."""
import copy
import os
import json
import gymnasium as gym
import numpy as np
from combat_start_env import BattleEnv as BaseEnv
from atomic_action_registry import registry
from trajectory_audit import audit

class BattleEnv(BaseEnv):
    entry_version='formal_single_battle_atomic_v1'
    horizon=128

    def __init__(self,*args,**kwargs):
        self.support_id=0;self.choices=None;self.micro_trace=[];self.closed_episode=False
        super().__init__(*args,**kwargs)
        self.key_to_id={a['Key']:i for i,a in enumerate(self.rules.actions)}
        self.observation_space.spaces['support_id']=gym.spaces.Box(0,2**24-1,(1,),np.float32)
        # Retain one dummy byte for upstream compact buffer compatibility.
        self.observation_space.spaces['legal_bits']=gym.spaces.Box(0,255,(1,),np.uint8)
        self.registry=registry(os.environ['BATTLE_RL_ATOMIC_REGISTRY'])

    def _observation(self):
        return dict(state=np.concatenate((self.current['features'],self.previous)).astype(np.float32),
                    legal_bits=np.ones(1,dtype=np.uint8),support_id=np.array([self.support_id],np.float32))

    def _choices(self):
        if self.current['done']:
            self.choices=[];self.support_id=0;return
        self.choices=self.rules.call(op='atomicChoices')['choices']
        if not self.choices:raise RuntimeError('No complete formal operation; preserve task as interface failure')
        if len(self.choices)>self.action_space.n:raise RuntimeError('Atomic support exceeds declared action space; no truncation allowed')
        sequences=[[self.key_to_id[a['Key']] for a in seq] for seq in self.choices]
        self.support_id=self.registry.add(sequences)

    def reset(self,**kwargs):
        self.support_id=0;self.micro_trace=[];self.closed_episode=False;self.interface_failure=None
        _,info=super().reset(**kwargs)
        try:self._choices()
        except Exception as error:self.interface_failure=str(error);self.support_id=0;self.choices=[]
        return self._observation(),info

    def _invalid(self,error,sequence=None):
        errors=[dict(rule='atomic_adapter_execution',detail=str(error))]
        self._write('failure',dict(error=str(error),attempted_sequence=sequence,current_state=self.current,
                                  classification='unclassified rule/adapter failure; not an unsolvable task'))
        self.quarantined_tasks.add(self.row['task_id']);self.episode+=1;self.closed_episode=True
        self.support_id=0;self.choices=[]
        performance=dict(getattr(self.objective,'metrics',{}))
        performance.update(won=False,audit_valid=False,audit_errors=errors,
            kills=performance.get('kills',0),new_wound_cards=performance.get('new_wound_cards',0),
            new_unit_wounds=performance.get('new_unit_wounds',0),steps=len(self.trace),timeout=False)
        return self._observation(),-1.,True,False,dict(task_id=self.row['task_id'],worker_step=self.total_steps,
            audit_failure=errors,is_success=False,performance=performance)

    def step(self,action):
        if self.interface_failure:return self._invalid(self.interface_failure)
        action=int(action)
        if not 0<=action<len(self.choices):raise ValueError('Illegal complete operation')
        before=self.current;sequence=self.choices[action];support=self.support_id
        try:following=self.rules.call(op='atomicStep',choice=action)
        except Exception as error:return self._invalid(error,sequence)
        micro=following.pop('micro')
        atomic_error=following.pop('atomic_error',None)
        if atomic_error:
            self.micro_trace.extend(micro);self.current=following
            return self._invalid(atomic_error,sequence)
        for t in micro:
            t.update(action_index=self.key_to_id[t['action']['Key']],reward=0.,training_reward=0.)
        if following['done'] and micro[-1]['action']['Kind']=='nextTurn':
            micro[-1]['task_boundary']='formal_single_battle_v1'
        self.micro_trace.extend(micro)
        timeout=not following['done'] and len(self.trace)+1>=self.horizon
        terminal=following['done'] or timeout
        reward=self._reward(before['state'],dict(following,done=terminal),sequence[-1])
        self.total_steps+=1
        self.trace.append(dict(worker_step=self.total_steps,before=before['state'],after=following['state'],
            action=sequence[-1],action_index=action,support_id=support,sequence=sequence,
            micro=micro,reward=following['reward'],training_reward=reward))
        components=[self.key_to_id[a['Key']] for a in sequence]
        self.previous=np.concatenate((np.mean(self.history_features[components],axis=0),[reward])).astype(np.float32)
        self.current=following
        info=dict(task_id=self.row['task_id'],worker_step=self.total_steps)
        if terminal:
            report=audit(self.micro_trace)
            won=bool(following['state']['taskProgress']['sequenceCompleted'])
            performance=dict(self.objective.metrics,won=won,timeout=timeout,
                stop_reason='task_atomic_action_limit_128' if timeout else following['state']['stopReason'],
                native_operations=len(self.micro_trace),policy_operations=len(self.trace))
            self._write('episode',dict(report=report,won=won,performance=performance,
                task_terminal=True,native_terminal=following['done']))
            self.closed_episode=True;self.episode+=1
            if report['errors']:
                self.quarantined_tasks.add(self.row['task_id'])
                won=False
                performance.update(won=False,audit_valid=False,audit_errors=report['errors'])
                info['audit_failure']=report['errors']
            else:
                result=self.results.setdefault(self.row['task_id'],[0,0]);result[0]+=1;result[1]+=int(won)
            info.update(is_success=won,performance=performance,
                episode=dict(r=sum(t['training_reward'] for t in self.trace),l=len(self.trace)))
            self.choices=[];self.support_id=0
        else:
            try:self._choices()
            except Exception as error:return self._invalid(error)
        return self._observation(),reward,terminal,False,info

    def _write(self,kind,extra):
        super()._write(kind,dict(extra,action_contract=self.entry_version,micro_trace=self.micro_trace))

    def snapshot(self):
        result=super().snapshot()
        result.update(micro_trace=copy.deepcopy(self.micro_trace),closed_episode=self.closed_episode,support_id=self.support_id)
        return result

    def restore(self,snapshot):
        self.row=snapshot['row'];self._objectives()
        self.current=self._enter(self.rules.reset_task(self.row['task']['definition']))
        if self.entry!=snapshot['formal_entry']:raise ValueError('Atomic entry replay mismatch')
        for t in snapshot['trace']:
            choices=self.rules.call(op='atomicChoices')['choices']
            if choices[t['action_index']]!=t['sequence']:raise ValueError('Atomic support replay mismatch')
            following=self.rules.call(op='atomicStep',choice=t['action_index'])
            if following.pop('atomic_error',None):raise ValueError('Atomic replay failed')
            if following['state']!=t['after']:raise ValueError('Atomic state replay mismatch')
            terminal=following['done'] or self.objective.steps+1>=self.horizon
            if self._reward(self.current['state'],dict(following,done=terminal),t['action'])!=t['training_reward']:
                raise ValueError('Atomic reward replay mismatch')
            following.pop('micro');self.current=following
        for field in ('trace','previous','results','episode','total_steps','micro_trace','closed_episode','support_id'):
            setattr(self,field,snapshot[field])
        self.rng.bit_generator.state=snapshot['rng']
        self.interface_failure=None
        if not self.closed_episode:self._choices()
        else:self.choices=[];self.support_id=0

    def close(self):
        if self.current is not None and self.trace and not self.closed_episode:self._write('partial',{})
        self.rules.close()
