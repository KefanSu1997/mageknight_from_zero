import json,os,subprocess,sys,time
from pathlib import Path
stage=Path('/root/autodl-tmp/solo_forest_trial');root=Path('/root/autodl-tmp/solo_conquest_runtime_v2')
def status(**values):(stage/'full_queue_status.json').write_text(json.dumps(dict(pid=os.getpid(),time=time.time(),**values)))
def parent_live():
    try:return Path('/proc/717591/stat').read_text().split(') ',1)[1].split()[19]=='421622119'
    except FileNotFoundError:return False
while parent_live():status(state='waiting_tree_cuda_gate');time.sleep(20)
gate=stage/'cuda_result.json'
if not gate.exists() or not json.loads(gate.read_text()).get('passed'):
    status(state='tree_cuda_gate_failed');sys.exit(1)
env=dict(os.environ,PYTHONPATH=str(stage)+':'+str(root/'tools/BattleRl'))
commands=[['check_solo_forest_sequence.py','--repo',str(root),'--schema',str(root/'public_dto_schema.json'),
    '--assets',str(root/'text_assets'),'--archive',str(root/'cuda_runtime_gate/collection/episode_000000/native.json'),
    '--weights',str(root/'cuda_runtime_gate/collection/behavior_policy.pt'),'--out',str(stage/'full_sequence_cuda.json'),'--device','cuda'],
    ['run_solo_forest_ppo_gate.py']]
for command in commands:
    with (stage/(command[0]+'.log')).open('x') as log:
        p=subprocess.Popen([sys.executable,str(stage/command[0]),*command[1:]],env=env,stdout=log,stderr=subprocess.STDOUT)
        status(state='running',stage=command[0],child_pid=p.pid);code=p.wait()
    if code:status(state='failed',stage=command[0],returncode=code);sys.exit(code)
status(state='completed')
