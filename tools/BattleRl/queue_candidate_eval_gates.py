import time,json,subprocess,sys,psutil
from pathlib import Path
root=Path('/root/autodl-tmp/mageknight');stage=root/'optimization_stage_20261014'
while any(psutil.pid_exists(pid) and psutil.Process(pid).status()!=psutil.STATUS_ZOMBIE for pid in [258587,258588]):time.sleep(5)
for arm in ['dot','interaction']:
 assert (root/f'remote_candidate_{arm}_bench16/complete.json').exists(), 'Benchmark did not complete'
 for workers in [1,3]:
  out=f'remote_candidate_{arm}_eval_gate{workers}'
  with (stage/f'{out}.log').open('w') as log:
   p=subprocess.run([sys.executable,str(stage/'evaluate_candidate_checkpoint.py'),'--arm',arm,'--checkpoint',str(root/f'remote_candidate_{arm}_gate/checkpoint.zip'),'--update','2','--limit','4','--workers',str(workers),'--out',out],cwd=root,stdout=log,stderr=subprocess.STDOUT)
  assert p.returncode==0,(arm,workers,p.returncode)
 a=json.loads((root/f'remote_candidate_{arm}_eval_gate1/summary.json').read_text());b=json.loads((root/f'remote_candidate_{arm}_eval_gate3/summary.json').read_text())
 assert a['records']==b['records'] and a['parameter_sha']==b['parameter_sha']
 (root/f'remote_candidate_{arm}_gate/eval_gate.json').write_text(json.dumps({'passed':True,'tasks':4,'workers':[1,3],'records_equal':True,'parameter_sha':a['parameter_sha']}))
print('both eval gates passed')
