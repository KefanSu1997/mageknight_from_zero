"""Optional complete-task objective, separate from native game rewards."""


class TerminalSequenceReward:
    def step(self,before,after,raw_reward,done):
        # Neutralize native kill deltas, including counter reset at a new group.
        reward=raw_reward-10*(after['kills']-before['kills'])
        if after['phase']=='End' and after['won'] and not before['won']:
            reward-=20
        completed=bool(after.get('taskProgress',{}).get('sequenceCompleted'))
        previously_completed=bool(before.get('taskProgress',{}).get('sequenceCompleted'))
        if completed and not previously_completed:
            reward+=20
        elif done:
            if after['kills']==0:
                reward+=5  # Replace only native no-kill terminal penalty.
            reward-=10
        return reward
