import json,psutil,subprocess,sys,time
from pathlib import Path
root=Path('/root/autodl-tmp/mageknight');stage=root/'optimization_stage_20261014'
q=psutil.Process(239706);e=psutil.Process(240905);t=psutil.Process(239705)
assert 'final_parallel_eval_worker.py' in ' '.join(q.cmdline())
assert e.ppid()==q.pid and '--update 8' in ' '.join(e.cmdline()) and 'evaluate_final_checkpoint.py' in ' '.join(e.cmdline())
assert 'run_final_eval_resume.py' in ' '.join(t.cmdline())
children=e.children();assert all('dotnet' in ' '.join(p.cmdline()) and 'BattleRl.dll' in ' '.join(p.cmdline()) for p in children)
report={'reason':'User standard evaluation every20 only','stopped':[{'pid':p.pid,'cmd':p.cmdline(),'created':p.create_time()} for p in [q,e]+children],'trainer':t.pid,'time':time.time()}
q.terminate();q.wait(10)
e.terminate()
for p in children:
    try:p.terminate()
    except psutil.NoSuchProcess:pass
_,alive=psutil.wait_procs([e]+children,timeout=10)
for p in alive:p.kill()
(stage/'standard20_switch.json').write_text(json.dumps(report,indent=2))
log=(stage/'parallel_eval_standard20.log').open('a')
p=subprocess.Popen([sys.executable,'-u',str(stage/'final_parallel_eval_worker.py')],cwd=root,stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
print(json.dumps({'queue_pid':p.pid,'trainer_alive':t.is_running(),'stopped_eval':e.pid,'children':len(children)}))
