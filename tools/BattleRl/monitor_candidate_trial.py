import json,time,psutil,subprocess
from pathlib import Path
s=Path('/root/autodl-tmp/mageknight/optimization_stage_20261014');ids=[258587,258588];out=s/'candidate_parallel16_resources.jsonl'
for _ in range(180):
 alive=[]
 for pid in ids:
  try:
   p=psutil.Process(pid)
   if 'run_candidate_benchmark.py' in ' '.join(p.cmdline()) and p.status()!=psutil.STATUS_ZOMBIE:alive.append(pid)
  except psutil.NoSuchProcess:pass
 row={'time':time.time(),'alive':alive,'memory_current':Path('/sys/fs/cgroup/memory.current').read_text(),'memory_events':Path('/sys/fs/cgroup/memory.events').read_text(),'memory_stat':Path('/sys/fs/cgroup/memory.stat').read_text(),'gpu':subprocess.check_output(['nvidia-smi','--query-gpu=memory.used,utilization.gpu','--format=csv,noheader,nounits'],text=True).strip()}
 with out.open('a') as f:f.write(json.dumps(row)+'\n')
 if not alive:break
 time.sleep(5)
