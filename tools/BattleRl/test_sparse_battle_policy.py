import unittest
from types import SimpleNamespace
import torch
from sparse_battle_policy import SparseBattleRecurrentPolicy


class SparseSupportTests(unittest.TestCase):
    def test_probabilities_entropy_global_ids_and_gradients(self):
        torch.set_num_threads(1)
        torch.manual_seed(42)
        n = 95653
        p = SimpleNamespace(descriptors=torch.randn(n, 12, dtype=torch.float64),
            argument_encoder=torch.nn.Linear(12, 32, bias=False).double(),
            action_net=torch.nn.Linear(128, 32).double(),
            _mask=torch.zeros(4, n, dtype=torch.bool))
        for row, count in enumerate((1, 3, 137, 503)):
            p._mask[row, torch.randperm(n)[:count]] = True
        x = torch.randn(4, 128, dtype=torch.float64, requires_grad=True)
        dense = torch.distributions.Categorical(logits=(p.action_net(x) @
            p.argument_encoder(p.descriptors).T).masked_fill(~p._mask, -torch.inf))
        sparse = SparseBattleRecurrentPolicy._get_action_dist_from_latent(p, x)
        actions = p._mask.long().argmax(1)
        torch.testing.assert_close(dense.log_prob(actions), sparse.log_prob(actions))
        torch.testing.assert_close(dense.entropy(), sparse.entropy())
        torch.testing.assert_close(dense.probs.gather(1, sparse.ids).masked_fill(~sparse.valid, 0),
                                   sparse.distribution.probs)
        self.assertTrue(torch.equal(dense.probs.argmax(1), sparse.mode()))
        params = [x, *p.action_net.parameters(), *p.argument_encoder.parameters()]
        gd = torch.autograd.grad((dense.log_prob(actions) + .01*dense.entropy()).sum(), params)
        gs = torch.autograd.grad((sparse.log_prob(actions) + .01*sparse.entropy()).sum(), params)
        for a,b in zip(gd,gs):torch.testing.assert_close(a,b,atol=1e-10,rtol=1e-10)
        for _ in range(50):self.assertTrue(p._mask.gather(1,sparse.sample()[:,None]).all())


if __name__ == '__main__':unittest.main()
