"""Full real scenario likelihood, memory and gradient comparison, no optimization."""
import argparse,json,time,gc
from pathlib import Path
import torch
from solo_public_schema import PublicSchema
from solo_text_assets import SoloTextAssets
from solo_policy_inputs import SoloPolicyInputs
from solo_vectorized_text_policy import SoloVectorizedTextPolicy
from solo_forest_policy import SoloForestPolicy

p=argparse.ArgumentParser()
for key in ('repo','schema','assets','archive','weights','out'):p.add_argument('--'+key,required=True)
p.add_argument('--device',default='cpu');a=p.parse_args()
torch.set_num_threads(1)
schema=PublicSchema(a.schema);assets=SoloTextAssets(a.assets,a.repo);builder=SoloPolicyInputs(schema,assets)
archive=json.loads(Path(a.archive).read_text())['native'];assert archive['terminal']
records=archive['records'];actions=[r['SelectedIndex'] for r in records]
state=torch.load(a.weights,map_location='cpu',weights_only=True)
state=state.get('network',state)
outputs=[];gradients=[];times=[]
for cls in (SoloForestPolicy,SoloVectorizedTextPolicy):
    model=cls(schema,assets.catalog.signature).to(a.device);model.load_state_dict(state)
    start=time.perf_counter()
    v,l,e,m=model.evaluate_sequence((builder.prepare_record(r) for r in records),actions)
    (v.square().mean()+l.mean()*.1-e.mean()*.01).backward()
    if a.device=='cuda':torch.cuda.synchronize()
    times.append(time.perf_counter()-start)
    outputs.append([x.detach().cpu().clone() for x in (v,l,e,*m[0],*m[1])])
    gradients.append({k:None if p.grad is None else p.grad.detach().cpu().clone() for k,p in model.named_parameters()})
    del model,v,l,e,m;gc.collect()
    if a.device=='cuda':torch.cuda.empty_cache()
for x,y in zip(*outputs):torch.testing.assert_close(x,y,rtol=1e-5,atol=3e-6)
maximum=0.
for name,x in gradients[0].items():
    y=gradients[1][name];assert (x is None)==(y is None),name
    if x is not None:
        torch.testing.assert_close(x,y,rtol=2e-4,atol=2e-5,msg=lambda msg:name+': '+msg)
        maximum=max(maximum,float((x-y).abs().max()))
report=dict(passed=True,complete_scenario=True,decisions=len(records),device=a.device,
    gradient_max_difference=maximum,output_max_difference=max(float((x-y).abs().max()) for x,y in zip(*outputs)),
    old_forward_backward_seconds=times[0],new_forward_backward_seconds=times[1],optimizer_steps=0,
    scope='Full archived recurrent scenario output/gradient; not full PPO or new rollout timing')
Path(a.out).write_text(json.dumps(report,indent=2));print(json.dumps(report))


