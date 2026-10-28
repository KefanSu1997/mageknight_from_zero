"""Lossless relocation of stopped historical episodes; no evidence discarded."""
import hashlib,json,shutil,time
from pathlib import Path
source=Path('/root/autodl-tmp/mageknight/remote_conditioned300/episodes')
target=Path('/root/mageknight_archive/remote_conditioned300_episodes')
report=target.parent/'relocation_conditioned_episodes_report.json'
assert source.resolve()==source and source.is_dir() and not target.exists()
assert source.parent==Path('/root/autodl-tmp/mageknight/remote_conditioned300')
assert target.parent.resolve()==Path('/root/mageknight_archive')
files=sorted(p for p in source.rglob('*') if p.is_file())
assert not any(p.is_symlink() for p in source.rglob('*'))
total=sum(p.stat().st_size for p in files)
assert shutil.disk_usage(target.parent).free>total+2*1024**3
stats={p.relative_to(source).as_posix():(p.stat().st_size,p.stat().st_mtime_ns) for p in files}
def sha(p):
 h=hashlib.sha256()
 with p.open('rb') as f:
  for b in iter(lambda:f.read(1048576),b''):h.update(b)
 return h.hexdigest()
rows=[];target.mkdir()
for p in files:
 rel=p.relative_to(source);dest=target/rel;dest.parent.mkdir(parents=True,exist_ok=True)
 before=sha(p);shutil.copy2(p,dest);assert sha(dest)==before
 rows.append(dict(path=rel.as_posix(),bytes=p.stat().st_size,sha256=before))
assert {p.relative_to(source).as_posix():(p.stat().st_size,p.stat().st_mtime_ns) for p in source.rglob('*') if p.is_file()}==stats
report.write_text(json.dumps(dict(status='verified_before_relocation',source=str(source),target=str(target),bytes=total,files=rows)))
assert source.resolve()==Path('/root/autodl-tmp/mageknight/remote_conditioned300/episodes')
shutil.rmtree(source);source.symlink_to(target,target_is_directory=True)
assert source.resolve()==target and all(sha(source/r['path'])==r['sha256'] for r in rows)
report.write_text(json.dumps(dict(status='complete',source=str(source),target=str(target),bytes=total,files=rows,time=time.time())))
print(json.dumps(dict(status='complete',files=len(rows),bytes=total,data_free=shutil.disk_usage(source.parent).free,system_free=shutil.disk_usage(target).free)))
