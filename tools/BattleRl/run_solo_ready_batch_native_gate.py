"""Compare actual independent task trajectories against archived behavior data."""
import argparse
import hashlib
import json
from pathlib import Path
import torch
from solo_native_pool import SoloNativePool
from solo_public_schema import PublicSchema
from solo_text_assets import SoloTextAssets
from solo_policy_inputs import SoloPolicyInputs
from solo_ready_batch_policy import SoloReadyBatchPolicy
from solo_ready_batch_behavior import SoloReadyBatchBehavior
from solo_forest_policy import SoloForestPolicy
from solo_episode_collector import SoloBehavior, collect_scenarios
from solo_episode_data import state_signature

p=argparse.ArgumentParser()
for k in ('repo','baseline','out'):p.add_argument('--'+k,required=True)
p.add_argument('--workers',type=int,required=True)
p.add_argument('--tasks',type=int,required=True)
p.add_argument('--serial',action='store_true',help='Original per-environment inference for paired timing')
a=p.parse_args();repo=Path(a.repo);base=Path(a.baseline);out=Path(a.out)
out.mkdir(exist_ok=False);torch.set_num_threads(1)
schema=PublicSchema(repo/'public_dto_schema.json');assets=SoloTextAssets(repo/'text_assets',repo)
builder=SoloPolicyInputs(schema,assets)
model=(SoloForestPolicy if a.serial else SoloReadyBatchPolicy)(schema,assets.catalog.signature).cuda().eval()
ckpt=base/'checkpoint_0000.pt'
model.load_state_dict(torch.load(ckpt,map_location='cuda',weights_only=False)['network'])
before=state_signature(model.state_dict());tasks=[];old=[];refs=[]
for i in range(a.tasks):
    directory=base/f'rollout_0001/episode_{i:06d}'
    result=json.loads((directory/'result.json').read_text())
    assert result['eligible_complete_episode']
    raw=(directory/'native.json').read_bytes()
    assert hashlib.sha256(raw).hexdigest()==result['archive']['sha256']
    tasks.append(json.loads((directory/'task.json').read_text()))
    old.append(json.loads(raw)['native'])
    refs.append(dict(path=str(directory),sha256=result['archive']['sha256']))
(out/'contract.json').write_text(json.dumps(dict(tasks=tasks,reference=refs,
    checkpoint_sha256=hashlib.sha256(ckpt.read_bytes()).hexdigest(),workers=a.workers),indent=2))
with SoloNativePool(repo/'native/SoloConquestRl.dll',repo,out/'workers',workers=a.workers,preparation=True) as pool:
    behavior=(SoloBehavior if a.serial else SoloReadyBatchBehavior)(model,builder)
    result=collect_scenarios(pool,behavior,tasks,out/'collection',ready_batch=not a.serial)
assert result['complete']==len(tasks), 'Incomplete scenarios: preserve failure evidence'
assert state_signature(model.state_dict())==before
excluded={'BehaviorLogProbability','ValuePrediction','SupportSeconds','ExecuteAndObserveSeconds'}
maximum=0.;batches={};decisions=0
for i,reference in enumerate(old):
    current=json.loads((out/f'collection/episode_{i:06d}/native.json').read_text())['native']
    assert {k:v for k,v in reference.items() if k!='records'}=={k:v for k,v in current.items() if k!='records'}
    assert len(reference['records'])==len(current['records'])
    for x,y in zip(reference['records'],current['records']):
        assert {k:v for k,v in x.items() if k not in excluded}=={k:v for k,v in y.items() if k not in excluded}
        for key in ('BehaviorLogProbability','ValuePrediction'):
            delta=abs(x[key]-y[key]);maximum=max(maximum,delta)
            assert delta<3e-5,(i,key,delta)
        decisions+=1
    oc=torch.load(base/f'rollout_0001/episode_{i:06d}/behavior_context.pt',map_location='cpu',weights_only=False)
    nc=torch.load(out/f'collection/episode_{i:06d}/behavior_context.pt',map_location='cpu',weights_only=False)
    assert torch.equal(oc['generator'],nc['generator'])
    for op,np in zip(oc['memory'],nc['memory']):
        for x,y in zip(op,np):torch.testing.assert_close(x,y,rtol=1e-5,atol=3e-6)
    for t in result['episodes'][i]['timings']:
        if t['operation']=='policy':
            size=t.get('inference_batch_size',1);batches[size]=batches.get(size,0)+1
report=dict(passed=True,workers=a.workers,tasks=len(tasks),decisions=decisions,
    max_behavior_numeric_difference=maximum,task_rng_equal=True,parameters_unchanged=True,
    wall_seconds=result['wall_seconds'],decision_counts_by_batch_size=batches,
    inference_mode='serial' if a.serial else 'ready_batch',
    scope='Actual complete archived-task comparison; compare timings only under documented matching workloads')
(out/'result.json').write_text(json.dumps(report,indent=2));print(json.dumps(report),flush=True)
