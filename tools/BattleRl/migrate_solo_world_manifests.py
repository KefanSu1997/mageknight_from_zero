"""Re-export every assigned world on a new runtime without reshuffling splits."""
import argparse,hashlib,json
from pathlib import Path
from solo_native_env import SoloNativeEnv
from solo_conquest_contract import family_digest
from solo_world_manifest import encoded


def run(a):
    source=Path(a.source);out=Path(a.out);out.mkdir(parents=True,exist_ok=False)
    index=json.loads((source/'index.json').read_text());new_index=dict(version=1,files={})
    migrated=set();old_seen=set();counts={};binding=None
    with SoloNativeEnv(a.dll,a.repo,out/'probe',preparation=True) as env:
        binding=env.capabilities['binding_sha256']
        catalog_sha=hashlib.sha256(encoded({k:v for k,v in env.capabilities['binding'].items()
            if k.startswith('resources/')})).hexdigest()
        for split,expected in [('train',1000),('validation',100),('test',100)]:
            entry=index['files'][split];raw=(source/entry['file']).read_bytes()
            assert entry['file']==split+'.json' and hashlib.sha256(raw).hexdigest()==entry['sha256']
            manifest=json.loads(raw);assert len(manifest['families'])==expected
            tasks_by={}
            for t in manifest['tasks']:tasks_by.setdefault(t['family_sha256'],[]).append(t)
            families=[];tasks=[]
            for family in manifest['families']:
                oldkey=family['family_sha256'];assert oldkey not in old_seen;old_seen.add(oldkey)
                oldworld=family['world']
                assert family_digest(oldworld)==oldkey
                group=tasks_by[oldkey];assert len(group)==7
                candidate=dict(oldworld,rules_sha256=binding)
                new_enemy=None
                for task in group:
                    env.reset(task['setup']);world=env._request(op='private_initial_world')
                    for key in ('map_tiles','city_colors','city_levels','enemy_supply','ruins','initial_mana'):
                        if key=='enemy_supply' and getattr(a,'enemy_shuffle_repair',False):
                            # Only order/placement may change; physical IDs/colors
                            # and the seven heroes' common world must stay bound.
                            def physical(supply):
                                return sorted((t['Id'],t['color']) for k in ('draw','in_use') for t in supply[k])
                            if physical(world[key])!=physical(oldworld[key]):
                                raise ValueError('Enemy inventory changed, not just shuffled')
                            if new_enemy is not None and world[key]!=new_enemy:
                                raise ValueError('Hero-dependent enemy world')
                            new_enemy=world[key]
                            candidate[key]=new_enemy
                            continue
                        if world[key]!=oldworld[key]:raise ValueError(f'{split}/{oldkey}/{key} changed')
                    hero=task['setup']['Hero']
                    for key in ('offer_supply','dummy_setup'):
                        if world[key]!=oldworld[key][hero]:raise ValueError(f'{split}/{oldkey}/{hero}/{key} changed')
                    if catalog_sha!=oldworld['catalog_sha256']:
                        raise ValueError('Printed catalog changed')
                key=family_digest(candidate);assert key not in migrated;migrated.add(key)
                families.append(dict(world=candidate,family_sha256=key))
                tasks.extend(dict(t,id=key+'/'+t['setup']['Hero'],family_sha256=key) for t in group)
                (out/'progress.json').write_text(json.dumps(dict(verified_worlds=len(migrated),total=1200,split=split)))
            payload=encoded(dict(manifest,families=families,tasks=tasks))
            filename=split+'.json';(out/filename).write_bytes(payload)
            new_index['files'][split]=dict(file=filename,sha256=hashlib.sha256(payload).hexdigest(),worlds=expected,episodes=len(tasks))
            counts[split]=len(tasks)
    (out/'migration.json').write_bytes(encoded(dict(source_index_sha256=hashlib.sha256((source/'index.json').read_bytes()).hexdigest(),
        native_binding=binding,worlds_verified=1200,hero_resets_verified=8400,split_membership_preserved=True,counts=counts,
        enemy_shuffle_repair=bool(getattr(a,'enemy_shuffle_repair',False)))))
    (out/'index.json').write_bytes(encoded(new_index))

if __name__=='__main__':
    p=argparse.ArgumentParser()
    for n in ('source','out','dll','repo'):p.add_argument('--'+n,required=True)
    p.add_argument('--enemy-shuffle-repair',action='store_true')
    run(p.parse_args())
