"""Gate world projection on actual native public observations (no optimizer)."""
import copy
import json
from pathlib import Path
import numpy as np
from baseline_a_world_inputs import encode_world


def run(path):
    worlds=json.loads(Path(path).read_text(encoding='utf-8-sig'))
    encoded=[encode_world(w) for w in worlds]
    if len({r['numeric_schema_sha256'] for r in encoded})!=1:
        raise AssertionError('Numerical schema varies with public state')
    for world,result in zip(worlds,encoded):
        shuffled=copy.deepcopy(world); shuffled['Cells'].reverse()
        shuffled['ExplorationPositions'].reverse()
        second=encode_world(shuffled)
        np.testing.assert_array_equal(result['numeric'],second['numeric'])
        assert result['records']==second['records']
        assert np.isfinite(result['numeric']).all()
    rejected=[]
    def reject(name, world):
        try: encode_world(world)
        except ValueError: rejected.append(name)
        else: raise AssertionError('Accepted '+name)
    bad=copy.deepcopy(worlds[0]);bad['HiddenDrawOrder']=[];reject('hidden extra field',bad)
    bad=copy.deepcopy(worlds[0]);bad['Round']=float('nan');reject('nonfinite clock',bad)
    bad=copy.deepcopy(worlds[0]);bad['PlayerTactic']='invented';reject('unknown tactic',bad)
    bad=copy.deepcopy(worlds[0]);bad['Cells'].append(copy.deepcopy(bad['Cells'][0]));reject('duplicate hex',bad)
    bad=copy.deepcopy(worlds[0]);site=next(c['Site'] for c in bad['Cells'] if c['Site'])
    site['Guards']=[dict(Slot=0,BackColor='Green',Revealed=False,DefinitionId='monster_000')]
    reject('unrevealed guard identity',bad)
    bad=copy.deepcopy(worlds[0]);site=next(c['Site'] for c in bad['Cells'] if c['Site'])
    site['TokenRevealed']=False;site['RuinDetails']={};reject('unrevealed ruin details',bad)
    # Public clock changes must reach the numeric branch, not only metadata.
    changed=copy.deepcopy(worlds[0]);changed['Dummy']['Remaining']-=1
    assert not np.array_equal(encoded[0]['numeric'],encode_world(changed)['numeric'])
    return dict(passed=True,actual_native_states=len(worlds),numeric_width=len(encoded[0]['numeric']),
        numeric_names=encoded[0]['names'],numeric_schema_sha256=encoded[0]['numeric_schema_sha256'],
        record_counts=[len(r['records']) for r in encoded],negative_cases_rejected=rejected,
        scope='Public world projection; entity tensor binding and end-to-end A policy remain pending')


if __name__=='__main__':
    import argparse
    p=argparse.ArgumentParser();p.add_argument('input');p.add_argument('output');a=p.parse_args()
    result=run(a.input);Path(a.output).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in ('numeric_names','record_counts')}))
