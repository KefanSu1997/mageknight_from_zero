import unittest
import numpy as np
from value_diagnostics import measure
import test_shared_argument_recurrent_policy as fixture
from train_recurrent_ppo import updated_kl


class ValueDiagnosticsTests(unittest.TestCase):
    def test_reference_values_and_degenerate_targets(self):
        y=np.array([-1.,0.,1.])
        self.assertEqual(measure(y,y)['explained_variance'],1.)
        self.assertEqual(measure(np.zeros(3),y)['explained_variance'],0.)
        self.assertEqual(measure(-y,y)['explained_variance'],-3.)
        biased=measure(y+4,y)
        self.assertEqual(biased['explained_variance'],1.)
        self.assertEqual(biased['mse'],16.)
        self.assertIsNone(measure(y,np.ones(3))['explained_variance'])
        with self.assertRaises(ValueError):measure([np.nan], [1.])

    def test_shared_recurrent_values_reuse_kl_without_parameter_change(self):
        f=fixture.SharedArgumentRecurrentTests();p=f.model()
        a=f.arguments(p,np.random.default_rng(12))
        e=dict(zip(('observations','previous_actions','previous_rewards','legal','selected','old_log'),a[:6]))
        saved={k:v.copy() for k,v in p.parameters().items()}
        baseline=updated_kl(p,[e]);actual=updated_kl(p,[e],with_values=True)
        self.assertEqual(baseline,actual)
        memory=None;values=[]
        for t in range(len(a[0])):
            _,v,memory=p.step(a[0][t,0],a[3][t],a[1][t,0],a[2][t,0],memory)
            values.append(v)
        np.testing.assert_allclose(values,e['updated_values'],rtol=1e-12,atol=1e-12)
        for k,v in p.parameters().items():np.testing.assert_array_equal(v,saved[k])
