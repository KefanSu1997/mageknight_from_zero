"""CPU reload of an actual remote CUDA checkpoint, with zero optimizer updates."""
import copy
import json
from pathlib import Path
import tempfile
from types import SimpleNamespace
import numpy as np
import torch
from solo_public_schema import PublicSchema
from solo_structured_policy import SoloStructuredPolicy
from solo_episode_data import state_signature
from train_solo_conquest import checkpoint,restore_checkpoint


def check():
    evidence=Path('H:/MageKnight_RL_Runs/20261021_solo_conquest_prepare')
    gate=evidence/'cuda_solo_ppo_v1'
    saved=torch.load(gate/'checkpoint.pt',map_location='cpu',weights_only=False)
    schema=PublicSchema(gate/'public_dto_schema.json')
    catalog=json.loads((gate/'text_assets/manifest.json').read_text())['catalog_signature']
    def make():
        network=SoloStructuredPolicy(schema,catalog)
        updater=SimpleNamespace(policy=SimpleNamespace(optimizer=torch.optim.Adam(network.parameters(),lr=1e-4,eps=1e-5),
                                                      accepted_steps=saved['accepted_steps']),_n_updates=4)
        return network,updater
    source,updater=make();source.load_state_dict(saved['network'])
    updater.policy.optimizer.load_state_dict(saved['optimizer'])
    assert len(updater.policy.optimizer.state)>0,'Require actual populated Adam state'
    rng=np.random.default_rng(7112026);rng.integers(0,100,size=9)
    out=Path(tempfile.mkdtemp(prefix='training_checkpoint_gate_',dir=evidence))
    contract=dict(scope='actual_remote_weights_reload_only_not_formal_contract')
    receipt=checkpoint(out,1,source,updater,rng,contract)
    expected_tasks=rng.integers(0,7000,size=128);expected_torch=torch.rand(12)
    restored,reader=make();other=np.random.default_rng(0)
    update,_=restore_checkpoint(receipt['path'],restored,reader,other,contract)
    assert update==1 and np.array_equal(other.integers(0,7000,size=128),expected_tasks)
    assert torch.equal(torch.rand(12),expected_torch)
    assert state_signature(source.state_dict())==state_signature(restored.state_dict())
    a=updater.policy.optimizer.state_dict();b=reader.policy.optimizer.state_dict()
    assert a['param_groups']==b['param_groups'] and a['state'].keys()==b['state'].keys()
    for key,values in a['state'].items():
        for name,value in values.items():
            other_value=b['state'][key][name]
            assert torch.equal(value,other_value) if torch.is_tensor(value) else value==other_value
    rejected=False
    try:restore_checkpoint(receipt['path'],restored,reader,other,dict(scope='changed'))
    except ValueError:rejected=True
    assert rejected
    report=dict(passed=True,scope='actual_remote_adam_and_weights_cpu_reload_no_training',
                adam_states=len(a['state']),parameters_exact=True,adam_exact=True,task_rng_exact=True,
                torch_rng_exact=True,contract_mismatch_rejected=True,local_optimizer_steps=0)
    (out/'gate.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print(json.dumps(dict(evidence=str(out),**report)))


if __name__=='__main__':check()
