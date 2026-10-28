import json,hashlib
from pathlib import Path
a=json.loads(Path('remote_final_eval_gate1/summary.json').read_text())
b=json.loads(Path('remote_final_eval_gate3/summary.json').read_text())
assert a['records']==b['records'] and a['parameter_sha']==b['parameter_sha']
result=dict(passed=True,tasks=4,actions=sum(r['steps'] for r in a['records']),one_three_worker_records_equal=True,frozen_parameter_sha=a['parameter_sha'],sources={n:hashlib.sha256(Path('optimization_stage_20261014',n).read_bytes()).hexdigest() for n in ['evaluate_final_checkpoint.py','final_periodic_eval.py','run_final_eval_resume.py']})
Path('optimization_stage_20261014/final_eval_gate.json').write_text(json.dumps(result,indent=2));print(json.dumps(result))
