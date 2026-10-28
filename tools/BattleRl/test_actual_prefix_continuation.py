import copy
import json
import os
from pathlib import Path
import unittest
from continue_actual_prefix_search import verify_prefix
from train_ppo import Rules

ROOT=Path(__file__).resolve().parents[2]
DLL=ROOT/'tmp/battle_rl_explicit_build_tasks/BattleRl.dll'


class PrefixContinuationTests(unittest.TestCase):
    def test_actual_prefix_replay_and_corrupt_state_action_reward_rejected(self):
        pool=json.loads((ROOT/'AutomationOutputs/BattleRl/20261007_frozen_policy_validation_discovery/solvable_pool.json').read_text())
        definitions={r['draft_id']:r['task_definition'] for r in pool['quarantine']}
        chosen=None
        for attempt in pool['attempts']:
            trace=json.loads(Path(attempt['path']).read_text())['trace']
            cutoff=next((i+1 for i,row in enumerate(trace) if
                         row['after']['taskProgress']['encounterIndex']>row['before']['taskProgress']['encounterIndex']
                         and row['after']['phase']=='Outside'),None)
            if cutoff:
                chosen=(definitions[attempt['draft_id']],trace[:cutoff])
                break
        self.assertIsNotNone(chosen)
        task,trace=chosen
        os.environ.update(BATTLE_RL_DLL=str(DLL),BATTLE_RL_PROTOCOL='--serve-journey-artifacts',
                          BATTLE_RL_NATIVE_PAYMENTS='1',BATTLE_RL_NATIVE_CONVERSIONS='1',
                          BATTLE_RL_NATIVE_ENERGY_TARGETS='1',BATTLE_RL_ORIGINAL_OPENINGS='1')
        rules=Rules()
        try:
            prefix=verify_prefix(rules,task,trace)
            self.assertEqual(len(prefix),len(trace))
            self.assertEqual(trace[-1]['after']['taskProgress']['encounterIndex'],1)
            changed=copy.deepcopy(trace)
            changed[0]['before']['fame']+=1
            with self.assertRaises(ValueError):
                verify_prefix(rules,task,changed)
            changed=copy.deepcopy(trace)
            changed[0]['action']['Input']+=';fake=1'
            with self.assertRaises(ValueError):
                verify_prefix(rules,task,changed)
            changed=copy.deepcopy(trace)
            changed[0]['reward']+=1
            with self.assertRaises(ValueError):
                verify_prefix(rules,task,changed)
        finally:
            rules.close()


if __name__=='__main__':
    unittest.main()
