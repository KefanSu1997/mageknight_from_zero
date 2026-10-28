"""Serialize the exploration gate behind the existing GPU equivalence test."""
import json
import os
from pathlib import Path
import subprocess
import sys
import time

stage = Path(__file__).resolve().parent
root = Path('/root/autodl-tmp/solo_conquest_score_v5')
out = Path('/root/solo_group_prior_score_v5_gate_20261022')
status_path = stage / 'queue_status.json'
(stage / 'queue.lock').open('x').write(str(os.getpid()))

def status(**values):
    status_path.write_text(json.dumps(dict(pid=os.getpid(), time=time.time(), **values)))

proc = Path('/proc/866622')
try:
    assert b'run_solo_sequence_v5_ppo_gate.py' in (proc / 'cmdline').read_bytes()
    identity = (proc / 'stat').read_text().split(') ', 1)[1].split()[19]
except FileNotFoundError:
    identity = None
while identity is not None:
    try:
        if (proc / 'stat').read_text().split(') ', 1)[1].split()[19] != identity:
            break
    except FileNotFoundError:
        break
    status(state='waiting_sequence_ppo_gate', parent_pid=866622)
    time.sleep(20)
gate = Path('/root/autodl-tmp/solo_vectorized_v5_gate/sequence_ppo_gate/result.json')
if not gate.exists() or not json.loads(gate.read_text()).get('passed'):
    status(state='predecessor_failed_requires_review')
    sys.exit(1)
assert not out.exists()
env = dict(os.environ, PYTHONPATH=str(stage)+':'+str(root/'tools/BattleRl'),
           PATH='/root/autodl-tmp/dotnet:'+os.environ['PATH'])
with (stage/'collection.log').open('x') as log:
    child = subprocess.Popen([sys.executable, '-u', str(stage/'run_solo_group_collection_gate.py'),
        '--repo', str(root), '--baseline', '/root/mageknight_solo_conquest_A_score_v5_100_20261022',
        '--out', str(out)], env=env, stdout=log, stderr=subprocess.STDOUT)
    status(state='collecting', child_pid=child.pid, output=str(out))
    code = child.wait()
status(state='completed' if code==0 else 'failed', returncode=code, output=str(out),
       formal_training_changed=False)
