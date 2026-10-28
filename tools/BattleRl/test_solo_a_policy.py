"""CPU inference contracts only, no RL training or optimizer updates."""
import io
import unittest
import torch
from solo_a_policy import SoloAPolicy


class PolicyTests(unittest.TestCase):
    def setUp(self):
        torch.set_num_threads(2)
        torch.manual_seed(731)
        self.model=SoloAPolicy(17,9,11).eval()
        self.inputs=(torch.randn(17),torch.randn(4,384),torch.randn(4,9),
                     torch.randn(5,11),[torch.randn(2,384) for _ in range(5)])

    @torch.no_grad()
    def test_candidate_permutation_and_text_influence(self):
        logits,value,_=self.model.step(*self.inputs)
        order=torch.tensor([4,2,0,3,1])
        permuted=(*self.inputs[:3],self.inputs[3][order],[self.inputs[4][i] for i in order])
        other,other_value,_=self.model.step(*permuted)
        torch.testing.assert_close(other,logits[order])
        torch.testing.assert_close(other_value,value)
        texts=list(self.inputs[4]);texts[0]=torch.randn(2,384)
        altered,_,_=self.model.step(*self.inputs[:4],texts)
        self.assertGreater(abs((altered[0]-logits[0]).item()),1e-7)
        torch.testing.assert_close(altered[1:],logits[1:])

    @torch.no_grad()
    def test_reset_and_reload(self):
        logits,value,memory=self.model.step(*self.inputs)
        reset,reset_value,_=self.model.step(*self.inputs,memory=memory,episode_start=True)
        torch.testing.assert_close(reset,logits);torch.testing.assert_close(reset_value,value)
        continued,_,_=self.model.step(*self.inputs,memory=memory)
        self.assertFalse(torch.allclose(continued,logits,atol=1e-8,rtol=1e-8))
        stream=io.BytesIO();torch.save(self.model.state_dict(),stream);stream.seek(0)
        restored=SoloAPolicy(*self.model.dimensions).eval()
        restored.load_state_dict(torch.load(stream,weights_only=True))
        actual,prediction,_=restored.step(*self.inputs)
        torch.testing.assert_close(actual,logits,rtol=0,atol=0)
        torch.testing.assert_close(prediction,value,rtol=0,atol=0)

    @torch.no_grad()
    def test_dynamic_support_and_entity_permutation(self):
        logits,value,_=self.model.step(*self.inputs)
        order=torch.tensor([3,0,2,1])
        actual,prediction,_=self.model.step(self.inputs[0],self.inputs[1][order],self.inputs[2][order],*self.inputs[3:])
        torch.testing.assert_close(actual,logits,atol=1e-6,rtol=1e-5)
        torch.testing.assert_close(prediction,value,atol=1e-6,rtol=1e-5)
        many=torch.randn(300,11)
        scores,_,_=self.model.step(*self.inputs[:3],many,[torch.empty(0,384) for _ in range(300)])
        self.assertEqual(scores.shape,(300,))
        self.assertTrue((scores.softmax(0)>0).all())


if __name__=='__main__':unittest.main()
