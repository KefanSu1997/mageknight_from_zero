import json,subprocess
from pathlib import Path
import psutil
stage=Path('optimization_stage_20261014')
assert not Path('remote_public_state71').exists()
assert json.loads(Path('remote_partitioned_clip61/status.json').read_text())['state']=='review_boundary'
assert Path('remote_partitioned_clip61/latest_checkpoint_bundle.json').exists()
with (stage/'public_state_resume.log').open('xb') as log:
 p=subprocess.Popen(['/root/autodl-tmp/mkvenv/bin/python','-u',str(stage/'resume_public_state71.py')],stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
report=dict(pid=p.pid,created=psutil.Process(p.pid).create_time(),command=psutil.Process(p.pid).cmdline())
(stage/'public_state_launch.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
