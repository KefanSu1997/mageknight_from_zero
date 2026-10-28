"""Run isolated CUDA equivalence only after the sampling comparison stops."""
import json,os,shutil,subprocess,time
from pathlib import Path
stage=Path('/root/solo_v6_state_batch_gate_20261022')
runtime=Path('/root/autodl-tmp/solo_conquest_enemyshuffle_v6')
base=Path('/root/mageknight_solo_conquest_A_enemyshuffle_v6_100_20261022')
with (stage/'queue_identity.json').open('x') as f:json.dump(dict(pid=os.getpid()),f)
def status(state,**kw):
    (stage/'queue_status.json').write_text(json.dumps(dict(state=state,pid=os.getpid(),time=time.time(),**kw)))
status('waiting_sampling_comparison',wait_pid=225139)
while True:
    p=Path('/proc/225139/stat')
    if not p.exists():break
    row=p.read_text().split(') ',1)[1].split()
    if row[19]!='424700474':raise RuntimeError('Predecessor PID reused')
    if row[0]=='Z':break
    time.sleep(10)
free=shutil.disk_usage(stage).free
if free<3*1024**3:
    status('insufficient_disk',free_bytes=free);raise SystemExit(2)
env=dict(os.environ,PYTHONPATH=str(stage)+':'+str(runtime/'tools/BattleRl'),
         PATH='/root/autodl-tmp/dotnet:'+os.environ['PATH'])
python='/root/autodl-tmp/mkvenv/bin/python'
commands=[('gradient',[python,str(stage/'check_solo_state_batch_gradient.py'),
    '--repo',str(runtime),'--schema',str(runtime/'public_dto_schema.json'),
    '--assets',str(runtime/'text_assets'),'--archive',str(base/'rollout_0001/episode_000000/native.json'),
    '--weights',str(base/'checkpoint_0000.pt'),'--device','cuda','--out',str(stage/'cuda_gradient.json')]),
    ('ppo',[python,str(stage/'run_solo_state_v6_ppo_gate.py')])]
for label,command in commands:
    with (stage/(label+'.log')).open('x') as log:
        child=subprocess.Popen(command,env=env,stdout=log,stderr=subprocess.STDOUT)
        status('running',gate=label,child_pid=child.pid,command=command)
        code=child.wait()
    if code:
        status('failed',gate=label,returncode=code);raise SystemExit(code)
status('completed',formal_training_started=False)
