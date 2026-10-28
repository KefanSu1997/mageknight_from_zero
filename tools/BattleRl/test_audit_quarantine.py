import json
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import Mock, patch
import numpy as np
from sb3_battle_env import BattleEnv
from train_sb3_recurrent import RecordedRecurrentPPO


class QuarantineTests(unittest.TestCase):
    def test_actual_failed_episode_is_durable_and_terminal_not_exception(self):
        path=Path('H:/MageKnight_RL_Runs/20261012_reward_v2_scaled001_100/episodes/worker_9_episode_15.json')
        doc=json.loads(path.read_text(encoding='utf-8'));t=doc['trace'][-1]
        with tempfile.TemporaryDirectory() as folder:
            env=BattleEnv.__new__(BattleEnv)
            env.output=Path(folder);env.worker=9;env.episode=15;env.row=doc['certificate']
            env.rng=np.random.default_rng(1);env.trace=doc['trace'][:-1];env.total_steps=t['worker_step']-1
            env.current=dict(state=t['before'],legal=[t['action_index']],features=t['features'])
            actions={t['action_index']:t['action']}
            env.rules=SimpleNamespace(actions=actions,step=lambda _:dict(state=t['after'],done=True,reward=t['reward']))
            env.history_features={t['action_index']:np.zeros(2)}
            env.objective=SimpleNamespace(metrics=doc['performance'])
            env._reward=lambda *args:t['training_reward'];env._observation=lambda:{}
            env.results={}
            _,_,done,_,info=env.step(t['action_index'])
            self.assertTrue(done);self.assertIn('audit_failure',info)
            self.assertFalse(info['is_success']);self.assertFalse(info['performance']['audit_valid'])
            self.assertNotIn('episode',info);self.assertEqual(env.results,{})
            saved=json.loads((env.output/'worker_9_episode_15.json').read_text())
            self.assertEqual(saved['report']['errors'],doc['report']['errors'])
            self.assertEqual(saved['trace'],doc['trace'])
            self.assertEqual(env.episode,16)

    def test_rejected_rollout_does_not_call_upstream_or_increment(self):
        with tempfile.TemporaryDirectory() as folder:
            model=SimpleNamespace(rollout_audit_failures=[{'task_id':'bad'}],rollout_tag='5_step20480',
                num_timesteps=24576,completed_updates=5,n_steps=128,n_envs=32,output=Path(folder))
            with patch('train_sb3_recurrent.render'),patch('sb3_contrib.RecurrentPPO.train') as upstream:
                RecordedRecurrentPPO.train(model)
            upstream.assert_not_called();self.assertEqual(model.completed_updates,5)
            result=json.loads((model.output/'rejected_rollouts.jsonl').read_text())
            self.assertEqual(result['rejected_transitions'],4096)
            self.assertFalse(result['optimizer_updated'])

if __name__=='__main__':unittest.main()
