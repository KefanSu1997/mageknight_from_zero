import subprocess,sys,json,time
from pathlib import Path
root=Path('/root/autodl-tmp/mageknight');stage=root/'optimization_stage_20261014'
children=[]
for arm in ['dot','interaction']:
 log=(stage/f'candidate_bench16_{arm}.log').open('w')
 p=subprocess.Popen([sys.executable,'-u',str(stage/'run_candidate_benchmark.py'),'--arm',arm,'--workers','16','--out',f'remote_candidate_{arm}_bench16'],cwd=root,stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
 children.append({'pid':p.pid,'arm':arm})
(stage/'candidate_parallel16_started.json').write_text(json.dumps({'children':children,'time':time.time()}));print(json.dumps(children))
