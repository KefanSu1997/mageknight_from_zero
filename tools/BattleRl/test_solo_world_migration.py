"""Synthetic integrity checks, not proof of actual native inventory equality."""
import copy,json,tempfile,unittest
from pathlib import Path
from pair_solo_world_exports import pair
from solo_world_manifest import HEROES,write_manifests,read_tasks,encoded

class MigrationTests(unittest.TestCase):
    def test_preserves_membership_and_rejects_inventory_change(self):
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary);old=root/'old';new=root/'new';old.mkdir();new.mkdir();exports=[]
            for i in range(1200):
                world=dict(map_tiles=[str(i)],city_colors=['a','b'],city_levels=[5,8],enemy_supply=['fixture'],
                    offer_supply=['fixture'],dummy_setup=['fixture'],ruins=['fixture'],initial_mana=['fixture'],
                    rules_sha256='old',catalog_sha256='same')
                tasks=[dict(setup=dict(Version=1,Hero=h,DummyHero=HEROES[(j+1)%7],DualColorRule=1,Seeds={'Tiles':i}),actor_seed=i*7+j) for j,h in enumerate(HEROES)]
                row=dict(world=world,tasks=tasks);exports.append(row)
                (old/f'family_{i:04d}.json').write_bytes(encoded(row))
                other=copy.deepcopy(row);other['world']['rules_sha256']='new'
                (new/f'family_{i:04d}.json').write_bytes(encoded(other))
            for path in (old,new):(path/'complete.json').write_text(json.dumps(dict(families=1200,formal_split_created=True)))
            write_manifests(exports,old/'manifests')
            pair(old,new,root/'paired')
            for split in ('train','validation','test'):
                a=read_tasks(old/'manifests',split,final_test=True);b=read_tasks(root/'paired',split,final_test=True)
                self.assertEqual([(t['setup'],t['actor_seed']) for t in a],[(t['setup'],t['actor_seed']) for t in b])
                self.assertTrue(all(x['family_sha256']!=y['family_sha256'] for x,y in zip(a,b)))
            corrupted=json.loads((new/'family_0001.json').read_text());corrupted['world']['ruins']=['different']
            (new/'family_0001.json').write_text(json.dumps(corrupted))
            with self.assertRaises(ValueError):pair(old,new,root/'must_not_publish')
            self.assertFalse((root/'must_not_publish/index.json').exists())

if __name__=='__main__':unittest.main()
