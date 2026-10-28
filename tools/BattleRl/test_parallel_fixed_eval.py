import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
import numpy as np
import torch
from sb3_contrib import RecurrentPPO
from sb3_battle_policy import BattleRecurrentPolicy
from test_sb3_battle import TinyMemoryEnv
from parallel_fixed_eval import collect

class FakeEnv:
    def __init__(self,rows,*args,reward_config=None):
        self.rows=rows
        assert reward_config=={'version':'single_battle_v2','scale':.01}
    def reset(self):
        self.t=0;self.actions=[]
        self.limit=2+int(self.rows[0]['task_id'][:8],16)%5
        return self.obs(),{}
    def obs(self):return dict(state=np.full(5,.1*self.t,np.float32),legal_bits=np.array([5],np.uint8))
    def step(self,action):
        assert action in (0,2)
        self.actions.append(action);self.t+=1
        return self.obs(),float(action==2),self.t==self.limit,False,dict(is_success=True,performance={'actions':self.actions.copy()})
    def close(self):pass

class Gate(unittest.TestCase):
    def test_task_rng_and_memory_independent_of_parallelism(self):
        torch.set_num_threads(1)
        model=RecurrentPPO(BattleRecurrentPolicy,TinyMemoryEnv(),device='cuda',seed=3,
            policy_kwargs=dict(descriptors=np.eye(4,dtype=np.float32)))
        model.policy.set_training_mode(False)
        model.reward_config={'version':'single_battle_v2','scale':.01}
        rows=[{'task_id':f'{i:064x}'} for i in range(12)]
        # Vary the seed prefix and episode lengths, including partial final batch.
        rows=[{'task_id':f'{i:08x}'+r['task_id'][8:]} for i,r in enumerate(rows)]
        with tempfile.TemporaryDirectory() as d,patch('parallel_fixed_eval.BattleEnv',FakeEnv):
            a=collect(model,rows,None,Path(d),1)
            b=collect(model,rows,None,Path(d),3)
        self.assertEqual(a,b)

if __name__=='__main__':unittest.main()
