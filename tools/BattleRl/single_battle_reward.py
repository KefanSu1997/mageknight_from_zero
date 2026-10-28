"""Bounded, undiscounted single-battle objective; physical costs tracked separately."""
class SingleBattleReward:
    version = 'single_battle_v2'

    def __init__(self):
        self.steps = self.kills = self.wounds = self.unit_wounds = 0
        self.seen = None
        self.total = 0.

    @staticmethod
    def wound_ids(state):
        return {c['instance'] for cards in state['zones'].values() for c in cards if c['wound']}

    def step(self, before, after, done):
        if self.seen is None:
            self.seen = self.wound_ids(before)
        ids = self.wound_ids(after)
        added = len(ids-self.seen)
        self.seen |= ids
        old_cost = 20*self.wounds/(self.wounds+5)
        self.wounds += added
        new_cost = 20*self.wounds/(self.wounds+5)
        prior_units = {u['id']:u['wounds'] for u in before['units']}
        self.unit_wounds += sum(max(0,u['wounds']-prior_units.get(u['id'],u['wounds'])) for u in after['units'])
        new_kills = max(0,after['kills']-before['kills'])
        denominator = max(1, len(before['enemies']))
        old_kill_reward = min(10.,getattr(self,'kill_reward',0.))
        self.kill_reward = min(10.,old_kill_reward+10*new_kills/denominator)
        self.kills += new_kills
        # Saturating budget also protects return ordering if native horizon changes.
        step_cost = min(5.,(self.steps+1)*5/256)-min(5.,self.steps*5/256)
        self.steps += 1
        won = bool(after.get('taskProgress',{}).get('sequenceCompleted'))
        terminal = (100. if won else -100.) if done else 0.
        reward = terminal+self.kill_reward-old_kill_reward-(new_cost-old_cost)-step_cost
        self.total += reward
        self.metrics = dict(won=won,kills=self.kills,new_wound_cards=self.wounds,
            new_unit_wounds=self.unit_wounds,remaining_wound_cards=after['totalWounds'],
            steps=self.steps,training_return=self.total,stop_reason=after.get('stopReason'),
            reward_version=self.version)
        if done:
            assert (self.total >= 75-1e-8) if won else (self.total <= -90+1e-8)
        return reward
