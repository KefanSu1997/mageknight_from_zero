"""Single-battle episodes begin after the formal native fight operation."""
import copy
from sb3_battle_env import BattleEnv as OutsideBattleEnv


class BattleEnv(OutsideBattleEnv):
    entry_version = 'formal_fight_start_v1'

    def _enter(self, current):
        candidates=[i for i in current['legal'] if self.rules.actions[i]['Kind']=='fight']
        if len(candidates)!=1:
            raise ValueError('Task lacks a unique legal formal fight entry')
        index=candidates[0]
        following=self.rules.step(index)
        if following['done'] or not following['state']['inBattle']:
            raise ValueError('Formal fight did not enter a live battle')
        self.entry=dict(version=self.entry_version,before=current,action_index=index,
                        action=self.rules.actions[index],after=following)
        return following

    def reset(self, **kwargs):
        _,info=super().reset(**kwargs)
        self.current=self._enter(self.current)
        # Entry is environment setup, not an actor action or a PPO reward.
        self.previous[:]=0
        return self._observation(),info

    def _write(self, kind, extra):
        super()._write(kind,dict(extra,formal_entry=getattr(self,'entry',None)))

    def snapshot(self):
        snapshot=super().snapshot()
        snapshot['formal_entry']=copy.deepcopy(self.entry)
        return snapshot

    def restore(self, snapshot):
        if snapshot.get('formal_entry',{}).get('version')!=self.entry_version:
            raise ValueError('Cannot resume an outside-start episode as combat-start')
        self.quarantined_tasks.update(snapshot.get('quarantined_tasks',[]))
        self.row=snapshot['row'];self._objectives()
        current=self._enter(self.rules.reset_task(self.row['task']['definition']))
        if self.entry!=snapshot['formal_entry']:raise ValueError('Formal fight replay mismatch')
        for t in snapshot['trace']:
            if current['state']!=t['before']:raise ValueError('Resume before-state mismatch')
            following=self.rules.step(t['action_index'])
            if following['state']!=t['after'] or following['reward']!=t['reward']:
                raise ValueError('Resume after-state/reward mismatch')
            if self._reward(current['state'],following,t['action'])!=t['training_reward']:
                raise ValueError('Resume training reward mismatch')
            current=following
        if current!=snapshot['current']:raise ValueError('Resume full protocol mismatch')
        self.current=current;self.trace=snapshot['trace'];self.previous=snapshot['previous']
        self.rng.bit_generator.state=snapshot['rng'];self.results=snapshot['results']
        self.episode=snapshot['episode'];self.total_steps=snapshot['total_steps']
