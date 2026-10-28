import tempfile
import unittest
import threading
import time
from pathlib import Path
from unittest.mock import patch
import numpy as np
import torch
from sb3_contrib import RecurrentPPO
from sparse_battle_policy import SparseBattleRecurrentPolicy
from test_sb3_battle import TinyMemoryEnv
from test_parallel_sparse_eval import FakeEnv
import parallel_combat_eval as old
import parallel_combat_eval_fast as new

class ConcurrentEnv(FakeEnv):
    lock=threading.Lock()
    busy=0
    peak=0
    def reset(self):
        with self.lock:
            type(self).busy+=1
            type(self).peak=max(type(self).peak,type(self).busy)
        try:
            time.sleep(.01)
            return super().reset()
        finally:
            with self.lock:type(self).busy-=1

class Gate(unittest.TestCase):
    def test_old_new_task_rng_actions_and_concurrent_reset(self):
        torch.set_num_threads(1)
        model=RecurrentPPO(SparseBattleRecurrentPolicy,TinyMemoryEnv(),device='cuda',seed=3,
            policy_kwargs=dict(descriptors=np.eye(4,dtype=np.float32)))
        model.policy.set_training_mode(False)
        model.reward_config={'version':'single_battle_v2','scale':.01}
        rows=[{'task_id':f'{i:08x}'+('0'*56)} for i in range(12)]
        with tempfile.TemporaryDirectory() as d,patch.object(old,'BattleEnv',FakeEnv),patch.object(new,'BattleEnv',ConcurrentEnv):
            reference=old.collect(model,rows,None,Path(d),3)
            for workers in (1,3):
                result=new.collect(model,rows,None,Path(d),workers)
                self.assertEqual(reference,result)
            self.assertGreater(ConcurrentEnv.peak,1)

if __name__=='__main__':unittest.main()
