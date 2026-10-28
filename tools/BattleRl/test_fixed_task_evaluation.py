import copy
import json
import tempfile
import unittest
from pathlib import Path
import numpy as np
from fixed_task_evaluation import evaluate


class FrozenEvaluationTests(unittest.TestCase):
    def test_cache_reuses_only_bound_completed_tasks(self):
        from task_feasibility import file_hash
        class Policy:
            def parameters(self):return {'w':np.array([1.])}
        class Parallel:
            def __init__(self):self.random=[np.random.default_rng(9)];self.visited=[]
            def state(self):return [copy.deepcopy(r.bit_generator.state) for r in self.random]
            def collect(self,policy,jobs,*args,**kwargs):
                self.visited.extend(i for i,_ in jobs)
                return [dict(trace=[dict(after={'taskProgress':{'sequenceCompleted':False}},training_reward=-10,reward=0)],rollout_values=np.array([0.]),returns=np.array([-10.])) for _ in jobs]
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);source=root/'old'/'eval';source.mkdir(parents=True)
            new=root/'new';new.mkdir();checkpoint=root/'checkpoint';checkpoint.write_bytes(b'fixture')
            for parent in (source.parent,new):(parent/'manifest.json').write_text(json.dumps({'metadata':{'binding':'same'}}))
            (source/'manifest.json').write_text(json.dumps(dict(update=0,checkpoint_sha256=file_hash(checkpoint),task_count=2,actor_seed=9112026,per_task_rng=True,optimizer_updates=0)))
            rows=[{'task_id':'a'},{'task_id':'b'}];archive=source/'rollouts';archive.mkdir()
            (archive/'episode_0.json').write_text(json.dumps(dict(episode=0,certificate=rows[0],report={'errors':[]},trace=[dict(after={'taskProgress':{'sequenceCompleted':True}},training_reward=20,reward=0)])))
            np.savez(archive/'episode_0.npz',rollout_values=np.array([0.]),returns=np.array([20.]))
            parallel=Parallel();result=evaluate(Policy(),parallel,rows,new/'eval',None,(),0,checkpoint,source)
            self.assertEqual(parallel.visited,[1]);self.assertEqual(result['wins'],1);self.assertEqual(result['episodes'],2)
            checkpoint.write_bytes(b'changed')
            with self.assertRaisesRegex(ValueError,'binding changed'):
                evaluate(Policy(),parallel,rows,new/'bad',None,(),0,checkpoint,source)

    def test_evaluation_restores_training_rng_and_counts_all_tasks(self):
        class Policy:
            def parameters(self):return {'w':np.array([1.,2.])}
        class Parallel:
            def __init__(self):self.random=[np.random.default_rng(42)]
            def state(self):return [copy.deepcopy(r.bit_generator.state) for r in self.random]
            def collect(self, policy, jobs, collect, arguments, output, task_seed=None):
                self.random[0].random(4)
                return [dict(trace=[dict(after={'taskProgress':{'sequenceCompleted':i==0}},training_reward=20 if i==0 else -10,reward=0)],
                    rollout_values=np.array([0.]),returns=np.array([20. if i==0 else -10.])) for i,_ in jobs]
        parallel=Parallel();before=parallel.state()
        with tempfile.TemporaryDirectory() as directory:
            parent=Path(directory);checkpoint=parent/'checkpoint';checkpoint.write_bytes(b'fixture')
            result=evaluate(Policy(),parallel,[{},{}],parent/'eval',None,(),0,checkpoint)
            self.assertEqual(result['wins'],1);self.assertEqual(result['episodes'],2)
            self.assertEqual(result['mean_training_reward'],5.)
            self.assertEqual(parallel.state(),before)
            self.assertTrue((parent/'train_eval.html').exists())

    def test_failure_also_restores_training_rng(self):
        class Policy:
            def parameters(self):return {'w':np.array([1.])}
        class Parallel:
            def __init__(self):self.random=[np.random.default_rng(9)]
            def state(self):return [copy.deepcopy(r.bit_generator.state) for r in self.random]
            def collect(self,*args,**kwargs):
                self.random[0].random(9)
                raise RuntimeError('preserved failure')
        parallel=Parallel();before=parallel.state()
        with tempfile.TemporaryDirectory() as directory:
            parent=Path(directory);checkpoint=parent/'checkpoint';checkpoint.write_bytes(b'fixture')
            with self.assertRaisesRegex(RuntimeError,'preserved failure'):
                evaluate(Policy(),parallel,[{}],parent/'eval',None,(),0,checkpoint)
            self.assertEqual(parallel.state(),before)


if __name__=='__main__':unittest.main()
