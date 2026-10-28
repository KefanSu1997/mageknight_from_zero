import json,sys,subprocess,hashlib,time,psutil,shutil
from pathlib import Path
root=Path('/root/autodl-tmp/mageknight');stage=root/'optimization_stage_20261014';report={}
for p in psutil.process_iter(['pid','cmdline']):
 if p.pid!=psutil.Process().pid and any(Path(x).name in ['run_candidate_formal.py','run_candidate_benchmark.py','run_final_eval_resume.py'] for x in (p.info['cmdline'] or [])[1:]):raise RuntimeError(f'Active conflicting trainer {p.pid}')
for arm in ['dot','interaction']:
 r=root/f'remote_candidate_{arm}_control_gate';assert json.loads((r/'complete.json').read_text())['updates']==2
 ev=[json.loads(x) for x in (r/'evaluation_metrics.jsonl').read_text().splitlines()];assert [x['update'] for x in ev]==[0,1,2]
 assert all(x['episodes']==4 and x['frozen_parameters'] and x['training_rng_restored'] for x in ev)
 m=[json.loads(x) for x in (r/'metrics.jsonl').read_text().splitlines()];assert m[-1]['episodes']==8
 report[arm]={'passed':True,'updates':2,'episodes':8,'eval_steps':[0,1,2],'eval_each':4}
assert shutil.disk_usage(root).free>10*2**30
report['sources']={n:hashlib.sha256((stage/n).read_bytes()).hexdigest() for n in ['run_candidate_formal.py','candidate_option_policy.py','candidate_option_runtime.py','candidate_green_baseline.json']}
(stage/'candidate_formal_deployment_gate.json').write_text(json.dumps(report,indent=2))
started=[]
for arm in ['dot','interaction']:
 out=f'remote_candidate_{arm}300';assert not (root/out).exists()
 log=(stage/f'candidate_{arm}300.log').open('w')
 p=subprocess.Popen([sys.executable,'-u',str(stage/'run_candidate_formal.py'),'--arm',arm,'--workers','16','--out',out],cwd=root,stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
 started.append({'arm':arm,'pid':p.pid,'created':psutil.Process(p.pid).create_time(),'out':out})
(stage/'candidate_formal_started.json').write_text(json.dumps({'time':time.time(),'runs':started},indent=2));print(json.dumps(started))
