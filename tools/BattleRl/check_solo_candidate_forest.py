"""Actual archived-support forward/gradient comparison; no optimizer step."""
import argparse,copy,json,time
from pathlib import Path
import torch
from solo_public_schema import PublicSchema
from solo_text_assets import SoloTextAssets
from solo_policy_inputs import SoloPolicyInputs
from solo_structured_policy import PublicTreeEncoder
from solo_candidate_forest import roots

p=argparse.ArgumentParser()
for key in ('repo','schema','assets','archive','out'):p.add_argument('--'+key,required=True)
p.add_argument('--device',default='cpu');a=p.parse_args()
torch.set_num_threads(1);torch.manual_seed(7112026)
schema=PublicSchema(a.schema);assets=SoloTextAssets(a.assets,a.repo);builder=SoloPolicyInputs(schema,assets)
records=json.loads(Path(a.archive).read_text())['native']['records']
selected=sorted(records,key=lambda r:len(r['CommandsJson']),reverse=True)[:3]
report=[]
for row in selected:
    prepared=builder.prepare_record(row);candidates=prepared['candidates']
    old=PublicTreeEncoder(schema).to(a.device);new=copy.deepcopy(old)
    if a.device=='cuda':torch.cuda.synchronize()
    start=time.perf_counter()
    expected=torch.stack([old(r['numeric'],r['node_mask'])[0] for r in candidates])
    weights=torch.linspace(-.7,.9,expected.numel(),device=a.device).reshape_as(expected)
    (expected*weights).sum().backward()
    if a.device=='cuda':torch.cuda.synchronize()
    old_seconds=time.perf_counter()-start;start=time.perf_counter()
    actual=roots(new,candidates);(actual*weights).sum().backward()
    if a.device=='cuda':torch.cuda.synchronize()
    new_seconds=time.perf_counter()-start
    torch.testing.assert_close(actual,expected,rtol=1e-5,atol=2e-6)
    max_gradient=0.
    for (name,left),(other,right) in zip(old.named_parameters(),new.named_parameters()):
        assert name==other and (left.grad is None)==(right.grad is None),name
        if left.grad is not None:
            torch.testing.assert_close(left.grad,right.grad,rtol=2e-4,atol=2e-5,msg=lambda msg:name+': '+msg)
            max_gradient=max(max_gradient,float((left.grad-right.grad).abs().max()))
    report.append(dict(step=row['BeforeStep'],candidates=len(candidates),output_max_difference=float((actual-expected).abs().max()),
        gradient_max_difference=max_gradient,old_forward_backward_seconds=old_seconds,new_forward_backward_seconds=new_seconds))
Path(a.out).write_text(json.dumps(dict(passed=True,device=a.device,tests=report,optimizer_steps=0,
    scope='Actual support tree encoder only; not complete policy/PPO throughput'),indent=2))
print(json.dumps(report))
