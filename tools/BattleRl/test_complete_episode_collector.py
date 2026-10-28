import tempfile,unittest
from pathlib import Path
import numpy as np
import torch
from sb3_contrib import RecurrentPPO
from stable_baselines3.common.callbacks import BaseCallback
from sb3_battle_vec import ThreadedRulesVecEnv
from test_sb3_battle import TinyMemoryEnv
from sparse_battle_policy import SparseBattleRecurrentPolicy
from complete_episode_collector import CompleteEpisodeCollector

class Env(TinyMemoryEnv):
    serial=0
    def reset(self,**kw):
        self.t=0;self.number=Env.serial;Env.serial+=1
        self.limit=128 if self.number%4==0 else 3+self.number%7
        return self._observation(),{}
    def _observation(self):return dict(state=np.zeros(5,np.float32),legal_bits=np.array([5],np.uint8))
    def step(self,action):
        self.t+=1;done=self.t==self.limit;bad=self.number==3
        info={'task_id':str(self.number),'performance':{'won':False,'steps':self.t},'episode':{'r':-1.,'l':self.t}}
        if done and bad:info['audit_failure']=['injected invalid episode']
        return self._observation(),(-1000. if bad else -1.) if done else 0.,done,False,info
class Callback(BaseCallback):
    def _on_step(self):return True
class Model(CompleteEpisodeCollector,RecurrentPPO):pass
class Gate(unittest.TestCase):
    def test_complete128_isolation_and_terminal_returns(self):
        torch.set_num_threads(1);Env.serial=0
        vec=ThreadedRulesVecEnv([Env for _ in range(32)])
        try:
            m=Model(SparseBattleRecurrentPolicy,vec,device='cuda',n_steps=128,batch_size=128,n_epochs=1,gamma=1.,gae_lambda=1.,policy_kwargs={'descriptors':np.eye(4,dtype=np.float32)})
            _,cb=m._setup_learn(1,Callback());m.completed_updates=40;Env.serial=0
            with tempfile.TemporaryDirectory() as d:
                m.output=Path(d)
                m.collect_rollouts(vec,cb,m.rollout_buffer,128)
                import json
                row=json.loads((m.output/'episode_batches.jsonl').read_text())
                self.assertEqual(row['accepted_episodes'],128)
                self.assertEqual(row['rejected_episodes'],1)
                self.assertEqual(max(row['lengths']),128)
                np.testing.assert_allclose(m.rollout_buffer.returns,-1.,atol=2e-6)
                before=[p.detach().clone() for p in m.policy.parameters()]
                m.train()
                self.assertTrue(any(not torch.equal(a,b) for a,b in zip(before,m.policy.parameters())))
        finally:vec.close()
if __name__=='__main__':unittest.main()
