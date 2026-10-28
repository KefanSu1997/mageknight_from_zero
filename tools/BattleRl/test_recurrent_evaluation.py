import copy
import json
from pathlib import Path
import unittest
from evaluate_recurrent_ppo import partition_gate, parameter_hash, select_action
import numpy as np
from recurrent_policy import RecurrentPolicy

ROOT=Path(__file__).resolve().parents[2]


class RecurrentEvaluationTests(unittest.TestCase):
    def test_related_random_stream_variant_cannot_be_held_out(self):
        training=json.loads((ROOT/'AutomationOutputs/BattleRl/20261007_discovery_cached_candidates/solvable_pool.json').read_text())['tasks']
        variant=copy.deepcopy(training[0])
        variant['task']['definition']['ShuffleSeed']+=1
        partition_gate(training,[variant],'calibration')
        for partition in ('validation','test'):
            with self.assertRaises(ValueError):
                partition_gate(training,[variant],partition)

    def test_declared_validation_and_test_partitions_remain_separate(self):
        rows=json.loads((ROOT/'AutomationOutputs/BattleRl/20261007_coverage_task_drafts/tasks.json').read_text())['candidates']
        training=[dict(task=dict(definition=next(r for r in rows if r['split']=='train')['task_definition']))]
        validation=[dict(task=dict(definition=next(r for r in rows if r['split']=='validation')['task_definition']))]
        partition_gate(training,validation,'validation')
        with self.assertRaises(ValueError):
            partition_gate(training,validation,'test')
        with self.assertRaises(ValueError):
            partition_gate(training,validation,'calibration')

    def test_inference_changes_memory_without_mutating_weights(self):
        actions=[dict(Kind='advance'),dict(Kind='play')]
        policy=RecurrentPolicy(4,actions,encoder_sizes=(5,5,3),hidden_size=4,embedding_size=2)
        before=parameter_hash(policy)
        _,_,memory=policy.step([1,0,0,1],[0,1])
        policy.step([0,1,0,1],[0,1],0,.5,memory)
        self.assertEqual(before,parameter_hash(policy))

    def test_joint_map_and_kind_first_greedy_are_explicitly_different(self):
        actions=[dict(Kind='advance'),dict(Kind='play'),dict(Kind='play'),dict(Kind='source')]
        policy=RecurrentPolicy(4,actions,encoder_sizes=(5,5,3),hidden_size=4,embedding_size=2)
        # Joint MAP can also differ when the largest kind has many parameters.
        probability=np.array([.4,.3,.3,0.])
        random=np.random.default_rng(1)
        self.assertEqual(select_action(policy,probability,random,'joint_map'),0)
        self.assertEqual(select_action(policy,probability,random,'hierarchical_greedy'),1)
        with self.assertRaises(ValueError):
            select_action(policy,probability,random,'unrecognized')


if __name__=='__main__':
    unittest.main()
