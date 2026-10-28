"""Actual combat projection coverage; no training and no claim of all rules."""
import copy,collections,hashlib,json
from pathlib import Path
import numpy as np
from baseline_a_combat_entities import CombatEntityCodec,COMBAT_ROLES
from baseline_a_visible_inventory import VISIBLE_ROLES
from baseline_a_world_entities import WORLD_ROLES
from public_entity_tokens import KINDS


def run():
    ids=json.loads(Path('H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets/manifest.json').read_text())['entity_ids']
    offset=len(KINDS)+len(WORLD_ROLES)+len(VISIBLE_ROLES);codec=CombatEntityCodec('.',ids,offset)
    folder=Path('H:/MageKnight_RL_Runs/autodl_20261013/solo_conquest_A_enemyshuffle_v6_100_20261022/rollout_0001')
    files=[];states=0;active=0;maximum=0;roles=collections.Counter();example=None
    for path in sorted(folder.glob('episode_*/native.json')):
        data=path.read_bytes();files.append(dict(path=str(path),sha256=hashlib.sha256(data).hexdigest()))
        for row in json.loads(data)['native']['records']:
            combat=json.loads(row['ObservationBefore'])['Combat'];items=codec.encode(combat)
            states+=1;active+=bool(combat['Active']);maximum=max(maximum,len(items))
            roles.update(COMBAT_ROLES[r-offset] for _,r,_ in items)
            if example is None and combat['Enemies']:example=combat
    assert states and example
    expanded=copy.deepcopy(example);expanded['Enemies']=[dict(copy.deepcopy(example['Enemies'][0]),Slot=i) for i in range(20)]
    entries=codec.encode(expanded);enemies=[x for x in entries if x[1]==offset+1]
    assert len(enemies)==20 and enemies[-1][2][0]==19/32
    for field,value in [('EffectiveArmor',77),('Defeated',not expanded['Enemies'][0]['Defeated'])]:
        changed=copy.deepcopy(expanded);changed['Enemies'][0][field]=value
        updated=codec.encode(changed)
        assert any(not np.array_equal(a[2],b[2]) for a,b in zip(entries,updated))
    for fault in ('identity','hidden','slot'):
        bad=copy.deepcopy(example)
        if fault=='identity':bad['Enemies'][0]['Id']='_not_known_'
        elif fault=='hidden':bad['FutureEnemies']=[]
        else:bad['Enemies'][0]['Slot']=500
        try:codec.encode(bad)
        except ValueError:pass
        else:raise AssertionError('Bad combat accepted')
    return dict(passed=True,files=files,public_states=states,active_combat_states=active,maximum_entities=maximum,
                actual_roles=dict(roles),synthetic_enemies_retained=20,mutation_cases=2,negative_cases=3,
                optimizer_steps=0,scope='Archived combat states only; units/obligations and full input integration pending')

if __name__=='__main__':
    result=run();Path('H:/MageKnight_RL_Runs/20261022_A_baseline_spec/combat_entity_gate.json').write_text(json.dumps(result,indent=2))
    print(json.dumps({k:v for k,v in result.items() if k!='files'}))
