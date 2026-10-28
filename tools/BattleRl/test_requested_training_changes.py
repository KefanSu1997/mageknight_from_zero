"""Meaningful input-gradient and isolated parallel RNG restoration contracts."""
import copy
import tempfile
from pathlib import Path
from unittest.mock import patch
import unittest
import numpy as np
import test_shared_argument_recurrent_policy as shared_fixture
from printed_argument_features import extend_arguments
from parallel_rollouts import ParallelRollouts


class RequestedTrainingChanges(unittest.TestCase):
    def test_printed_input_warm_equivalence_and_sequence_gradient(self):
        fixture=shared_fixture.SharedArgumentRecurrentTests();old=fixture.model();new=copy.deepcopy(old)
        features,_=extend_arguments(new.actions,new.head.argument_features)
        width=new.head.argument_features.shape[1]
        new.head.argument_features=features
        new.head.p['argument_project']=np.pad(new.head.p['argument_project'],((0,features.shape[1]-width),(0,0)))
        args=fixture.arguments(old,np.random.default_rng(43))
        np.testing.assert_array_equal(old.sequence_log_probabilities(*args[:5]),new.sequence_log_probabilities(*args[:5]))
        new.head.p['argument_project'][width:]=np.random.default_rng(13).normal(0,.03,new.head.p['argument_project'][width:].shape)
        loss,grad,_=new.loss_grad(*args)
        for key in ('head.argument_project','core.uz','core.entity.project','previous_action_embedding'):
            value=new.parameters()[key];index=np.unravel_index(np.abs(grad[key]).argmax(),value.shape);original=value[index]
            if key=='head.argument_project':
                self.assertGreater(float(np.abs(grad[key][width:]).max()),0.)
                tail=np.unravel_index(np.abs(grad[key][width:]).argmax(),grad[key][width:].shape)
                index=(tail[0]+width,tail[1]);original=value[index]
            value[index]=original+1e-5;plus=new.loss_grad(*args)[0]
            value[index]=original-1e-5;minus=new.loss_grad(*args)[0];value[index]=original
            self.assertAlmostEqual((plus-minus)/2e-5,grad[key][index],delta=2e-6)
        self.assertTrue(np.isfinite(loss))

    def test_parallel_worker_rng_restore_and_mutable_array_isolation(self):
        fixture=shared_fixture.SharedArgumentRecurrentTests();policy=fixture.model()
        class FakeRules:
            info={'featureSchema':'math_fixture'};actions=fixture.actions
            def close(self):pass
        parent=FakeRules();seed=np.random.default_rng(17).bit_generator.state
        def collect(env,local,rng,task):
            local.parameters()['head.bp'][0]+=1
            return {'draw':float(rng.random()),'value':float(local.parameters()['head.bp'][0])}
        jobs=[(i,{'task_id':str(i),'task':{'seed':i}}) for i in range(4)]
        with tempfile.TemporaryDirectory() as tmp, patch('train_ppo.Rules',FakeRules):
            first=ParallelRollouts(parent,2,seed)
            first.collect(policy,jobs,collect,lambda _:(),tmp)
            states=first.state();expected=first.collect(policy,jobs,collect,lambda _:(),tmp);first.close()
            second=ParallelRollouts(parent,2,seed,states)
            actual=second.collect(policy,jobs,collect,lambda _:(),tmp);second.close()
            self.assertEqual(expected,actual)
            self.assertEqual(policy.parameters()['head.bp'][0],0.)
            self.assertNotEqual(states[0],states[1])

    def test_step_cost_reference_rank_keeps_completion_before_early_failure(self):
        gamma=.995;cost=.03  # Existing .01 plus new training-only .02.
        value=lambda steps,terminal:sum(-cost*gamma**i for i in range(steps))+terminal*gamma**(steps-1)
        self.assertGreater(value(10,20),value(256,20))
        self.assertGreater(value(256,20),value(1,-10))


if __name__=='__main__':unittest.main()
