"""Deterministic scenario setups; inventory identity is computed by native export."""
import argparse
import hashlib
import json
from pathlib import Path
from solo_world_manifest import HEROES


def seed(family, stream):
    return int.from_bytes(hashlib.sha256(f'solo-world-v1/{family}/{stream}'.encode()).digest()[:4],'big') % (2**31-1)


def generate():
    families=[]
    for i in range(1200):
        shared={name:seed(i,name) for name in ('Tiles','Enemies','Mana','Offers','RoundShuffle',
            'Progression','Dummy','Tactics','DummySkills','Ruins','AdventureRewards','Effects','TacticEffects')}
        tasks=[]
        for hero in HEROES:
            # Goldyx GGB is explicit in the official rulebook's dummy example.
            # Tovak RBB is the documented alternate when Goldyx is the player.
            dummy='Tovak' if hero=='Goldyx' else 'Goldyx'
            crystals=[1,2,0,0] if dummy=='Tovak' else [0,1,2,0]
            setup=dict(Version=1,Hero=hero,DummyHero=dummy,DummyCrystals=crystals,
                       DualColorRule=1,Seeds=dict(shared,Player=seed(i,'Player/'+hero)))
            tasks.append(dict(setup=setup,actor_seed=seed(i,'Actor/'+hero)))
        families.append(dict(tasks=tasks))
    return families


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--out',required=True);args=p.parse_args()
    with Path(args.out).open('x',encoding='utf-8') as f:json.dump(generate(),f,indent=2)
