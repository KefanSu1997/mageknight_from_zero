import json,time,subprocess
from pathlib import Path
root=Path('/root/autodl-tmp/mageknight');stage=root/'optimization_stage_20261014'
def tail(path):
    try:return json.loads(path.read_text().splitlines()[-1])
    except (FileNotFoundError,IndexError,json.JSONDecodeError):return None
for _ in range(120):
    mem=dict(line.split() for line in Path('/sys/fs/cgroup/memory.stat').read_text().splitlines())
    record=dict(time=time.time(),memory_bytes=int(Path('/sys/fs/cgroup/memory.current').read_text()),anon_bytes=int(mem['anon']),file_bytes=int(mem['file']),memory_events=Path('/sys/fs/cgroup/memory.events').read_text(),cpu_stat=Path('/sys/fs/cgroup/cpu.stat').read_text(),gpu=subprocess.check_output(['nvidia-smi','--query-gpu=utilization.gpu,memory.used,memory.total','--format=csv,noheader'],text=True).strip(),training=tail(root/'remote_final_eval_resume300/metrics.jsonl'),evaluation={p.parent.name:json.loads(p.read_text()) for p in (root/'remote_final_backfill').glob('eval_*/progress.json')})
    with (stage/'parallel_resource_samples.jsonl').open('a') as f:f.write(json.dumps(record)+'\n')
    time.sleep(10)
