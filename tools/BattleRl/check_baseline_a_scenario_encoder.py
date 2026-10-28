"""Actual A300 encoder equivalence and new world branch gradient gate; no training."""
import io
import json
import zipfile
from pathlib import Path
import gymnasium as gym
import numpy as np
import torch
from atomic_entity_text import EntityTextEncoder
from public_entity_tokens import PublicEntityTokens, original_vocabulary, KINDS
from baseline_a_world_inputs import encode_world
from baseline_a_world_entities import WorldEntityCodec, ScenarioEntityTextEncoder


def run(root):
    torch.set_num_threads(2);torch.manual_seed(7112026)
    assets=Path('H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets')
    meta=json.loads((assets/'manifest.json').read_text())
    vectors=np.load(assets/'entity_vectors.npy',allow_pickle=False)
    import hashlib
    assert hashlib.sha256((assets/'entity_vectors.npy').read_bytes()).hexdigest()==meta['assets_sha256']['entity_vectors.npy']
    layout=PublicEntityTokens(original_vocabulary());assert layout.ids==meta['entity_ids']
    codec=WorldEntityCodec('.',layout.ids)
    worlds=json.loads((root/'native_observation_solo_gate.json.worlds.json').read_text(encoding='utf-8-sig'))[::4]
    features=np.array(json.loads((root/'native_observation_solo_gate.json.features.json').read_text(encoding='utf-8-sig')))[::4]
    extensions=[encode_world(w) for w in worlds]
    n=len(worlds);capacity=512
    def space(width):
        return gym.spaces.Dict({'state':gym.spaces.Box(-np.inf,np.inf,(width,),np.float32)})
    old=EntityTextEncoder(space(6411),vectors).eval()
    checkpoint=root/'reference_checkpoint.zip'
    assert hashlib.sha256(checkpoint.read_bytes()).hexdigest()=='21744726500984e8d4f422f39e1d5fedd883a1968173da61f74b9596d5945e4f'
    with zipfile.ZipFile(checkpoint) as z:
        saved=torch.load(io.BytesIO(z.read('policy.pth')),map_location='cpu',weights_only=True)
    state={k.removeprefix('features_extractor.'):v for k,v in saved.items() if k.startswith('features_extractor.')}
    old.load_state_dict(state,strict=True)
    expanded_vectors=np.concatenate((vectors,np.zeros((1,384),np.float32)))
    new=ScenarioEntityTextEncoder(space(6471),expanded_vectors).eval();new.load_reference(state)
    obs=dict(state=np.concatenate((features,np.zeros((n,97))),axis=1).astype(np.float32),
             entity_ids=np.zeros((n,capacity),np.int64),entity_roles=np.zeros((n,capacity),np.int64),
             entity_attributes=np.zeros((n,capacity,54),np.float32),entity_present=np.zeros((n,capacity),np.uint8))
    counts=[]
    for row,f in enumerate(features):
        tokens,entries=layout._one(f);counts.append(len(entries))
        for i,(identity,kind,_) in enumerate(entries):
            obs['entity_ids'][row,i]=layout.id_index[identity];obs['entity_roles'][row,i]=KINDS.index(kind)
        obs['entity_attributes'][row,:len(entries)]=tokens[:,len(layout.ids)+len(KINDS):]
        obs['entity_present'][row,:len(entries)]=1
    tensor=lambda d:{k:torch.as_tensor(v) for k,v in d.items()}
    baseline=tensor(obs)
    migrated={k:v.clone() for k,v in baseline.items()}
    migrated['state']=torch.cat((baseline['state'],torch.as_tensor(np.stack([e['numeric'] for e in extensions]))),1)
    with torch.no_grad():
        expected=old(baseline);equivalent=new(migrated)
    difference=float((expected-equivalent).abs().max());assert difference<1e-5
    augmented={k:v.clone() for k,v in migrated.items()}
    for row,e in enumerate(extensions):
        entities=codec.encode(e['records'])
        if counts[row]+len(entities)>capacity:raise ValueError('Entity capacity overflow; no truncation')
        for i,(identity,role,attributes) in enumerate(entities,counts[row]):
            augmented['entity_ids'][row,i]=identity;augmented['entity_roles'][row,i]=role
            augmented['entity_attributes'][row,i]=torch.as_tensor(attributes);augmented['entity_present'][row,i]=1
    output=new(augmented);assert torch.isfinite(output).all()
    output.square().mean().backward()
    role_gradient=float(new.role_embedding.weight.grad[len(KINDS):].abs().sum())
    world_numeric_gradient=float(new.numeric[0].weight.grad[:,6411:].abs().sum())
    assert role_gradient>0 and world_numeric_gradient>0
    assert all(p.grad is None or torch.isfinite(p.grad).all() for p in new.parameters())
    return dict(passed=True,heroes=n,reference_checkpoint_sha256=hashlib.sha256(checkpoint.read_bytes()).hexdigest(),
        reference_encoder_max_abs=difference,world_role_gradient_l1=role_gradient,
        world_numeric_gradient_l1=world_numeric_gradient,
        world_output_change_max_abs=float((output.detach()-equivalent).abs().max()),
        role_attribute_names=codec.attribute_names,source_sha256=codec.sources,
        optimizer_steps=0,scope='A300 feature encoder only; candidate policy, full world coverage and PPO gates remain pending')


if __name__=='__main__':
    root=Path('H:/MageKnight_RL_Runs/20261022_A_baseline_spec')
    result=run(root)
    (root/'scenario_encoder_gate.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in ('role_attribute_names','source_sha256')}))
