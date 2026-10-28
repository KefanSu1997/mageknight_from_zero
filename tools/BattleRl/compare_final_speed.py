import json,hashlib
from pathlib import Path
old=Path('remote_final_speed_old');new=Path('remote_final_speed_fast')
a=json.loads((old/'benchmark.json').read_text());b=json.loads((new/'benchmark.json').read_text())
assert a['parameter_sha']==b['parameter_sha']
assert a['arrays']==b['arrays'],(a['arrays'],b['arrays'])
assert a['steps']==b['steps']
paths=sorted((old/'episodes').glob('*episode*.json'));count=0
for path in paths:
    x=json.loads(path.read_text());y=json.loads((new/'episodes'/path.name).read_text())
    for doc in (x,y):
        for row in doc['trace']:row.pop('support_id',None)
    assert x==y,path.name
    count+=1
assert count==64
result=dict(passed=b['collection_seconds']<a['collection_seconds'],equal=True,episodes=64,steps=a['steps'],
            old_seconds=a['collection_seconds'],fast_seconds=b['collection_seconds'],speedup=a['collection_seconds']/b['collection_seconds'],
            parameters_unchanged=True,actions_rewards_values_logprobs_returns_exact=True,full_episode_documents_exact_except_local_support_ids=True,
            candidate_sha=hashlib.sha256(Path('optimization_stage_20261014/final_cpu_fast.py').read_bytes()).hexdigest())
Path('optimization_stage_20261014/final_sampling_speed_gate.json').write_text(json.dumps(result,indent=2));print(json.dumps(result))
