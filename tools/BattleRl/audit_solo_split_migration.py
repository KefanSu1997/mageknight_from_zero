"""Read-only independent audit of actual manifests, with no test policy runs."""
import argparse,hashlib,json
from pathlib import Path

p=argparse.ArgumentParser()
for name in ('old','new','out'):p.add_argument('--'+name,required=True)
a=p.parse_args()
roots=[Path(a.old),Path(a.new)]
indexes=[json.loads((r/'index.json').read_text()) for r in roots]
counts={};all_families=set();all_tasks=set()
for split,worlds in (('train',1000),('validation',100),('test',100)):
    manifests=[]
    for root,index in zip(roots,indexes):
        entry=index['files'][split];assert entry['file']==split+'.json'
        raw=(root/entry['file']).read_bytes()
        assert hashlib.sha256(raw).hexdigest()==entry['sha256']
        manifest=json.loads(raw)
        assert manifest['split']==split and len(manifest['families'])==worlds
        assert len(manifest['tasks'])==worlds*7
        assert manifest['test_locked']==(split=='test')
        manifests.append(manifest)
    def setup_keys(manifest):
        return sorted(json.dumps(dict(setup=t['setup'],actor_seed=t['actor_seed']),sort_keys=True) for t in manifest['tasks'])
    assert setup_keys(manifests[0])==setup_keys(manifests[1]),'Task assignment/RNG changed: '+split
    new=manifests[1]
    family_keys={f['family_sha256'] for f in new['families']}
    assert len(family_keys)==worlds and not family_keys.intersection(all_families)
    all_families.update(family_keys)
    task_keys={t['id'] for t in new['tasks']}
    assert len(task_keys)==worlds*7 and not task_keys.intersection(all_tasks)
    all_tasks.update(task_keys)
    for family in family_keys:
        heroes=[t['setup']['Hero'] for t in new['tasks'] if t['family_sha256']==family]
        assert len(heroes)==7 and set(heroes)=={'Arythea','Tovak','Goldyx','Norowas','Wolfhawk','Krang','Braevalar'}
    counts[split]=dict(worlds=worlds,tasks=worlds*7)
report=dict(passed=True,counts=counts,split_assignment_and_actor_rng_preserved=True,
            disjoint_worlds=True,disjoint_tasks=True,test_policy_evaluated=False,
            new_index_sha256=hashlib.sha256((roots[1]/'index.json').read_bytes()).hexdigest())
Path(a.out).write_text(json.dumps(report,indent=2));print(json.dumps(report))
