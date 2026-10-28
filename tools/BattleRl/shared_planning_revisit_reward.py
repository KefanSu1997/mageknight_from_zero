"""Optional training-only revisit cost; original selection rules and raw reward remain."""
import math


class SharedPlanningRevisitReward:
    def __init__(self,cost=0.):
        if not math.isfinite(cost) or cost<0:raise ValueError('Nonnegative finite shared planning cost required')
        self.cost=float(cost);self.seen=set()

    def step(self,action,before,after):
        if not self.cost:return 0.
        if (action['Kind'] not in ('sharedArm','sharedCancel')
                or before['phase']!=after['phase']):
            self.seen.clear();return 0.
        previous=(before['phase'],before['sharedSkills']['enhancementColor'])
        following=(after['phase'],after['sharedSkills']['enhancementColor'])
        if not self.seen:self.seen.add(previous)
        revisit=following in self.seen;self.seen.add(following)
        return -self.cost if revisit else 0.
