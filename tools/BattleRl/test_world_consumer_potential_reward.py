import copy
import unittest
from world_consumer_potential_reward import WorldConsumerPotentialReward


class WorldConsumerPotentialTests(unittest.TestCase):
    def state(self, q=0, r=0):
        return dict(skills=['hero_skill_pool_005:05'], world=dict(enabled=True, q=q, r=r,
            inNegotiation=False, loyaltyBound=None, cells=[dict(q=2, r=-3, site='Village')]))

    def test_terminal_success_and_failure_have_same_telescoping_delta(self):
        reward = WorldConsumerPotentialReward(5.)
        states = [self.state(), self.state(0, -1), self.state(1, -2), self.state(2, -3)]
        states[-1]['world']['inNegotiation'] = True
        for success in (True, False):
            end = copy.deepcopy(states[-1]); end['won'] = success
            sequence = states + [end]
            shaped = sum(reward.gamma ** i * reward.step(a, b, i == len(sequence) - 2)
                         for i, (a, b) in enumerate(zip(sequence, sequence[1:])))
            self.assertAlmostEqual(shaped, 0., places=12)

    def test_disable_unowned_nonworld_and_repeated_state_not_positive_loop(self):
        state = self.state(2, -3)
        self.assertEqual(WorldConsumerPotentialReward().step(state, state, False), 0.)
        self.assertLess(WorldConsumerPotentialReward(5.).step(state, state, False), 0.)
        state['skills'] = []
        self.assertEqual(WorldConsumerPotentialReward.potential(state), 0.)
        state['skills'] = ['hero_skill_pool_005:05']; state['world']['enabled'] = False
        self.assertEqual(WorldConsumerPotentialReward.potential(state), 0.)

    def test_no_audit_fields_or_future_groups_influence_potential(self):
        state = self.state(1, -2)
        changed = copy.deepcopy(state)
        changed.update(auditDrawOrder=['secret'], seed=999, future_enemy_groups=['secret'])
        self.assertEqual(WorldConsumerPotentialReward.potential(state), WorldConsumerPotentialReward.potential(changed))

    def test_actual_public_preparation_bridges_sparse_navigation_without_terminal_bonus(self):
        reward = WorldConsumerPotentialReward(5.)
        before = self.state(); after = copy.deepcopy(before); after['movement'] = 2
        self.assertAlmostEqual(reward.step(before, after, False), 5 * .995 / 6)
        self.assertAlmostEqual(reward.step(before, after, False) + reward.gamma * reward.step(after, after, True), 0., places=12)
        after['inBattle'] = True
        self.assertEqual(reward.potential(after), 0.)


if __name__ == '__main__':
    unittest.main()
