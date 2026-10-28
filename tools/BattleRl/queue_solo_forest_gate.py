"""Run one isolated CUDA comparison after the identified integration process exits."""
import json,os,subprocess,sys,time
from pathlib import Path
stage=Path('/root/autodl-tmp/solo_forest_trial');root=Path('/root/autodl-tmp/solo_conquest_runtime_v2')
status=stage/'queue_status.json'
def write(**value):status.write_text(json.dumps(dict(pid=os.getpid(),time=time.time(),**value)))
def same_process():
    try:return Path('/proc/708751/stat').read_text().split(') ',1)[1].split()[19]=='421562063'
    except FileNotFoundError:return False
while same_process():
    write(state='waiting_identified_cuda_gate',parent_pid=708751);time.sleep(20)
result=root/'cuda_runtime_gate/result.json'
if not result.exists() or not json.loads(result.read_text()).get('passed'):
    write(state='parent_gate_failed_or_incomplete');sys.exit(1)
env=dict(os.environ,PYTHONPATH=str(root/'tools/BattleRl'))
command=[sys.executable,str(stage/'check_solo_candidate_forest.py'),'--repo',str(root),
    '--schema',str(root/'public_dto_schema.json'),'--assets',str(root/'text_assets'),
    '--archive',str(root/'cuda_runtime_gate/collection/episode_000000/native.json'),
    '--out',str(stage/'cuda_result.json'),'--device','cuda']
write(state='running_cuda_comparison')
with (stage/'cuda.log').open('x') as log:
    process=subprocess.Popen(command,env=env,stdout=log,stderr=subprocess.STDOUT)
    write(state='running_cuda_comparison',child_pid=process.pid)
    code=process.wait()
write(state='completed' if code==0 else 'failed',returncode=code)
