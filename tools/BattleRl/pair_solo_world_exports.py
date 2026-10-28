"""Verify complete old/new native exports and retain original family assignment."""
import argparse,hashlib,json
from pathlib import Path
from solo_conquest_contract import family_digest
from solo_world_manifest import encoded


def pair(old,new,out):
    old=Path(old);new=Path(new);out=Path(out)
    for root in (old,new):
        complete=json.loads((root/'complete.json').read_text())
        if complete['families']!=1200 or not complete['formal_split_created']:
            raise ValueError('Both native exports must finish all 1200 worlds')
    mapping={};identities=set();old_bindings=set();new_bindings=set();evidence={}
    for i in range(1200):
        name=f'family_{i:04d}.json';a_raw=(old/name).read_bytes();b_raw=(new/name).read_bytes()
        a=json.loads(a_raw);b=json.loads(b_raw)
        if a['tasks']!=b['tasks']:raise ValueError('Hero/setup/actor RNG changed: '+name)
        aw=dict(a['world']);bw=dict(b['world'])
        old_bindings.add(aw.pop('rules_sha256'));new_bindings.add(bw.pop('rules_sha256'))
        if aw!=bw:raise ValueError('Actual native inventory changed: '+name)
        ak=family_digest(a['world']);bk=family_digest(b['world'])
        if ak in mapping or bk in identities:raise ValueError('Duplicate world')
        mapping[ak]=(bk,b['world']);identities.add(bk)
        evidence[name]=dict(old_sha256=hashlib.sha256(a_raw).hexdigest(),new_sha256=hashlib.sha256(b_raw).hexdigest())
    if len(old_bindings)!=1 or len(new_bindings)!=1:raise ValueError('Mixed runtime exports')
    index=json.loads((old/'manifests/index.json').read_text());result=dict(version=1,files={});used=set();payloads={}
    for split,count in [('train',1000),('validation',100),('test',100)]:
        entry=index['files'][split];raw=(old/'manifests'/entry['file']).read_bytes()
        if entry['file']!=split+'.json' or hashlib.sha256(raw).hexdigest()!=entry['sha256']:
            raise ValueError('Source split changed')
        manifest=json.loads(raw)
        if len(manifest['families'])!=count or len(manifest['tasks'])!=count*7:raise ValueError('Split size changed')
        families=[];tasks=[]
        for family in manifest['families']:
            key=family['family_sha256']
            if key in used or family_digest(family['world'])!=key:raise ValueError('Split overlap/corruption')
            used.add(key);nk,world=mapping[key];families.append(dict(family_sha256=nk,world=world))
        for task in manifest['tasks']:
            nk,_=mapping[task['family_sha256']]
            tasks.append(dict(task,id=nk+'/'+task['setup']['Hero'],family_sha256=nk))
        payload=encoded(dict(manifest,families=families,tasks=tasks));name=split+'.json';payloads[name]=payload
        result['files'][split]=dict(file=name,sha256=hashlib.sha256(payload).hexdigest(),worlds=count,episodes=count*7)
    if len(used)!=1200:raise ValueError('Incomplete source assignment')
    out.mkdir(parents=True,exist_ok=False)
    for name,payload in payloads.items():(out/name).write_bytes(payload)
    (out/'migration.json').write_bytes(encoded(dict(version=1,passed=True,worlds=1200,heroes_per_world=7,
        inventories_identical=True,split_membership_preserved=True,old_bindings=sorted(old_bindings),
        new_bindings=sorted(new_bindings),files=evidence)))
    (out/'index.json').write_bytes(encoded(result))
    return result

if __name__=='__main__':
    p=argparse.ArgumentParser()
    for n in ('old','new','out'):p.add_argument('--'+n,required=True)
    a=p.parse_args();print(json.dumps(pair(a.old,a.new,a.out)))
