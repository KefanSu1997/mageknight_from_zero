"""Restore archived Unity project files ONLY in an explicitly named isolated worktree.

Never changes primary project. Existing differences/extras are preserved outside
Unity's Assets roots; every path is checked before a move or overwrite.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def restore(worktree, manifest, blobs, audit):
    worktree=worktree.resolve();primary=Path(__file__).resolve().parents[2]
    if worktree==primary:raise ValueError('Primary workspace is forbidden')
    actual=Path(subprocess.check_output(['git','-C',str(worktree),'rev-parse','--show-toplevel'],text=True).strip()).resolve()
    if actual!=worktree:raise ValueError('Exact registered worktree root required')
    data=json.loads(manifest.read_text(encoding='utf8'))
    roots={'Assets','Packages','ProjectSettings','resources'}
    files={rel:row for rel,row in data['files'].items() if Path(rel).parts[0] in roots}
    audit.mkdir(parents=True,exist_ok=False);backup=audit/'checkout_before_restore';changed=[];extra=[]
    def inside(path):
        if not path.resolve().is_relative_to(worktree):raise ValueError('Path escapes isolated worktree')
        return path
    # The fresh checkout's extra Assets can affect compilation, import or scene
    # discovery. Move them to a recoverable location before restoring the archive.
    for name in roots:
        directory=worktree/name
        if directory.exists():
            for path in list(directory.rglob('*')):
                if not path.is_file():continue
                rel=path.relative_to(worktree).as_posix()
                if rel not in files:
                    target=inside(backup/'extras'/rel);inside(path)
                    target.parent.mkdir(parents=True,exist_ok=True);path.replace(target);extra.append(rel)
    for rel,row in sorted(files.items()):
        target=inside(worktree/rel);blob=blobs/row['sha256'][:2]/row['sha256']
        if sha(blob)!=row['sha256']:raise ValueError('Corrupt archived blob: '+rel)
        if target.is_file() and sha(target)==row['sha256']:continue
        if target.exists():
            saved=inside(backup/'changed'/rel);saved.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(target,saved)
        target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(blob,target);changed.append(rel)
    actual_inventory={p.relative_to(worktree).as_posix() for name in roots for p in (worktree/name).rglob('*') if p.is_file()}
    if actual_inventory!=set(files):raise AssertionError('Restored Unity file inventory differs')
    bad=[rel for rel,row in files.items() if sha(worktree/rel)!=row['sha256']]
    if bad:raise AssertionError(bad)
    value=dict(worktree=str(worktree),source_manifest=str(manifest.resolve()),manifest_sha256=sha(manifest),
               unity_roots=sorted(roots),verified_files=len(files),changed=changed,extras_preserved=extra,
               all_archived_unity_bytes_match=True,scope='Unity Assets/Packages/ProjectSettings/resources only; fresh Library must be generated, no old DLL or original capture time claim.')
    (audit/'restoration.json').write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding='utf8')
    return dict(verified_files=len(files),changed_files=len(changed),extra_files_preserved=len(extra),all_archived_unity_bytes_match=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ['worktree','manifest','blobs','audit']:parser.add_argument('--'+name,type=Path,required=True)
    a=parser.parse_args();print(json.dumps(restore(a.worktree,a.manifest.resolve(),a.blobs.resolve(),a.audit.resolve())))
