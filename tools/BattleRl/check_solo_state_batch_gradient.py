"""Actual archived sequence gradient equivalence, no optimizer or training."""
import argparse,json,time
from pathlib import Path
import torch
from solo_public_schema import PublicSchema
from solo_text_assets import SoloTextAssets
from solo_policy_inputs import SoloPolicyInputs
from solo_forest_policy import SoloForestPolicy
from solo_state_batch_policy import SoloStateBatchPolicy
p=argparse.ArgumentParser()
for key in ('repo','schema','assets','archive','weights','out'):p.add_argument('--'+key,required=True)
p.add_argument('--device',default='cpu')
a=p.parse_args();torch.set_num_threads(1)
schema=PublicSchema(a.schema);assets=SoloTextAssets(a.assets,a.repo);builder=SoloPolicyInputs(schema,assets)
records=json.loads(Path(a.archive).read_text())['native']['records'][:17]
prepared=[builder.prepare_record(r) for r in records];actions=[r['SelectedIndex'] for r in records]
state=torch.load(a.weights,map_location='cpu',weights_only=False);state=state.get('network',state)
outputs=[];gradients=[];times=[]
for cls in (SoloForestPolicy,SoloStateBatchPolicy):
    model=cls(schema,assets.catalog.signature).to(a.device);model.load_state_dict(state)
    start=time.perf_counter();v,l,e,m=model.evaluate_sequence(prepared,actions)
    (v.square().sum()+l.mean()+.01*e.mean()).backward()
    if a.device.startswith('cuda'):torch.cuda.synchronize()
    times.append(time.perf_counter()-start)
    outputs.append((v.detach().cpu(),l.detach().cpu(),e.detach().cpu()))
    gradients.append({n:None if p.grad is None else p.grad.detach().cpu().clone() for n,p in model.named_parameters()})
    del model
for x,y in zip(*outputs):torch.testing.assert_close(x,y,rtol=2e-5,atol=3e-6)
maximum=0.;count=0
for name,x in gradients[0].items():
    y=gradients[1][name]
    if x is None or y is None:
        assert x is None and y is None,name
        continue
    torch.testing.assert_close(x,y,rtol=3e-4,atol=3e-6,msg=name)
    maximum=max(maximum,float((x-y).abs().max()));count+=1
report=dict(passed=True,device=a.device,decisions=len(records),parameters_with_gradient=count,
    max_gradient_absolute_difference=maximum,forward_backward_seconds=times,optimizer_steps=0,
    scope='Actual 17-step prefix including three batching chunks; no complete PPO or throughput claim')
Path(a.out).write_text(json.dumps(report,indent=2));print(json.dumps(report))
