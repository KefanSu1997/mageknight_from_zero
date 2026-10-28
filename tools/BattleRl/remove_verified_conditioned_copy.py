"""Human-authorized removal of the exact verified historical remote copy."""
import os,json,hashlib,shutil,time
from pathlib import Path
import psutil
source=Path('/root/autodl-tmp/mageknight/remote_conditioned300')
episodes=Path('/root/mageknight_archive/remote_conditioned300_episodes')
stage=Path('/root/autodl-tmp/mageknight/optimization_stage_20261014')
assert source.resolve()==source and source.is_dir()
assert episodes.resolve()==episodes and episodes.is_dir()
assert (source/'episodes').is_symlink() and (source/'episodes').resolve()==episodes
assert source.parent==Path('/root/autodl-tmp/mageknight') and episodes.parent==Path('/root/mageknight_archive')
rows=json.loads((stage/'conditioned_offload_manifest.json').read_text())
assert len(rows)==4304 and sum(r['bytes'] for r in rows)==3439509759
expected={r['path']:r for r in rows}
seen=set()
for directory,dirs,files in os.walk(source,followlinks=True):
 for n in dirs+files:
  p=Path(directory)/n
  if p.is_symlink():assert p==source/'episodes'
 for n in files:seen.add((Path(directory)/n).relative_to(source).as_posix())
assert seen==set(expected),'Source inventory changed'
for name,row in expected.items():
 p=source/name;resolved=p.resolve()
 assert resolved.is_relative_to(source) or resolved.is_relative_to(episodes)
 assert p.stat().st_size==row['bytes']
 h=hashlib.sha256()
 with p.open('rb') as f:
  for b in iter(lambda:f.read(1048576),b''):h.update(b)
 assert h.hexdigest()==row['sha256'],name
for process in psutil.process_iter(['pid','cmdline']):
 try:
  if process.pid==os.getpid():continue
  for f in process.open_files():
   path=Path(f.path)
   assert not path.is_relative_to(source) and not path.is_relative_to(episodes),('Historical file still open',process.pid,str(path))
 except (psutil.NoSuchProcess,psutil.AccessDenied):continue
report=dict(status='verified_before_delete',files=len(rows),bytes=sum(r['bytes'] for r in rows),source=str(source),relocated_episodes=str(episodes),local_archive='H:/MageKnight_RL_Runs/autodl_20261013/verified_archives/remote_conditioned300/archive.tar',archive_sha256='08ee2ce5fcd168ba7b5c8dd34abde446b0e4c1d660cb981bdd6b42d90056216a',authorization='Human explicitly approved deleting this verified remote old copy',time=time.time())
report_path=stage/'conditioned_verified_offload_complete.json'
report_path.write_text(json.dumps(report,indent=2))
# Both exact absolute roots checked above. rmtree does not follow the episode link.
shutil.rmtree(source)
shutil.rmtree(episodes)
assert not source.exists() and not episodes.exists()
report.update(status='complete',data_free=shutil.disk_usage(source.parent).free,system_free=shutil.disk_usage(episodes.parent).free)
report_path.write_text(json.dumps(report,indent=2));print(json.dumps(report))
