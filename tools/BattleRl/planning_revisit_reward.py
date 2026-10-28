"""Optional training-only cost for revisiting a consecutive damage planning configuration."""
import json
import math


class PlanningRevisitReward:
    def __init__(self,cost=0.):
        if not math.isfinite(cost) or cost<0: raise ValueError('Nonnegative finite planning cost required')
        self.cost=float(cost)
        self.seen=set()

    def step(self,action,before,after):
        if self.cost==0: return 0.
        if action['Kind']!='unitDamage' or before['phase']!='AssignDamage' or after['phase']!='AssignDamage':
            self.seen.clear()
            return 0.
        # Public ordered reservations only. Never use hidden deck order or episode counters.
        key=lambda state:json.dumps(state['unitDamagePlan'],sort_keys=True,separators=(',',':'))
        if not self.seen: self.seen.add(key(before))
        following=key(after)
        repeated=following in self.seen
        self.seen.add(following)
        return -self.cost if repeated else 0.
