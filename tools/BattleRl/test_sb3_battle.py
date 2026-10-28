"""Mask, temporal memory, upstream PPO, and save/load gates on real PyTorch."""
import tempfile
import hashlib
from pathlib import Path
import unittest
import gymnasium as gym
import numpy as np
import torch
from sb3_contrib import RecurrentPPO
from sb3_contrib.common.recurrent.type_aliases import RNNStates
from sb3_battle_policy import BattleRecurrentPolicy


class TinyMemoryEnv(gym.Env):
    observation_space=gym.spaces.Dict({'state':gym.spaces.Box(-1,1,(5,),np.float32),
                                     'legal_bits':gym.spaces.Box(0,255,(1,),np.uint8)})
    action_space=gym.spaces.Discrete(4)
    def reset(self,seed=None,options=None):
        super().reset(seed=seed);self.t=0;return self.obs(),{}
    def obs(self):
        return dict(state=np.full(5,.1*self.t,np.float32),legal_bits=np.array([5],np.uint8))
    def step(self,action):
        assert action in [0,2]
        self.t+=1
        return self.obs(),float(action==2),self.t==5,False,{}


class Gates(unittest.TestCase):
    def test_external_descriptor_binding(self):
        with tempfile.TemporaryDirectory() as folder:
            path=Path(folder)/'descriptors.npy'
            np.save(path,np.eye(4,dtype=np.float32))
            signature=hashlib.sha256(path.read_bytes()).hexdigest()
            model=RecurrentPPO(BattleRecurrentPolicy,TinyMemoryEnv(),device='cpu',n_steps=8,
                policy_kwargs=dict(descriptor_file=str(path),descriptor_sha256=signature))
            self.assertNotIn('descriptors',model.policy.state_dict())
            model.save(Path(folder)/'model')
            restored=RecurrentPPO.load(Path(folder)/'model',device='cpu')
            torch.testing.assert_close(model.policy.descriptors,restored.policy.descriptors)
            np.save(path,np.zeros((4,4),dtype=np.float32))
            with self.assertRaisesRegex(ValueError,'binding changed'):
                RecurrentPPO.load(Path(folder)/'model',device='cpu')

    def test_mask_sequence_update_restore(self):
        device='cuda' if torch.cuda.is_available() else 'cpu'
        model=RecurrentPPO(BattleRecurrentPolicy,TinyMemoryEnv(),device=device,n_steps=16,
            batch_size=8,n_epochs=2,seed=7,policy_kwargs=dict(descriptors=np.eye(4,dtype=np.float32)))
        policy=model.policy
        obs=policy.obs_to_tensor(TinyMemoryEnv().reset()[0])[0]
        zeros=tuple(torch.zeros(1,1,256,device=device) for _ in range(2))
        start=torch.ones(1,device=device);cont=torch.zeros(1,device=device)
        with torch.no_grad():
            dist,memory=policy.get_distribution(obs,zeros,start)
            baseline=dist.distribution.probs.clone()
            self.assertEqual(float(baseline[0,1]+baseline[0,3]),0.)
            dist2,_=policy.get_distribution(obs,memory,cont)
            self.assertFalse(torch.equal(baseline,dist2.distribution.probs))
            dist3,_=policy.get_distribution(obs,memory,start)
            torch.testing.assert_close(baseline,dist3.distribution.probs)
        before={k:v.clone() for k,v in policy.named_parameters()}
        model.learn(32)
        self.assertTrue(any(not torch.equal(before[k],v) for k,v in policy.named_parameters() if 'lstm' in k))
        self.assertTrue(all(torch.isfinite(v).all() for v in policy.parameters()))
        with tempfile.TemporaryDirectory() as folder:
            model.save(folder+'/model')
            restored=RecurrentPPO.load(folder+'/model',device=device)
            for k,v in policy.state_dict().items():torch.testing.assert_close(v,restored.policy.state_dict()[k],rtol=0,atol=0)
            self.assertEqual(len(policy.optimizer.state),len(restored.policy.optimizer.state))
        print('Verified upstream recurrent PPO, masked legal support, history/reset, LSTM updates and checkpoint on',device)


if __name__=='__main__':unittest.main()
