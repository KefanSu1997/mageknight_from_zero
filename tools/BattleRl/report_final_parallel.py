import json
from pathlib import Path
root=Path('/root/autodl-tmp/mageknight');stage=root/'optimization_stage_20261014'
rows=[json.loads(line) for line in (stage/'parallel_resource_samples.jsonl').read_text().splitlines()]
metrics=[json.loads(line) for line in (root/'remote_final_eval_resume300/metrics.jsonl').read_text().splitlines()]
report=dict(samples=len(rows),span_seconds=rows[-1]['time']-rows[0]['time'],max_memory_gib=max(r['memory_bytes'] for r in rows)/1024**3,max_anon_gib=max(r['anon_bytes'] for r in rows)/1024**3,max_gpu_mib=max(int(r['gpu'].split(',')[1].strip().split()[0]) for r in rows),memory_events_start=rows[0]['memory_events'],memory_events_last=rows[-1]['memory_events'],new_updates=[{k:r[k] for k in ('update','timesteps','collection_seconds','optimization_seconds')} for r in metrics if r['update']>89],evaluation_start=rows[0]['evaluation'],evaluation_last=rows[-1]['evaluation'])
(stage/'parallel_trial_report.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
