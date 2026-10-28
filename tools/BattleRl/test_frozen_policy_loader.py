import json
import os
from pathlib import Path
import unittest
import numpy as np
from evaluate_recurrent_ppo import load_frozen_policy,parameter_hash
from train_ppo import Rules

ROOT=Path(__file__).resolve().parents[2]
DLL=ROOT/'tmp/battle_rl_explicit_build_tasks/BattleRl.dll'
POOL=ROOT/'AutomationOutputs/BattleRl/20261007_nine_task_data_bound_pool.json'
CHECKPOINT=ROOT/'AutomationOutputs/BattleRl/20261007_recurrent_current_data_binding/checkpoint.npz'


class FrozenLoaderTests(unittest.TestCase):
    def test_actual_saved_parameters_and_training_pool_guard(self):
        os.environ.update(BATTLE_RL_DLL=str(DLL),BATTLE_RL_PROTOCOL='--serve-journey-artifacts',
                          BATTLE_RL_NATIVE_PAYMENTS='1',BATTLE_RL_NATIVE_CONVERSIONS='1',
                          BATTLE_RL_NATIVE_ENERGY_TARGETS='1',BATTLE_RL_ORIGINAL_OPENINGS='1')
        rules=Rules()
        try:
            certificate=json.loads(POOL.read_text())['tasks'][0]
            initial=rules.reset_task(certificate['task']['definition'])
            policy,replay=load_frozen_policy(CHECKPOINT,POOL,DLL,rules,initial)
            self.assertEqual(replay['next_episode'],101)
            with np.load(CHECKPOINT,allow_pickle=False) as arrays:
                for key,value in policy.parameters().items():
                    np.testing.assert_array_equal(value,arrays['parameter/'+key])
            before=parameter_hash(policy)
            probability,_,_=policy.step(initial['features'],initial['legal'])
            self.assertAlmostEqual(float(probability.sum()),1.)
            self.assertEqual(before,parameter_hash(policy))
            old_pool=ROOT/'AutomationOutputs/BattleRl/20261007_discovery_two_groups/solvable_pool.json'
            with self.assertRaises(ValueError):
                load_frozen_policy(CHECKPOINT,old_pool,DLL,rules,initial)
        finally:
            rules.close()


if __name__=='__main__':
    unittest.main()
