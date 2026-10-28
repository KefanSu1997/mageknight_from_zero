"""Seven-hero paired initial-policy collection gate; never starts formal training."""
import argparse,json,time,hashlib
from pathlib import Path
import torch
from solo_native_pool import SoloNativePool
from solo_public_schema import PublicSchema
from solo_text_assets import SoloTextAssets
from solo_policy_inputs import SoloPolicyInputs
from solo_group_prior_policy import SoloGroupPriorPolicy
from solo_episode_collector import SoloBehavior,collect_scenarios
from solo_episode_data import state_signature

p=argparse.ArgumentParser()
for key in ('repo','baseline','out'):p.add_argument('--'+key,required=True)
a=p.parse_args();repo=Path(a.repo);baseline=Path(a.baseline);out=Path(a.out)
out.mkdir(exist_ok=False);torch.set_num_threads(1)
schema=PublicSchema(repo/'public_dto_schema.json');assets=SoloTextAssets(repo/'text_assets',repo)
builder=SoloPolicyInputs(schema,assets);model=SoloGroupPriorPolicy(schema,assets.catalog.signature).cuda().eval()
checkpoint=baseline/'checkpoint_0000.pt';model.load_state_dict(torch.load(checkpoint,map_location='cuda',weights_only=False)['network'])
before=state_signature(model.state_dict());chosen={};references=[]
for taskfile in sorted((baseline/'rollout_0001').glob('episode_*/task.json')):
    task=json.loads(taskfile.read_text());hero=task['setup']['Hero']
    if hero in chosen:continue
    result=json.loads(taskfile.with_name('result.json').read_text())
    if not result.get('eligible_complete_episode'):raise ValueError('Baseline must be complete')
    raw=taskfile.with_name('native.json').read_bytes()
    if hashlib.sha256(raw).hexdigest()!=result['archive']['sha256']:raise ValueError('Baseline SHA differs')
    final=json.loads(raw)['native']['records'][-1]['Transition']
    chosen[hero]=task;references.append(dict(hero=hero,task_id=task['id'],source=str(taskfile.parent),
        sha256=result['archive']['sha256'],fame=final['Score']['Inventory']['Fame'],battles=final['CompletedBattles'],
        score=final['Score']['Total'],decisions=result['decisions']))
if len(chosen)!=7:raise ValueError('All seven actual heroes required')
(out/'paired_contract.json').write_text(json.dumps(dict(checkpoint_sha256=hashlib.sha256(checkpoint.read_bytes()).hexdigest(),
    baseline=references,tasks=list(chosen.values()),candidate_repo=str(repo),
    changed='Operation-family count prior; candidate runtime rules version must be read from native archives. Original reward, initial weights and task actor seeds unchanged. A changed rules binding makes this a joint integration comparison, not a prior-only causal comparison.'),indent=2))
start=time.time()
with SoloNativePool(repo/'native/SoloConquestRl.dll',repo,out/'workers',workers=3,preparation=True) as pool:
    collection=collect_scenarios(pool,SoloBehavior(model,builder),list(chosen.values()),out/'collection',max_decisions=512)
assert state_signature(model.state_dict())==before
rows=[]
for i,base in enumerate(references):
    result=collection['episodes'][i];row=dict(baseline=base,status=result['status'])
    if result.get('eligible_complete_episode'):
        native=json.loads((out/f'collection/episode_{i:06d}/native.json').read_text())['native']
        final=native['records'][-1]['Transition']
        row['candidate']=dict(fame=final['Score']['Inventory']['Fame'],battles=final['CompletedBattles'],
            score=final['Score']['Total'],decisions=result['decisions'],audit=result['audit'])
    rows.append(row)
report=dict(passed=collection['complete']==7,complete=collection['complete'],requested=7,
    elapsed_seconds=time.time()-start,parameters_unchanged=True,paired=rows,
    scope='Seven same training tasks/seeds and initial weights; exploratory evidence, no PPO, no validation/test, not statistical superiority')
(out/'result.json').write_text(json.dumps(report,indent=2));print(json.dumps(report),flush=True)
