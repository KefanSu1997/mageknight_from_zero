import json
from pathlib import Path
r=Path('/root/autodl-tmp/mageknight');s=r/'optimization_stage_20261014'
rows=[json.loads(x) for x in (s/'candidate_parallel16_resources.jsonl').read_text().splitlines()]
def entries(t):return {line.split()[0]:int(line.split()[1]) for line in t.splitlines()}
report={'samples':len(rows),'peak_memory_GiB':max(int(x['memory_current']) for x in rows)/2**30,'peak_anon_GiB':max(entries(x['memory_stat'])['anon'] for x in rows)/2**30,'event_delta':{k:entries(rows[-1]['memory_events'])[k]-entries(rows[0]['memory_events'])[k] for k in entries(rows[0]['memory_events'])},'peak_gpu_MiB':max(int(x['gpu'].split(',')[0]) for x in rows),'arms':{}}
for arm in ['dot','interaction']:
 p=r/f'remote_candidate_{arm}_bench16';report['arms'][arm]={'complete':(p/'complete.json').exists()}
 if (p/'metrics.jsonl').exists():report['arms'][arm]['metrics']=json.loads((p/'metrics.jsonl').read_text().splitlines()[-1])
(s/'candidate_parallel16_report.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
