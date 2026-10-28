"""Export actual native inventories for explicit seven-hero families.

Input is a list of families, each containing seven tasks with native setup and
actor_seed. Never infer inventory from a seed. Partial output is retained on
error; formal split materialization requires all 1200 families.
"""
import argparse
import hashlib
import json
from pathlib import Path
from solo_native_env import SoloNativeEnv
from solo_world_manifest import HEROES, encoded, write_manifests
from solo_conquest_contract import family_digest


def export(args):
    rows=json.loads(Path(args.setups).read_text(encoding='utf-8'))
    out=Path(args.out);out.mkdir(parents=True,exist_ok=False)
    exports=[];seen=set()
    with SoloNativeEnv(args.dll,args.repo,out/'native',preparation=True) as env:
        binding=env.capabilities['binding_sha256']
        catalog={k:v for k,v in env.capabilities['binding'].items() if k.startswith('resources/')}
        catalog_sha=hashlib.sha256(encoded(catalog)).hexdigest()
        for number,row in enumerate(rows):
            tasks=row['tasks']
            if len(tasks)!=7 or {t['setup']['Hero'] for t in tasks}!=set(HEROES):
                raise ValueError('Seven distinct original heroes required')
            inventory={}
            for task in tasks:
                if task['setup'].get('DualColorRule')!=1:raise ValueError('Dummy rule differs')
                env.reset(task['setup'])
                before=env.export()
                world=env._request(op='private_initial_world')
                if env.export()!=before:raise ValueError('World export altered native state')
                if not world['complete_ruin_inventory']:raise ValueError('Ruin supply missing')
                inventory[task['setup']['Hero']]=world
            # Hero-specific skill offers and unused dummy heroes are kept in one
            # family. No hero can leak into a different train/evaluation split.
            reference=inventory[HEROES[0]]
            for hero in HEROES[1:]:
                for key in ('map_tiles','city_colors','city_levels','enemy_supply','ruins','initial_mana'):
                    if inventory[hero][key]!=reference[key]:
                        raise ValueError(f'Family {number}: shared {key} differs for {hero}')
            canonical={k:reference[k] for k in ('map_tiles','city_colors','city_levels','enemy_supply','ruins','initial_mana')}
            for key in ('offer_supply','dummy_setup'):
                canonical[key]={h:inventory[h][key] for h in HEROES}
            canonical.update(rules_sha256=binding,catalog_sha256=catalog_sha)
            digest=family_digest(canonical)
            if digest in seen:raise ValueError('Duplicate actual world family')
            seen.add(digest)
            result=dict(world=canonical,tasks=tasks)
            (out/f'family_{number:04d}.json').write_bytes(encoded(result))
            exports.append(result)
            (out/'progress.json').write_bytes(encoded(dict(exported=len(exports),requested=len(rows),binding=binding)))
            print(json.dumps(dict(exported=len(exports),family_sha256=digest)),flush=True)
    if len(exports)==1200:
        write_manifests(exports,out/'manifests')
    (out/'complete.json').write_bytes(encoded(dict(families=len(exports),formal_split_created=len(exports)==1200)))


if __name__=='__main__':
    p=argparse.ArgumentParser()
    for name in ('repo','dll','setups','out'):p.add_argument('--'+name,required=True)
    export(p.parse_args())
