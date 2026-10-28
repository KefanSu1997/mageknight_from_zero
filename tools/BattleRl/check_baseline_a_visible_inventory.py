"""Audit actual full games plus explicit dynamic-slot and hidden-field checks."""
import collections,copy,hashlib,json
from pathlib import Path
import numpy as np
from baseline_a_visible_inventory import VisibleInventoryCodec,VISIBLE_ROLES
from baseline_a_world_entities import WORLD_ROLES
from public_entity_tokens import KINDS


def run():
    assets=Path('H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets')
    ids=json.loads((assets/'manifest.json').read_text())['entity_ids']
    offset=len(KINDS)+len(WORLD_ROLES);codec=VisibleInventoryCodec(ids,offset)
    directory=Path('H:/MageKnight_RL_Runs/autodl_20261013/solo_conquest_A_enemyshuffle_v6_100_20261022/rollout_0001')
    files=[];states=0;maximum=0;roles=collections.Counter();example=None
    for path in sorted(directory.glob('episode_*/native.json')):
        data=path.read_bytes();files.append(dict(path=str(path),sha256=hashlib.sha256(data).hexdigest()))
        for row in json.loads(data)['native']['records']:
            obs=json.loads(row['ObservationBefore'])
            if example is None and obs['Inventory']['Hand']:example=obs
            items=codec.encode(obs);states+=1;maximum=max(maximum,len(items))
            roles.update(VISIBLE_ROLES[r-offset] for _,r,_ in items)
            assert all(np.isfinite(a).all() for _,_,a in items)
    assert states>0
    expanded=copy.deepcopy(example)
    expanded['Inventory']['Hand']=[dict(example['Inventory']['Hand'][0],Slot=i) for i in range(40)]
    hand=[x for x in codec.encode(expanded) if x[1]==offset]
    assert len(hand)==40 and hand[-1][2][0]==39/32
    for fault in ('identity','hidden','slot'):
        bad=copy.deepcopy(expanded)
        if fault=='identity':bad['Inventory']['Hand'][0]['Id']='_missing_printed_'
        elif fault=='hidden':bad['Inventory']['Hand'][0]['FutureDraw']='hidden'
        else:bad['Inventory']['Hand'][0]['Slot']=999
        try:codec.encode(bad)
        except ValueError:pass
        else:raise AssertionError('Bad input accepted '+fault)
    return dict(passed=True,files=files,public_states=states,maximum_visible_entities=maximum,
                actual_roles=dict(roles),synthetic_slots_preserved=40,negative_cases=3,optimizer_steps=0,
                scope='Visible cards/skills/offers only; unit/combat/obligation fields and full policy integration pending')

if __name__=='__main__':
    result=run();Path('H:/MageKnight_RL_Runs/20261022_A_baseline_spec/visible_inventory_gate.json').write_text(json.dumps(result,indent=2))
    print(json.dumps({k:v for k,v in result.items() if k!='files'}))
