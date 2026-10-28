"""One-shot, sequential fidelity and timing checks; never launches formal training."""
import json, os, shutil, subprocess, time
from pathlib import Path
root=Path('/root/autodl-tmp/solo_v6_sampling_comparison_retry1_20261022')
root.mkdir(exist_ok=False)
runtime=Path('/root/autodl-tmp/solo_conquest_enemyshuffle_v6')
baseline='/root/mageknight_solo_conquest_A_enemyshuffle_v6_100_20261022'
def status(state,**kw):
    (root/'status.json').write_text(json.dumps(dict(state=state,pid=os.getpid(),time=time.time(),**kw)))
status('waiting_sequence_gate',wait_pid=224578)
while True:
    p=Path('/proc/224578/stat')
    if not p.exists():break
    fields=p.read_text().split(') ',1)[1].split()
    if fields[19]!='424652877':raise RuntimeError('Predecessor PID reused')
    if fields[0]=='Z':break
    time.sleep(10)
gate=json.loads(Path('/root/autodl-tmp/solo_v6_sequence_gate_20261022/sequence_ppo_gate/result.json').read_text())
assert gate['passed'], 'Sequence gate rejected; preserve evidence'
env=dict(os.environ,PATH='/root/autodl-tmp/dotnet:'+os.environ['PATH'],
         PYTHONPATH=str(runtime/'tools/BattleRl'))
results={}
for label,tasks,serial in [('serial32',32,True),('batch32',32,False),('batch128',128,False)]:
    free=shutil.disk_usage(root).free
    if free < 3.25*1024**3:
        status('storage_required_before_next_gate',next_gate=label,free_bytes=free,results=results)
        raise SystemExit(2)
    command=['/root/autodl-tmp/mkvenv/bin/python','/root/autodl-tmp/run_solo_ready_batch_v6_native_gate.py',
             '--repo',str(runtime),'--baseline',baseline,'--out',str(root/label),
             '--workers','32','--tasks',str(tasks)]
    if serial:command.append('--serial')
    with (root/(label+'.log')).open('x') as log:
        child=subprocess.Popen(command,env=env,stdout=log,stderr=subprocess.STDOUT)
        status('running',gate=label,child_pid=child.pid,command=command)
        code=child.wait()
    if code:
        status('failed',gate=label,returncode=code)
        raise SystemExit(code)
    result=json.loads((root/label/'result.json').read_text())
    assert result['passed']
    results[label]=result
status('completed',results=results,
       paired32_speedup=results['serial32']['wall_seconds']/results['batch32']['wall_seconds'],
       formal_training_started=False)
