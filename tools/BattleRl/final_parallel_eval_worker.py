"""One evaluator, 32 workers; historical and new immutable checkpoint jobs."""
import json,time,sys,subprocess,hashlib
from pathlib import Path
import psutil
root=Path('/root/autodl-tmp/mageknight');stage=root/'optimization_stage_20261014';train=root/'remote_final_eval_resume300'
def status(phase,**extra):
    (stage/'parallel_eval_status.json').write_text(json.dumps(dict(phase=phase,pid=psutil.Process().pid,time=time.time(),**extra)))
def evaluate(step,checkpoint,out):
    if (out/'summary.json').exists():
        result=json.loads((out/'summary.json').read_text());assert result['episodes']==1000
        return result
    assert not out.exists(),f'Incomplete output requires recovery: {out}'
    with (stage/f'parallel_eval_{step:04d}.log').open('w') as log:
        p=subprocess.Popen([sys.executable,'-u',str(stage/'evaluate_final_checkpoint.py'),'--checkpoint',str(checkpoint),'--update',str(step),'--workers','32','--out',str(out)],cwd=root,stdout=log,stderr=subprocess.STDOUT)
        status('evaluating',update=step,child_pid=p.pid)
        assert p.wait()==0,f'Evaluation {step} failed'
    return json.loads((out/'summary.json').read_text())
initial=root/'remote_final_backfill/eval_0000/summary.json'
assert json.loads(initial.read_text())['episodes']==1000
while True:
    for jobfile in sorted((train/'eval_jobs').glob('*.json')):
        job=json.loads(jobfile.read_text());step=job['update'];checkpoint=Path(job['checkpoint'])
        assert step > 0 and step % 20 == 0, f'Nonstandard evaluation job: {step}'
        assert hashlib.sha256(checkpoint.read_bytes()).hexdigest()==job['sha256']
        result=evaluate(step,checkpoint,train/f'eval_{step:04d}')
        metrics=train/'evaluation_metrics.jsonl'
        completed={json.loads(line)['update'] for line in metrics.read_text().splitlines()} if metrics.exists() else set()
        if step not in completed:
            with metrics.open('a') as f:f.write(json.dumps({k:v for k,v in result.items() if k!='records'})+'\n')
    if (train/'complete.json').exists() and (train/'eval_0300/summary.json').exists():break
    status('waiting_checkpoint');time.sleep(10)
status('complete')
