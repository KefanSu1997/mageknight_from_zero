"""Delete only the explicitly authorized, SHA-verified stopped remote archive."""
import hashlib,json,os,shutil,time
from pathlib import Path
import psutil
root=Path('/root/autodl-tmp/mageknight')
source=root/'remote_entropy300'
stage=root/'optimization_stage_20261014'
assert source.resolve()==source and source.parent==root and source.is_dir()
rows=json.loads((stage/'entropy_offload_manifest.json').read_text())
assert len(rows)==21367 and sum(r['bytes'] for r in rows)==18835909487
expected={r['path']:r for r in rows}
assert len(expected)==len(rows)
seen=set()
for directory,dirs,files in os.walk(source):
 for name in dirs+files:assert not (Path(directory)/name).is_symlink()
 for name in files:seen.add((Path(directory)/name).relative_to(source).as_posix())
assert seen==set(expected),'Source inventory changed'
for name,row in expected.items():
 p=source/name
 assert p.resolve().is_relative_to(source) and p.stat().st_size==row['bytes']
 h=hashlib.sha256()
 with p.open('rb') as f:
  for block in iter(lambda:f.read(1048576),b''):h.update(block)
 assert h.hexdigest()==row['sha256'],name
for process in psutil.process_iter():
 try:
  if process.pid==os.getpid():continue
  assert not Path(process.cwd()).is_relative_to(source),('cwd dependency',process.pid)
  assert not any(str(source) in arg for arg in process.cmdline()),('command dependency',process.pid)
  for f in process.open_files():assert not Path(f.path).is_relative_to(source),('open file',process.pid,f.path)
 except psutil.NoSuchProcess:continue
report=dict(status='verified_before_delete',source=str(source),files=len(rows),bytes=sum(r['bytes'] for r in rows),archive_sha256='146c3fabfe46969107e98e1a03acc0a325955b6388ec372993aae3278bc71905',authorization='Human explicitly approved this verified remote old copy',time=time.time(),free_before=shutil.disk_usage(root).free)
receipt=stage/'entropy_verified_offload_complete.json'
receipt.write_text(json.dumps(report,indent=2))
shutil.rmtree(source)
assert not source.exists()
report.update(status='complete',free_after=shutil.disk_usage(root).free)
receipt.write_text(json.dumps(report,indent=2))
print(json.dumps(report))
