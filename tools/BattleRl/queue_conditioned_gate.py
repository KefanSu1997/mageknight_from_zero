"""One diagnostic gate after the verified pilot exits; never starts formal training."""
import os,sys,json,time,hashlib,subprocess
from pathlib import Path
import psutil
stage=Path('optimization_stage_20261014');parent=Path('remote_context_pilot200');out=Path('remote_conditioned_gate32')
lock=os.open(stage/'conditioned_gate_queue.lock',os.O_CREAT|os.O_EXCL|os.O_WRONLY)
os.write(lock,str(os.getpid()).encode());os.close(lock)
status=stage/'conditioned_gate_queue_status.json'
def write(**kw):status.write_text(json.dumps(dict(queue_pid=os.getpid(),**kw),indent=2))
names=['conditioned_context_policy.py','resume_conditioned_context.py','context_state_policy.py','entropy_token_value_policy.py','batched_token_value_policy.py']
binding={n:hashlib.sha256((stage/n).read_bytes()).hexdigest() for n in names}
try:
 p=psutil.Process(113236)
 assert 'optimization_stage_20261014/deploy_context_pilot.py' in p.cmdline()
 identity=dict(pid=p.pid,created=p.create_time(),command=p.cmdline())
 write(state='waiting_verified_parent',parent=identity,source_sha=binding)
 while p.is_running() and p.create_time()==identity['created'] and p.status()!=psutil.STATUS_ZOMBIE:
  time.sleep(30)
 complete=json.loads((parent/'complete.json').read_text());assert complete['updates']==200
 evaluation=json.loads((parent/'eval_0200/summary.json').read_text())
 assert evaluation['episodes']==1000 and len(evaluation['records'])==1000
 assert evaluation['frozen_parameters'] and evaluation['training_rng_restored']
 assert not out.exists()
 for n,v in binding.items():assert hashlib.sha256((stage/n).read_bytes()).hexdigest()==v,n
 os.environ['PATH']='/root/autodl-tmp/dotnet:/root/autodl-tmp/mkvenv/bin:'+os.environ['PATH']
 command=[sys.executable,'-u',str(stage/'resume_conditioned_context.py'),'--parent',str(parent),'--out',str(out),'--gate']
 child=subprocess.Popen(command)
 write(state='gate_running',gate_pid=child.pid,parent_eval_wins=evaluation['wins'],source_sha=binding)
 code=child.wait();assert code==0,code
 assert json.loads((out/'gate.json').read_text())['passed']
 write(state='gate_passed_not_deployed',parent_eval_wins=evaluation['wins'],gate=str(out),source_sha=binding)
except BaseException as error:
 write(state='failed',error=repr(error),source_sha=binding);raise
