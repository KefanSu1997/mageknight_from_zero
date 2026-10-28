"""Controlled full-policy wiring gate, not an encoder or learning acceptance."""
import io,json,tempfile,zipfile
from pathlib import Path
import gymnasium as gym
import numpy as np
import torch
from sb3_contrib.common.recurrent.type_aliases import RNNStates
from baseline_a_scenario_policy import BaselineAScenarioPolicy
from baseline_a_scenario_actions import prepare_support
from hierarchical_option_policy import ChoiceTree
from solo_public_schema import PublicSchema
from solo_text_assets import SoloTextAssets


def run(root):
    torch.set_num_threads(2);torch.manual_seed(7112026)
    records=json.loads(Path('H:/MageKnight_RL_Runs/autodl_20261013/solo_conquest_A_enemyshuffle_v6_100_20261022/rollout_0001/episode_000000/native.json').read_text())['native']['records'][:3]
    schema=PublicSchema('H:/MageKnight_RL_Runs/20261021_solo_conquest_prepare/public_dto_schema.json')
    assets=SoloTextAssets('H:/MageKnight_RL_Runs/20261021_solo_conquest_prepare/solo_text_assets_v3','.')
    supports=[prepare_support(json.loads(r['ObservationBefore']),r['CommandsJson'],schema,assets) for r in records]
    vectors=np.load('H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets/entity_vectors.npy')
    vectors=np.concatenate((vectors,np.zeros((1,384),np.float32)))
    shapes={'state':(6471,),'entity_ids':(8,),'entity_roles':(8,),
            'entity_attributes':(8,54),'entity_present':(8,),'context_id':(1,)}
    space=gym.spaces.Dict({k:gym.spaces.Box(-np.inf,np.inf,s,np.float32) for k,s in shapes.items()})
    policy=BaselineAScenarioPolicy(space,gym.spaces.Discrete(1000000),lambda _:1e-4,entity_vectors=vectors)
    with zipfile.ZipFile(root/'reference_checkpoint.zip') as z:
        saved=torch.load(io.BytesIO(z.read('policy.pth')),weights_only=True,map_location='cpu')
    policy.load_reference(saved);policy.set_training_mode(False)
    class ControlledRuntime:
        def __init__(self,path):self.path=path;self.trees=[ChoiceTree(s['operations']) for s in supports]
        def support(self,c):return supports[max(c-1,0)]
        def tree(self,c):return self.trees[c-1],{}
        def vectors(self,requests):
            # Deterministic artificial marker vectors isolate recurrent/head wiring.
            return [np.random.default_rng(c*10000+n).normal(0,.1,(len(self.trees[c-1].nodes[n]['children'])+1,384)).astype(np.float32) for c,n in requests]
    obs={k:torch.zeros((3,)+s) for k,s in shapes.items()}
    obs['context_id'][:,0]=torch.arange(1,4)
    obs['entity_present'][:,0]=1
    obs['state'][:,:6314]=torch.tensor(np.asarray(json.loads((root/'native_observation_solo_gate.json.features.json').read_text(encoding='utf-8-sig')))[:3],dtype=torch.float32)
    zero=lambda:(torch.zeros(1,3,256),torch.zeros(1,3,256))
    states=RNNStates(zero(),zero());starts=torch.ones(3)
    with tempfile.TemporaryDirectory() as tmp:
        policy.bind_runtime(ControlledRuntime(tmp))
        actions,value,logp,_=policy(obs,states,starts,deterministic=True)
        value2,logp2,_=policy.evaluate_actions(obs,actions,states,starts)
        error=float((logp-logp2).abs().max().detach());assert error<1e-5
        torch.testing.assert_close(value,value2)
        (-logp2.mean()+value2.square().mean()).backward()
        grads={name:sum(float(p.grad.abs().sum()) for p in module.parameters() if p.grad is not None)
               for name,module in [('actor',policy.lstm_actor),('critic',policy.lstm_critic),('options',policy.head.option_out)]}
        assert all(v>0 for v in grads.values())
        assert all(p.grad is None or torch.isfinite(p.grad).all() for p in policy.parameters())
        path=Path(tmp)/'policy.pt';policy.save(path)
        restored=BaselineAScenarioPolicy.load(path,device='cpu');restored.set_training_mode(False)
        try:restored(obs,states,starts)
        except ValueError:pass
        else:raise AssertionError('Unbound runtime accepted')
        restored.bind_runtime(ControlledRuntime(tmp))
        a,v,l,_=restored(obs,states,starts,deterministic=True)
        torch.testing.assert_close(a,actions,rtol=0,atol=0)
        torch.testing.assert_close(v,value,rtol=0,atol=0)
        torch.testing.assert_close(l,logp,rtol=0,atol=0)
    return dict(passed=True,rows=3,forward_replay_logp_max_abs=error,gradient_l1=grads,
                reload_exact=True,optimizer_steps=0,
                scope='Controlled marker vectors and initial observations; actual supports. Not joint text encoder, full-game rollout, CUDA or PPO acceptance.')

if __name__=='__main__':
    root=Path('H:/MageKnight_RL_Runs/20261022_A_baseline_spec');result=run(root)
    (root/'scenario_policy_wiring_gate.json').write_text(json.dumps(result,indent=2))
    print(json.dumps(result))
