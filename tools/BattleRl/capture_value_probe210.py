"""Keep one exact completed checkpoint for a prospective CPU diagnostic."""
import io,json,zipfile,hashlib,time
from pathlib import Path
import psutil
stage=Path('optimization_stage_20261014');source=Path('remote_conditioned300/checkpoint.zip')
dest=stage/'value_probe_checkpoint0210.zip';report=stage/'value_probe_checkpoint0210.json'
parent=psutil.Process(122535)
assert 'optimization_stage_20261014/deploy_conditioned_after_gate_v2.py' in parent.cmdline()
identity=dict(pid=parent.pid,created=parent.create_time(),command=parent.cmdline())
assert not dest.exists() and not report.exists()
while True:
 assert parent.is_running() and parent.create_time()==identity['created'] and parent.status()!=psutil.STATUS_ZOMBIE
 raw=source.read_bytes()
 try:
  with zipfile.ZipFile(io.BytesIO(raw)) as z:
   assert z.testzip() is None;update=json.loads(z.read('data'))['completed_updates']
 except zipfile.BadZipFile:
  time.sleep(2);continue
 if update>210:raise RuntimeError('Missed exact 210; do not mislabel another checkpoint')
 if update==210:
  with dest.open('xb') as f:f.write(raw)
  digest=hashlib.sha256(raw).hexdigest();assert hashlib.sha256(dest.read_bytes()).hexdigest()==digest
  result=dict(update=update,sha256=digest,source=str(source),destination=str(dest),parent=identity,scope='Diagnostic copy only; not a training resume bundle. Planned evaluation on rollout_0210, generated after these weights.')
  report.write_text(json.dumps(result,indent=2));print(json.dumps(result));break
 time.sleep(15)
