"""Resource/units projection audit over retained full episodes, no optimization."""
import copy,collections,hashlib,json
from pathlib import Path
import numpy as np
from baseline_a_unit_resources import UnitResourceCodec,RESOURCE_ROLES
from baseline_a_visible_inventory import VISIBLE_ROLES
from baseline_a_combat_entities import COMBAT_ROLES
from baseline_a_world_entities import WORLD_ROLES
from public_entity_tokens import KINDS


def run():
    ids=json.loads(Path('H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets/manifest.json').read_text())['entity_ids']
    offset=sum(map(len,(KINDS,WORLD_ROLES,VISIBLE_ROLES,COMBAT_ROLES)));codec=UnitResourceCodec('.',ids,offset)
    folder=Path('H:/MageKnight_RL_Runs/autodl_20261013/solo_conquest_A_enemyshuffle_v6_100_20261022/rollout_0001')
    files=[];states=0;maximum=0;roles=collections.Counter();example=None
    for path in sorted(folder.glob('episode_*/native.json')):
        data=path.read_bytes();files.append(dict(path=str(path),sha256=hashlib.sha256(data).hexdigest()))
        for row in json.loads(data)['native']['records']:
            inventory=json.loads(row['ObservationBefore'])['Inventory'];items=codec.encode(inventory)
            states+=1;maximum=max(maximum,len(items));example=example or inventory
            roles.update(RESOURCE_ROLES[r-offset] for _,r,_ in items)
    assert states
    baseline=codec.encode(example);changed=copy.deepcopy(example);changed['Fame']+=20
    assert not np.array_equal(baseline[0][2],codec.encode(changed)[0][2])
    changed=copy.deepcopy(example);changed['Mana'][0]['Color']='unknown'
    try:codec.encode(changed)
    except ValueError:pass
    else:raise AssertionError('Unknown color accepted')
    definitions_path=Path('H:/MageKnight_RL_Runs/20261022_A_baseline_spec/native_unit_definitions.json')
    definitions=json.loads(definitions_path.read_text(encoding='utf-8-sig'));abilities=0
    # Typed fixture uses actual native-loaded definitions. It does not claim
    # the current policy recruited or activated these units in real episodes.
    unit_fixture=copy.deepcopy(example);unit_fixture['Units']=[]
    for i,d in enumerate(definitions):
        actions=[dict(Index=j,Kind=codec.kinds[a['Kind']],Definition=a) for j,a in enumerate(d['Actions'])]
        abilities+=len(actions)
        unit_fixture['Units'].append(dict(Slot=i,Id=d['Id'],Ready=True,CanActivate=True,Wounds=0,Destroyed=False,
            Banner=None,BannerEffect=None,Attunement=None,NeedsUpkeep=False,CourageSpent=False,FortitudeSpent=False,
            ReturningBanner=False,Actions=actions))
    entries=codec.encode(unit_fixture)
    assert sum(r==offset+RESOURCE_ROLES.index('unit') for _,r,_ in entries)==len(definitions)
    damaged=copy.deepcopy(unit_fixture);damaged['Units'][0]['Wounds']=2
    assert any(not np.array_equal(a[2],b[2]) for a,b in zip(entries,codec.encode(damaged)))
    return dict(passed=True,files=files,public_states=states,maximum_entities=maximum,actual_roles=dict(roles),
                native_definition_fixture_units=len(definitions),native_definition_fixture_abilities=abilities,
                native_definitions_sha256=hashlib.sha256(definitions_path.read_bytes()).hexdigest(),
                optimizer_steps=0,scope='Observed resources plus typed actual-unit-definition fixture; not actual unit recruitment/activation coverage')

if __name__=='__main__':
    result=run();Path('H:/MageKnight_RL_Runs/20261022_A_baseline_spec/unit_resource_gate.json').write_text(json.dumps(result,indent=2))
    print(json.dumps({k:v for k,v in result.items() if k!='files'}))
