"""Retry isolated gates after removing the queue.py stdlib-shadowing error."""
import json,os,subprocess,sys,time
from pathlib import Path
stage=Path('/root/autodl-tmp/solo_forest_trial');root=Path('/root/autodl-tmp/solo_conquest_runtime_v2')
def status(**kw):(stage/'retry_chain_status.json').write_text(json.dumps(dict(pid=os.getpid(),time=time.time(),**kw)))
assert json.loads((root/'cuda_runtime_gate/result.json').read_text())['passed']
assert not (stage/'queue.py').exists()
env=dict(os.environ,PYTHONPATH=str(stage)+':'+str(root/'tools/BattleRl'))
common=['--repo',str(root),'--schema',str(root/'public_dto_schema.json'),'--assets',str(root/'text_assets'),
    '--archive',str(root/'cuda_runtime_gate/collection/episode_000000/native.json'),'--device','cuda']
commands=[['check_solo_candidate_forest.py',*common,'--out',str(stage/'cuda_result.json')],
    ['check_solo_forest_sequence.py',*common,'--weights',str(root/'cuda_runtime_gate/collection/behavior_policy.pt'),'--out',str(stage/'full_sequence_cuda.json')],
    ['run_solo_forest_ppo_gate.py']]
for command in commands:
    with (stage/(command[0]+'.retry.log')).open('x') as log:
        p=subprocess.Popen([sys.executable,str(stage/command[0]),*command[1:]],env=env,stdout=log,stderr=subprocess.STDOUT)
        status(state='running',stage=command[0],child_pid=p.pid);code=p.wait()
    if code:status(state='failed',stage=command[0],returncode=code);sys.exit(code)
status(state='completed')
