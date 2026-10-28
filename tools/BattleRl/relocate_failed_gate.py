"""Lossless relocation of one stopped run, with SHA verification and stable symlink."""
import os,json,hashlib,shutil,time
from pathlib import Path
source=Path('/root/autodl-tmp/mageknight/remote_context_gate32')
target=Path('/root/mageknight_archive/remote_context_gate32')
assert source.resolve()==source and source.is_dir() and not target.exists()
assert source.parent==Path('/root/autodl-tmp/mageknight') and target.parent==Path('/root/mageknight_archive')
target.parent.mkdir(exist_ok=True)
report=target.parent/'relocation_failed_gate_report.json'
def sha(p):
 h=hashlib.sha256()
 with p.open('rb') as f:
  for b in iter(lambda:f.read(4*1024*1024),b''):h.update(b)
 return h.hexdigest()
paths=sorted(source.rglob('*'));assert not any(p.is_symlink() for p in paths)
files=[p for p in paths if p.is_file()];total=sum(p.stat().st_size for p in files)
assert shutil.disk_usage(target.parent).free>total+2*1024**3
rows=[];target.mkdir()
for number,p in enumerate(files):
 rel=p.relative_to(source);dest=target/rel;dest.parent.mkdir(parents=True,exist_ok=True)
 digest=sha(p);shutil.copy2(p,dest);assert sha(dest)==digest
 rows.append(dict(path=str(rel),bytes=p.stat().st_size,sha256=digest))
 if number%1000==0:print(number,len(files),flush=True)
assert [str(p.relative_to(source)) for p in files]==[str(p.relative_to(source)) for p in sorted(source.rglob('*')) if p.is_file()]
report.write_text(json.dumps(dict(status='verified_before_relocation',source=str(source),destination=str(target),bytes=total,files=rows)))
# Only after every source file has a verified duplicate; retain all evidence.
assert source.resolve()==Path('/root/autodl-tmp/mageknight/remote_context_gate32')
shutil.rmtree(source);source.symlink_to(target,target_is_directory=True)
assert source.resolve()==target
report.write_text(json.dumps(dict(status='complete',source=str(source),destination=str(target),bytes=total,files=rows)))
print(json.dumps(dict(complete=True,bytes=total,files=len(rows),free=shutil.disk_usage(source.parent).free)),flush=True)
