import unittest
import torch
from solo_group_prior import count_neutral_logits

class CountNeutralTests(unittest.TestCase):
    def test_duplicate_parameters_do_not_change_family_mass(self):
        commands=[{'EndTurn':{'DiscardHandSlots':[i]}} for i in range(31)]+[{'Spatial':{'Kind':0}}]
        logits=torch.zeros(32,dtype=torch.float64,requires_grad=True)
        probs=count_neutral_logits(logits,commands).softmax(0)
        self.assertAlmostEqual(float(probs[:31].sum()),.5)
        self.assertTrue(bool((probs>0).all()))
        (-probs[-1].log()).backward()
        self.assertTrue(bool(torch.isfinite(logits.grad).all()))
        self.assertTrue(bool((logits.grad!=0).all()))

    def test_equivalent_hierarchical_distribution_and_gradient(self):
        commands=[{'EndTurn':{}},{'Spatial':{'Kind':0}},{'EndTurn':{}},{'Spatial':{'Kind':1}}]
        x=torch.tensor([.4,-.7,1.3,.2],dtype=torch.float64,requires_grad=True)
        flat=count_neutral_logits(x,commands).log_softmax(0)
        groups=[[0,2],[1],[3]]
        scores=torch.stack([torch.logsumexp(x[g],0)-torch.log(x.new_tensor(len(g))) for g in groups])
        group_logp=scores.log_softmax(0)
        nested=torch.stack([group_logp[j]+x[g].log_softmax(0)[g.index(i)]
                            for i in range(4) for j,g in enumerate(groups) if i in g])
        torch.testing.assert_close(flat,nested)
        torch.testing.assert_close(torch.autograd.grad(flat[2],x,retain_graph=True)[0],
                                   torch.autograd.grad(nested[2],x)[0])

    def test_permutation_and_singleton(self):
        commands=[{'EndTurn':{}},{'Spatial':{'Kind':0}},{'EndTurn':{}}]
        x=torch.tensor([.1,.3,-.5]);order=[2,0,1]
        torch.testing.assert_close(count_neutral_logits(x[order],[commands[i] for i in order]),
                                   count_neutral_logits(x,commands)[order])
        self.assertEqual(float(count_neutral_logits(x[:1],commands[:1]).softmax(0)[0]),1.)

if __name__=='__main__':unittest.main()
