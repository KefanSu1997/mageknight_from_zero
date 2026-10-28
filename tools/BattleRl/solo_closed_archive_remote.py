"""Read-only manifest of completed solo rollouts/evaluations/checkpoints."""
import hashlib,json,sys
from pathlib import Path
ROOT=Path('/root/mageknight_solo_conquest_A_forest100_20261022')
def digest(path):
    h=hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(8*1024*1024),b''):h.update(block)
    return h.hexdigest()
def groups():
    result={}
    if not ROOT.exists():return result
    metrics=ROOT/'metrics.json'
    if metrics.exists():
        for row in json.loads(metrics.read_text()):
            name=f"rollout_{row['update']:04d}";folder=ROOT/name
            if (folder/'collection.json').exists():result[name]=folder
    for folder in ROOT.glob('eval_[0-9]*'):
        if (folder/'summary.json').exists():result[folder.name]=folder
    # result.json is written last by finish(), after native/context archives.
    # Preserve finished episodes even while the rest of a 128/700-task group runs.
    for report in ROOT.glob('**/episode_*/result.json'):
        try:
            row=json.loads(report.read_text())
        except (FileNotFoundError,json.JSONDecodeError):
            continue
        if not isinstance(row.get('eligible_complete_episode'),bool):continue
        folder=report.parent
        name='__'.join(folder.relative_to(ROOT).parts)
        result[name]=folder
    for receipt in ROOT.glob('checkpoint_[0-9]*.json'):
        model=receipt.with_suffix('.pt')
        if model.exists():result[receipt.stem]=[receipt,model]
    return result
available=groups()
if sys.argv[1]=='list':print(json.dumps(sorted(available)))
elif sys.argv[1]=='manifest':
    name=sys.argv[2];selection=available[name]
    files=selection if isinstance(selection,list) else sorted(p for p in selection.rglob('*') if p.is_file())
    rows=[]
    for path in files:
        if path.is_symlink() or not path.resolve().is_relative_to(ROOT.resolve()):raise ValueError('Unsafe archive path')
        before=path.stat();sha=digest(path);after=path.stat()
        if (before.st_size,before.st_mtime_ns)!=(after.st_size,after.st_mtime_ns):raise ValueError('Closed file changed')
        rows.append(dict(path=path.relative_to(ROOT).as_posix(),bytes=after.st_size,sha256=sha))
    print(json.dumps(dict(root=str(ROOT),group=name,files=rows)))
else:raise ValueError('Read-only mode required')
