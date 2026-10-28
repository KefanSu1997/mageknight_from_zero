"""Serialized, isolated actual rollout equality tests; never launches training."""
import json,os,shutil,subprocess,sys,time
from pathlib import Path
stage=Path(__file__).resolve().parent
root=Path('/root/autodl-tmp/solo_conquest_score_v5')
(stage/'queue.lock').open('x').write(str(os.getpid()))
def status(**kw):
    (stage/'queue_status.json').write_text(json.dumps(dict(pid=os.getpid(),time=time.time(),**kw)))
proc=Path('/proc/882419')
try:
    assert b'run_solo_group_collection_gate.py' in (proc/'cmdline').read_bytes()
    identity=(proc/'stat').read_text().split(') ',1)[1].split()[19]
except FileNotFoundError:identity=None
while identity is not None:
    try:
        if (proc/'stat').read_text().split(') ',1)[1].split()[19]!=identity:break
    except FileNotFoundError:break
    status(state='waiting_group_gate',predecessor=882419);time.sleep(20)
previous=Path('/root/solo_group_prior_score_v5_gate_20261022/result.json')
if not previous.exists() or not json.loads(previous.read_text()).get('passed'):
    status(state='predecessor_failed_requires_review');sys.exit(1)
env=dict(os.environ,PYTHONPATH=str(stage)+':'+str(root/'tools/BattleRl'),
         PATH='/root/autodl-tmp/dotnet:'+os.environ['PATH'])
for workers,tasks in ((3,7),(32,32)):
    while shutil.disk_usage('/root').free<3.5*1024**3:
        status(state='waiting_space',workers=workers);time.sleep(30)
    output=Path('/root/solo_ready_batch_v5_gate_'+str(workers)+'_20261022')
    assert not output.exists()
    with (stage/('native_'+str(workers)+'.log')).open('x') as log:
        child=subprocess.Popen([sys.executable,'-u',str(stage/'run_solo_ready_batch_native_gate.py'),
            '--repo',str(root),'--baseline','/root/mageknight_solo_conquest_A_score_v5_100_20261022',
            '--out',str(output),'--workers',str(workers),'--tasks',str(tasks)],
            env=env,stdout=log,stderr=subprocess.STDOUT)
        status(state='collecting',workers=workers,tasks=tasks,child_pid=child.pid,output=str(output))
        code=child.wait()
    report=output/'result.json'
    if code or not report.exists() or not json.loads(report.read_text()).get('passed'):
        status(state='failed',workers=workers,returncode=code,output=str(output));sys.exit(1)
status(state='passed',formal_training_changed=False)
