"""Verify learned kind probabilities, legal tuple decoding and PPO derivatives."""
import json
from pathlib import Path
import numpy as np
from train_ppo import Policy, gradient_check

def main():
    net=Policy(4,5);net.enable_kind_factor([0,0,0,1,2])
    x=np.ones((1,4));mask=np.array([[True,True,True,True,False]])
    net.p['bp'][0]=100
    _,p,_=net.forward(x,mask)
    assert np.allclose([p[0,:3].sum(),p[0,3]], [.5,.5])
    assert p[0,4]==0 and net.greedy(p[0]) in (0,3)
    # A more probable kind can have a smaller individual tuple probability.
    net.p['bp'][:]=0;net.p['wp'][:]=0;net.p['bk'][0]=np.log(1.5)
    _,p,_=net.forward(x,mask)
    assert np.allclose([p[0,:3].sum(),p[0,3]],[.6,.4])
    assert int(p[0].argmax())==3 and net.greedy(p[0])==0
    target=Path('AutomationOutputs/BattleRl/kind_factor_verification.npz')
    target.parent.mkdir(parents=True,exist_ok=True);np.savez(target,**net.p)
    loaded=Policy(4,5);loaded.p=dict(np.load(target))
    _,q,_=loaded.forward(x,mask)
    assert np.array_equal(p,q) and loaded.greedy(q[0])==net.greedy(p[0])
    metadata=net.p['action_kind_groups'].copy();old_kind=net.p['bk'].copy()
    _,g,_=net.loss_grad(x,mask,np.array([0]),np.log(p[:,0]),np.ones(1),np.zeros(1))
    net.update(g)
    assert not np.array_equal(net.p['bk'],old_kind)
    assert np.array_equal(net.p['action_kind_groups'],metadata)
    flat=Policy(4,5);_,p,_=flat.forward(x,mask)
    assert flat.greedy(p[0])==int(p[0].argmax())
    result={'status':'passed','gradient_errors':{'flat':gradient_check(),'balanced':gradient_check(balanced=True),'hierarchical':gradient_check(hierarchical=True)},'checks':['large_tuple_logit_does_not_override_kind_mass','unavailable_kind_zero','kind_then_tuple_decoding','save_reload_identical','kind_head_updates_metadata_unchanged','legacy_decoder_unchanged'],'scope':'policy implementation only; no claim of better game performance'}
    Path('AutomationOutputs/BattleRl/kind_factor_verification.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps(result))

if __name__=='__main__':main()
