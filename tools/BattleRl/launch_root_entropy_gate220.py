"""Preserve completed220, stop only its identified job, run isolated32 gate."""
import hashlib, json, os, subprocess, time, zipfile
from pathlib import Path
import psutil

root = Path('remote_conditioned300')
stage = Path('optimization_stage_20261014')
out = Path('remote_root_entropy_gate32')
assert not out.exists(), 'Do not duplicate candidate gate'
summary = json.loads((root/'eval_0220/summary.json').read_text())
assert summary['episodes'] == 1000 and summary['wins'] == 100
assert summary['frozen_parameters'] and summary['training_rng_restored']
bundle = json.loads((root/'latest_checkpoint_bundle.json').read_text())
assert bundle['update'] == 220
assert hashlib.sha256((root/bundle['file']).read_bytes()).hexdigest() == bundle['sha256']
with zipfile.ZipFile(root/'checkpoint.zip') as z:
    assert json.loads(z.read('data'))['completed_updates'] == 220
parent = psutil.Process(122535)
assert abs(parent.create_time()-1791946710.97) < .02
assert parent.cmdline()[-1] == 'optimization_stage_20261014/deploy_conditioned_after_gate_v2.py'
identity = dict(pid=parent.pid, created=parent.create_time(), command=parent.cmdline())
rules = []
for child in parent.children(recursive=False):
    command = child.cmdline()
    if any('atomic_adapter_v2_frozen/BattleRl.dll' in arg.replace('\\','/') for arg in command):
        rules.append((child, child.create_time(), command))
evidence = dict(parent=identity, rules=[dict(pid=p.pid,created=c,command=a) for p,c,a in rules],
                fixed220_wins=100, fixed200_wins=99, bundle=bundle,
                reason='Value fitting improved but fixed policy performance essentially unchanged; isolated entropy estimator gate next.')
(stage/'root_entropy_stop220_identity.json').write_text(json.dumps(evidence,indent=2))
parent.terminate()
parent.wait(timeout=20)
for process, created, command in rules:
    try:
        if process.create_time()==created and process.cmdline()==command:
            process.terminate()
    except psutil.NoSuchProcess:
        pass
_, alive = psutil.wait_procs([p for p,_,_ in rules],timeout=20)
assert not [p for p in alive if p.status()!=psutil.STATUS_ZOMBIE], 'Rule workers still alive; do not start GPU gate'
with zipfile.ZipFile(root/'checkpoint.zip') as z:
    assert json.loads(z.read('data'))['completed_updates'] == 220
evidence['preserved_sha'] = {n: hashlib.sha256((root/n).read_bytes()).hexdigest()
                            for n in ['checkpoint.zip','continuation.pt','atomic_supports.jsonl','manifest.json']}
(stage/'conditioned_deployment_status.json').write_text(json.dumps(dict(state='stopped_after_complete220',completed=220,pid=122535,learning_objective_complete=False)))
env = dict(os.environ)
env['PATH']='/root/autodl-tmp/dotnet:/root/autodl-tmp/mkvenv/bin:'+env['PATH']
env['OPENBLAS_NUM_THREADS']='1'
with (stage/'root_entropy_cuda_gate.log').open('wb') as log:
    proc = subprocess.Popen(['/root/autodl-tmp/mkvenv/bin/python','-u',str(stage/'gate_root_entropy_cuda.py'),
                             '--parent',str(root),'--out',str(out),'--gate'],
                            env=env,stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
evidence.update(state='gate_running',gate_pid=proc.pid,gate_created=psutil.Process(proc.pid).create_time(),gate_command=psutil.Process(proc.pid).cmdline())
(stage/'root_entropy_gate_status.json').write_text(json.dumps(evidence,indent=2))
print(json.dumps({k:v for k,v in evidence.items() if k!='rules'}))
