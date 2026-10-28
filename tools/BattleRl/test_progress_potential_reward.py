import unittest
from progress_potential_reward import ProgressPotentialReward


def state(cursor=0,phase='Outside',won=False):
    return dict(phase=phase,won=won,taskProgress=dict(encounterIndex=cursor,groupCount=2))


class PotentialTests(unittest.TestCase):
    def test_full_and_failed_discounted_returns_are_unchanged(self):
        reward=ProgressPotentialReward(20)
        for states in ([state(),state(0,'End',True),state(1),state(1,'End',True),state(2)],
                       [state(),state(0,'End',True),state(1),state(1)]):
            value=sum(reward.gamma**i*reward.step(a,b,i==len(states)-2)
                      for i,(a,b) in enumerate(zip(states,states[1:])))
            self.assertAlmostEqual(value,0)

    def test_no_repeat_win_farming_and_disabled_identity(self):
        reward=ProgressPotentialReward(20);s=state(0,'End',True)
        self.assertLess(reward.step(s,s,False),0)
        self.assertEqual(ProgressPotentialReward().step({}, {},True),0)
        with self.assertRaises(ValueError):ProgressPotentialReward(-1)


if __name__=='__main__':unittest.main()
