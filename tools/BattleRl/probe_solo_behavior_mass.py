"""Frozen CPU replay of an archived prefix; no environment or parameter updates."""
import argparse,json
from collections import defaultdict
from pathlib import Path
import torch
from solo_public_schema import PublicSchema
from solo_text_assets import SoloTextAssets
from solo_policy_inputs import SoloPolicyInputs
from solo_forest_policy import SoloForestPolicy
from review_solo_exploration import branch
from solo_group_prior import count_neutral_logits

p=argparse.ArgumentParser()
for key in ('checkpoint','episode','schema','assets','repo','out'):p.add_argument('--'+key,required=True)
p.add_argument('--steps',type=int,default=12)
a=p.parse_args();torch.set_num_threads(1)
schema=PublicSchema(a.schema);assets=SoloTextAssets(a.assets,a.repo)
builder=SoloPolicyInputs(schema,assets);saved=torch.load(a.checkpoint,map_location='cpu',weights_only=False)
model=SoloForestPolicy(schema,assets.catalog.signature);model.load_state_dict(saved['network']);model.eval()
records=json.loads(Path(a.episode).read_text())['native']['records'];memory=None;rows=[]
with torch.inference_mode():
    for record in records[:a.steps]:
        logits,value,memory=model.step(builder.prepare_record(record),memory)
        logp=torch.log_softmax(logits,0);mass=defaultdict(float);count=defaultdict(int)
        for command,prob in zip(record['CommandsJson'],logp.exp().tolist()):
            key=branch(json.loads(command));mass[key]+=prob;count[key]+=1
        corrected_mass=defaultdict(float)
        for command,prob in zip(record['CommandsJson'],count_neutral_logits(logits,record['CommandsJson']).softmax(0).tolist()):
            corrected_mass[branch(json.loads(command))]+=prob
        error=abs(float(logp[record['SelectedIndex']])-record['BehaviorLogProbability'])
        if error>1e-4:raise ValueError('Frozen behavior replay differs: '+str(error))
        rows.append(dict(step=record['BeforeStep'],branch_mass=dict(mass),branch_count=dict(count),
                         hypothetical_count_neutral_mass=dict(corrected_mass),
                         selected_logprob_error=error,selected=branch(json.loads(record['CommandsJson'][record['SelectedIndex']]))))
report=dict(rows=rows,scope='Exact frozen initial policy CPU prefix replay, per-step branch mass, no updates')
Path(a.out).write_text(json.dumps(report,indent=2));print(json.dumps(report))
