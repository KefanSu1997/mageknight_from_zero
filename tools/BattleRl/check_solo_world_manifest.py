"""Synthetic split-integrity tests; no original world generation claim."""
import copy
import json
import tempfile
from pathlib import Path
from solo_world_manifest import HEROES, assemble, write_manifests, read_tasks


def check():
    rows=[]
    for i in range(1200):
        world=dict(map_tiles=['synthetic_world_'+str(i)],city_colors=['fixture_a','fixture_b'],
                   city_levels=[5,8],enemy_supply=['fixture'],offer_supply=['fixture'],
                   dummy_setup=['fixture'],ruins=['fixture'],initial_mana=['fixture'],rules_sha256='synthetic',catalog_sha256='synthetic')
        tasks=[dict(setup=dict(Version=1,Hero=h,DummyHero=HEROES[(j+1)%7],DualColorRule=1,Seeds={'Tiles':i+1}),
                    actor_seed=i*7+j) for j,h in enumerate(HEROES)]
        rows.append(dict(world=world,tasks=tasks))
    first=assemble(rows)
    assert first==assemble(list(reversed(rows)))
    families=[{r['family_sha256'] for r in first[s]['families']} for s in ('train','validation','test')]
    assert all(not families[i]&families[j] for i in range(3) for j in range(i))
    assert [len(first[s]['tasks']) for s in ('train','validation','test')]==[7000,700,700]
    root=Path(tempfile.mkdtemp(prefix='solo_manifest_gate_'))
    index=write_manifests(rows,root/'synthetic')
    assert len(read_tasks(root/'synthetic','validation'))==700
    rejected=0
    def reject(fn):
        nonlocal rejected
        try:fn()
        except ValueError:rejected+=1;return
        raise AssertionError('Invalid manifest accepted')
    reject(lambda:read_tasks(root/'synthetic','test'))
    assert len(read_tasks(root/'synthetic','test',final_test=True))==700
    duplicate=copy.deepcopy(rows);duplicate[-1]['world']=duplicate[0]['world']
    reject(lambda:assemble(duplicate))
    missing=copy.deepcopy(rows);missing[0]['tasks'].pop()
    reject(lambda:assemble(missing))
    same_hero=copy.deepcopy(rows);same_hero[0]['tasks'][0]['setup']['DummyHero']='Arythea'
    reject(lambda:assemble(same_hero))
    for rule in (None, 0, 2):
        bad_rule=copy.deepcopy(rows)
        bad_rule[0]['tasks'][0]['setup']['DualColorRule']=rule
        reject(lambda:assemble(bad_rule))
    with (root/'synthetic/validation.json').open('ab') as stream:stream.write(b' ')
    reject(lambda:read_tasks(root/'synthetic','validation'))
    report=dict(passed=True,scope='synthetic_split_integrity_only',native_worlds_generated=0,
                episode_counts=[7000,700,700],input_order_independent=True,disjoint=True,
                rejected_cases=rejected,evidence=str(root))
    (root/'gate.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print(json.dumps(report))


if __name__=='__main__':check()
