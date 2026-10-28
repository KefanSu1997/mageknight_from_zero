"""Optional PPO objective shaping; never changes the formal rule environment."""


class CompletionReward:
    def __init__(self, enabled=False):
        self.enabled = enabled
        self.reset()

    def reset(self):
        self.whole_wins = 0

    def step(self, before, after, raw_reward, done):
        # A kill or last live target alone is not a formally completed battle.
        if (after.get('phase') == 'End' and after.get('won')
                and not before.get('won')):
            self.whole_wins += 1
        if not self.enabled:
            return raw_reward
        reward = raw_reward - 8 * (after['kills'] - before['kills'])
        if done and self.whole_wins == 0:
            # Replace the native no-kill -5, while retaining every other penalty.
            reward += 5 if after['kills'] == 0 else 0
            reward -= 10
        return reward
