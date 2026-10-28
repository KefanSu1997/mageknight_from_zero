"""CPU inference/gradient contract only, no optimizer or environment training."""
import argparse,io,json
from pathlib import Path
import torch
from solo_public_schema import PublicSchema
from solo_text_assets import SoloTextAssets
from solo_policy_inputs import SoloPolicyInputs
from solo_group_prior_policy import SoloGroupPriorPolicy

p=argparse.ArgumentParser()
for key in ('checkpoint','episode','schema','assets','repo','out'):p.add_argument('--'+key,required=True)
a=p.parse_args();torch.set_num_threads(1)
schema=PublicSchema(a.schema);assets=SoloTextAssets(a.assets,a.repo);builder=SoloPolicyInputs(schema,assets)
model=SoloGroupPriorPolicy(schema,assets.catalog.signature).eval()
saved=torch.load(a.checkpoint,map_location='cpu',weights_only=False);model.load_state_dict(saved['network'])
records=json.loads(Path(a.episode).read_text())['native']['records'][:12]
actions=[r['SelectedIndex'] for r in records]
memory=None;expected=[];values=[]
with torch.inference_mode():
    for record,action in zip(records,actions):
        logits,value,memory=model.step(builder.prepare_record(record),memory)
        expected.append(logits.log_softmax(0)[action].clone());values.append(value.clone())
actual,logs,entropies,_=model.evaluate_sequence((builder.prepare_record(r) for r in records),actions)
torch.testing.assert_close(logs,torch.stack(expected));torch.testing.assert_close(actual,torch.stack(values))
loss=-logs.mean()+actual.square().mean()-.01*entropies.mean();loss.backward()
grads=[v.grad for v in model.parameters() if v.grad is not None]
assert grads and all(torch.isfinite(g).all() for g in grads)
stream=io.BytesIO();torch.save(model.state_dict(),stream);stream.seek(0)
restored=SoloGroupPriorPolicy(schema,assets.catalog.signature).eval();restored.load_state_dict(torch.load(stream,weights_only=True))
with torch.inference_mode():
    ov,ol,oe,_=model.evaluate_sequence((builder.prepare_record(r) for r in records),actions)
    rv,rl,re,_=restored.evaluate_sequence((builder.prepare_record(r) for r in records),actions)
torch.testing.assert_close(rl,ol,rtol=0,atol=0)
torch.testing.assert_close(rv,ov,rtol=0,atol=0)
torch.testing.assert_close(re,oe,rtol=0,atol=0)
for key,value in model.state_dict().items():
    if torch.is_tensor(value):torch.testing.assert_close(value,restored.state_dict()[key],rtol=0,atol=0)
    else:assert value==restored.state_dict()[key]
report=dict(passed=True,steps=len(records),sample_train_logprob_max=float((logs.detach()-torch.stack(expected)).abs().max()),
            finite_gradient_tensors=len(grads),reload_logprob_exact=True,
            inference_vs_gradient_logprob_max=float((ol-logs.detach()).abs().max()),
            scope='CPU prefix inference and gradient integration; no optimizer, no full scenario, no CUDA acceptance')
Path(a.out).write_text(json.dumps(report,indent=2));print(json.dumps(report))
