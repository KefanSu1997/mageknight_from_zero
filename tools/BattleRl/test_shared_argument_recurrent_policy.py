import tempfile
from pathlib import Path
import unittest
import numpy as np
from entity_recurrent_policy import EntityRecurrentPolicy
from shared_argument_recurrent_policy import SharedArgumentEntityPolicy
from recurrent_checkpoint import Adam, save_checkpoint, load_checkpoint


class PublicNumericFixture:
    width = 2
    def pack(self, x):
        return x.reshape(-1, 2, 2), np.ones((len(x), 2), dtype=bool), [None] * len(x)
    def scatter_numeric_gradient(self, gradient, identities):
        return gradient.reshape(-1, 4)


class SharedArgumentRecurrentTests(unittest.TestCase):
    actions = [dict(Kind=kind, Card=card, Choice='base', Targets=0, Parts=slot, Option=0, Input='')
               for kind, card, slot in [('play','basic_card_000',0),('play','basic_card_007',1),
                                        ('advance','',0),('fight','',0)]]
    def model(self, shared=True):
        cls = SharedArgumentEntityPolicy if shared else EntityRecurrentPolicy
        kwargs = dict(encoder_sizes=(6,5,4), hidden_size=4, embedding_size=2,
                      entity_width=4, entity_heads=2)
        if shared:kwargs['argument_rank']=4
        return cls(4, self.actions, PublicNumericFixture(), **kwargs)

    def arguments(self, model, rng):
        x=rng.normal(size=(4,1,4));previous=np.array([[-1],[0],[2],[1]])
        rewards=np.array([[0.],[-.01],[1.],[0.]])
        legal=[[0,1,2,3]]*4;selected=np.array([[0],[2],[1],[3]])
        old=model.sequence_log_probabilities(x,previous,rewards,legal,selected)+.02
        return x,previous,rewards,legal,selected,old,rng.normal(size=(4,1)),rng.normal(size=(4,1))

    def test_zero_head_retains_entity_policy_and_full_sequence_gradient(self):
        old,new=self.model(False),self.model()
        args=self.arguments(old,np.random.default_rng(71))
        np.testing.assert_array_equal(old.sequence_log_probabilities(*args[:5]),new.sequence_log_probabilities(*args[:5]))
        new.head.p['argument_project'][:]=np.random.default_rng(4).normal(0,.03,new.head.p['argument_project'].shape)
        _,grad,_=new.loss_grad(*args)
        for key in ['head.argument_query','head.argument_project','core.uz','core.entity.project','previous_action_embedding']:
            value=new.parameters()[key];index=np.unravel_index(abs(grad[key]).argmax(),value.shape);original=value[index]
            value[index]=original+1e-5;plus=new.loss_grad(*args)[0]
            value[index]=original-1e-5;minus=new.loss_grad(*args)[0]
            value[index]=original
            self.assertAlmostEqual((plus-minus)/2e-5,grad[key][index],places=6,msg=key)
            self.assertGreater(np.linalg.norm(grad[key]),1e-9)

    def test_complete_adam_rng_resume_after_sequence_updates(self):
        model=self.model();opt=Adam(model.parameters(),learning_rate=.0001);rng=np.random.default_rng(81)
        for _ in range(2):opt.update(model.loss_grad(*self.arguments(model,rng))[1])
        metadata=dict(rules_sha256='fixture',catalog_sha256='fixture',feature_schema='fixture',architecture='shared_args_entity_gru_fixture',task_goal='gradient_restore_only')
        with tempfile.TemporaryDirectory() as folder:
            path=Path(folder)/'checkpoint.npz';save_checkpoint(path,opt,rng,metadata,{'episode_boundary':True})
            arrays,other_opt,other_rng,history=load_checkpoint(path,metadata);other=self.model();other.bind_parameters(arrays)
            for _ in range(2):
                opt.update(model.loss_grad(*self.arguments(model,rng))[1]);other_opt.update(other.loss_grad(*self.arguments(other,other_rng))[1])
            for key in model.parameters():
                for a,b in ((model.parameters()[key],other.parameters()[key]),(opt.first[key],other_opt.first[key]),(opt.second[key],other_opt.second[key])):np.testing.assert_array_equal(a,b)
            self.assertEqual(rng.bit_generator.state,other_rng.bit_generator.state)
            self.assertEqual(opt.steps,other_opt.steps)
            self.assertTrue(history['episode_boundary'])


if __name__ == '__main__':unittest.main()
