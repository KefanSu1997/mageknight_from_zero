import copy
import unittest
from single_battle_reward import SingleBattleReward

def state(ids=(),kills=0,won=False):
    return dict(zones={'hand':[dict(instance=i,wound=True) for i in ids]},
        units=[],enemies=[{},{}],kills=kills,totalWounds=len(ids),
        taskProgress={'sequenceCompleted':won},stopReason='')

class RewardTests(unittest.TestCase):
    def test_positive_kill_and_no_healing_refund(self):
        r=SingleBattleReward();a=state([0]);b=state([0],1)
        self.assertGreater(r.step(a,b,False),0)
        c=state([0,1],1);self.assertLess(r.step(b,c,False),0)
        d=state([],1);self.assertLess(r.step(c,d,False),0)
        self.assertEqual(r.wounds,1)
        r.step(d,state([1],1),False)
        self.assertEqual(r.wounds,1)

    def test_win_dominates_any_loss_and_initial_cost_excluded(self):
        for won in (False,True):
            r=SingleBattleReward();a=state(range(100))
            for i in range(256):
                b=state(range(101+i),min(i,2),won and i==255)
                r.step(a,b,i==255);a=b
            self.assertEqual(r.wounds,256)
            self.assertGreaterEqual(r.total,75) if won else self.assertLessEqual(r.total,-90)

    def test_replay_reconstructs_reward_state(self):
        a=state();b=state([3],1);c=state([3],2,True)
        r=SingleBattleReward();s=SingleBattleReward()
        for before,after,done in ((a,b,False),(b,c,True)):
            self.assertEqual(r.step(before,after,done),s.step(copy.deepcopy(before),copy.deepcopy(after),done))
        self.assertEqual(r.metrics,s.metrics)

if __name__=='__main__':unittest.main()
