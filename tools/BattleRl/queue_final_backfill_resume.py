"""Serial authorized historical evaluation followed by checkpoint-89 resume."""
import json,sys,subprocess,time,shutil,hashlib
from pathlib import Path
import psutil
root=Path('/root/autodl-tmp/mageknight');stage=root/'optimization_stage_20261014'
for p in psutil.process_iter(['pid','cmdline']):
    if p.pid==psutil.Process().pid:continue
    assert not any(Path(x).name in ('queue_final_backfill_resume.py','evaluate_final_checkpoint.py','run_final_eval_resume.py','run_final_ready.py') for x in (p.info['cmdline'] or [])[1:]),p.pid
assert json.loads((root/'authorized_cleanup_complete_20261015.json').read_text())['complete']
gate=json.loads((stage/'final_eval_gate.json').read_text());assert gate['passed']
for n,h in gate['sources'].items():assert hashlib.sha256((stage/n).read_bytes()).hexdigest()==h
def status(phase,**kw):
    (stage/'backfill_resume_status.json').write_text(json.dumps(dict(phase=phase,pid=psutil.Process().pid,created=psutil.Process().create_time(),time=time.time(),**kw),indent=2))
for update,checkpoint in [(0,'remote_final_semantic300/initial_checkpoint.zip'),(8,'remote_final_semantic300/checkpoint.zip'),(50,'remote_final_semantic_fast300/behavior_0050_step80646.zip')]:
    output=root/f'remote_final_backfill/eval_{update:04d}'
    if (output/'summary.json').exists():
        result=json.loads((output/'summary.json').read_text());assert result['episodes']==1000 and result['update']==update
        continue
    assert shutil.disk_usage(root).free>4*1024**3
    status('backfill',update=update)
    with (stage/f'final_eval{update}.log').open('w') as log:
        child=subprocess.Popen([sys.executable,'-u',str(stage/'evaluate_final_checkpoint.py'),'--checkpoint',checkpoint,'--update',str(update),'--workers','32','--out',str(output)],cwd=root,stdout=log,stderr=subprocess.STDOUT)
        status('backfill',update=update,child_pid=child.pid)
        assert child.wait()==0, f'Backfill {update} failed; inspect preserved log'
assert shutil.disk_usage(root).free>6*1024**3
output=root/'remote_final_eval_resume300'
assert not output.exists()
status('starting_resume',completed=89)
with (stage/'final_eval_resume300.log').open('w') as log:
    child=subprocess.Popen([sys.executable,'-u',str(stage/'run_final_eval_resume.py'),'--mode','formal','--out',str(output)],cwd=root,stdout=log,stderr=subprocess.STDOUT)
    status('training',child_pid=child.pid,from_update=89,goal=300)
    result=child.wait()
status('finished' if result==0 else 'failed',exit_code=result)
