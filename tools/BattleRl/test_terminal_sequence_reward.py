import json
from pathlib import Path
import unittest
import subprocess
import sys
import tempfile
from sequence_training_reward import TerminalSequenceReward

ROOT=Path(__file__).resolve().parents[2]


class SequenceRewardTests(unittest.TestCase):
    def test_terminal_checkpoint_cannot_silently_resume_existing_objective(self):
        with tempfile.TemporaryDirectory() as directory:
            result=subprocess.run([sys.executable,'-X','utf8',str(ROOT/'tools/BattleRl/train_recurrent_ppo.py'),
                '--dll',str(ROOT/'tmp/battle_rl_explicit_build_tasks/BattleRl.dll'),
                '--pool',str(ROOT/'AutomationOutputs/BattleRl/20261007_eight_task_certified_pool.json'),
                '--resume',str(ROOT/'AutomationOutputs/BattleRl/20261007_recurrent_reward_pair_terminal/checkpoint.npz'),
                '--out',str(Path(directory)/'unexpected'), '--updates','1','--episodes-per-update','1','--epochs','1'],
                cwd=ROOT,capture_output=True,text=True,encoding='utf-8',timeout=30)
            self.assertNotEqual(result.returncode,0)
            self.assertIn('explicit experimental migration required',result.stderr)
            self.assertFalse(list(Path(directory).glob('**/episode_*.json')))

    def test_actual_partial_positive_reward_becomes_failure_not_win_credit(self):
        trace=json.loads((ROOT/'AutomationOutputs/BattleRl/20261007_recurrent_seven_hero_continued/episode_68.json').read_text())['trace']
        self.assertGreater(sum(r['training_reward'] for r in trace),0)
        objective=TerminalSequenceReward()
        rewards=[objective.step(r['before'],r['after'],r['reward'],i==len(trace)-1) for i,r in enumerate(trace)]
        self.assertLess(sum(rewards),0)
        self.assertFalse(trace[-1]['after']['taskProgress']['sequenceCompleted'])

    def test_actual_two_group_win_paid_only_on_formal_sequence_finish(self):
        pool=json.loads((ROOT/'AutomationOutputs/BattleRl/20261007_policy_actual_gap_discovery/solvable_pool.json').read_text())
        trace=json.loads(Path(pool['tasks'][0]['evidence']['path']).read_text())['trace']
        objective=TerminalSequenceReward()
        positive=[]
        for i,row in enumerate(trace):
            reward=objective.step(row['before'],row['after'],row['reward'],i==len(trace)-1)
            if reward>0:
                positive.append(i)
        self.assertEqual(positive,[len(trace)-1])
        self.assertTrue(trace[-1]['after']['taskProgress']['sequenceCompleted'])


if __name__=='__main__':
    unittest.main()
