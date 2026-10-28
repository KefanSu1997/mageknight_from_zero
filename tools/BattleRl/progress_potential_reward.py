"""Optional discounted progress shaping. Terminal potential is always zero."""
import math


class ProgressPotentialReward:
    def __init__(self,scale=0.,gamma=.995):
        if not math.isfinite(scale) or scale<0 or not 0<gamma<=1:
            raise ValueError('Finite nonnegative scale and valid discount required')
        self.scale,self.gamma=float(scale),float(gamma)

    def potential(self,state,terminal=False):
        if terminal or self.scale==0: return 0.
        progress=state.get('taskProgress')
        if not progress or progress['groupCount']<=0 or not 0<=progress['encounterIndex']<=progress['groupCount']:
            raise ValueError('Declared public sequence progress required')
        completed=progress['encounterIndex']
        # A completed current battle awaits formal turn cleanup before cursor advancement.
        if state['phase']=='End' and state['won']: completed+=1
        return self.scale*min(completed,progress['groupCount'])/progress['groupCount']

    def step(self,before,after,done):
        return self.gamma*self.potential(after,done)-self.potential(before)
