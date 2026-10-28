"""Read-only world representation coverage on existing full-game trajectories."""
import collections
import hashlib
import json
from pathlib import Path
from baseline_a_world_inputs import encode_world
from baseline_a_world_entities import WorldEntityCodec


def audit(directory, assets):
    ids=json.loads((Path(assets)/'manifest.json').read_text())['entity_ids']
    codec=WorldEntityCodec('.',ids);files=[];states=0;roles=collections.Counter();failures=[];maximum=0
    for path in sorted(Path(directory).glob('episode_*/native.json')):
        data=path.read_bytes();doc=json.loads(data);files.append(dict(path=str(path),sha256=hashlib.sha256(data).hexdigest()))
        native=doc.get('native',doc)
        for record in native['records']:
            world=json.loads(record['ObservationBefore'])['World']
            try:
                encoded=encode_world(world);entities=codec.encode(encoded['records'])
                maximum=max(maximum,len(entities));roles.update(r['role'] for r in encoded['records']);states+=1
            except (ValueError,KeyError) as exc:
                failures.append(dict(path=str(path),step=record['BeforeStep'],error=str(exc)))
                break
    return dict(passed=bool(files) and not failures,files=files,public_states=states,roles=dict(roles),
                maximum_world_entities=maximum,failures=failures,attribute_names=codec.attribute_names,
                scope='Existing observed trajectories only; not coverage of all legal states or a training gate')


if __name__=='__main__':
    import argparse
    p=argparse.ArgumentParser();p.add_argument('directory');p.add_argument('assets');p.add_argument('output');a=p.parse_args()
    result=audit(a.directory,a.assets);Path(a.output).write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in ('files','attribute_names')}))
