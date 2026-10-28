import json,subprocess,sys,time
from pathlib import Path
import psutil
root=Path('/root/autodl-tmp/mageknight');stage=root/'optimization_stage_20261014'
p=psutil.Process(236871)
identity=dict(pid=p.pid,created=p.create_time(),command=p.cmdline(),children=[c.pid for c in p.children()])
assert any(Path(x).name=='queue_final_backfill_resume.py' for x in p.cmdline()) and 236872 in identity['children']
assert 'initial_checkpoint.zip' in ' '.join(psutil.Process(236872).cmdline())
(stage/'serial_queue_retired_identity.json').write_text(json.dumps(identity,indent=2))
assert not (root/'remote_final_eval_resume300').exists()
p.terminate();p.wait(timeout=10) # Only the queue; its existing evaluator continues.
assert psutil.pid_exists(236872)
started={}
for name,args,logname in [('train',['run_final_eval_resume.py','--mode','formal','--out','remote_final_eval_resume300'],'final_eval_resume300.log'),('evaluation_queue',['final_parallel_eval_worker.py'],'parallel_eval_worker.log')]:
    with (stage/logname).open('w') as log:
        child=subprocess.Popen([sys.executable,'-u',str(stage/args[0]),*args[1:]],cwd=root,stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
    started[name]=dict(pid=child.pid,created=psutil.Process(child.pid).create_time(),command=psutil.Process(child.pid).cmdline())
started.update(time=time.time(),existing_evaluation_pid=236872,train_workers=32,evaluation_workers=32)
(stage/'parallel_deployment.json').write_text(json.dumps(started,indent=2));print(json.dumps(started))
