"""CPU inference-only gate on native unit choices; no local RL optimization."""
import argparse
import copy
import hashlib
import io
import json
from pathlib import Path
import time
import torch
from solo_public_schema import PublicSchema
from solo_text_assets import SoloTextAssets
from solo_policy_inputs import SoloPolicyInputs
from solo_structured_policy import SoloStructuredPolicy


def check(evidence):
    torch.set_num_threads(1)
    torch.manual_seed(7112026)
    evidence=Path(evidence);root=Path(__file__).resolve().parents[2]
    schema=PublicSchema(evidence/'public_dto_schema.json')
    assets=SoloTextAssets(evidence/'solo_text_assets_v2',root)
    builder=SoloPolicyInputs(schema,assets)
    fixture=json.loads((evidence/'native_unit_binding_fixtures.json').read_text(encoding='utf-8'))
    observation=fixture['observation']
    keys=[p['name'] for p in schema.document['types'][schema.document['command']]['properties']]
    commands=[]
    for payload in fixture['commands']:
        command=dict.fromkeys(keys)
        command.update(SchemaVersion=1,ExpectedStep=observation['Step'],Unit=payload)
        commands.append(json.dumps(command,separators=(',',':')))
    # Assemble a public receipt for the captured native fixture, not a new rule run.
    signed=json.dumps(dict(version=1,step=observation['Step'],observation=json.dumps(observation),commands=commands),separators=(',',':'))
    state=dict(observation=observation,commands=commands,support_payload_json=signed,
               support_sha256=hashlib.sha256(signed.encode()).hexdigest())
    prepared=builder.prepare(state)
    model=SoloStructuredPolicy(schema,assets.catalog.signature).eval()
    start=time.perf_counter()
    with torch.inference_mode():
        logits,value,memory=model.step(prepared,episode_start=True)
        assert logits.shape==(len(commands),) and torch.isfinite(logits).all() and torch.isfinite(value)
        assert (torch.softmax(logits,0)>0).all()
        reversed_input=dict(prepared,candidates=list(reversed(prepared['candidates'])))
        reverse,reverse_value,_=model.step(reversed_input,episode_start=True)
        torch.testing.assert_close(reverse.flip(0),logits,rtol=0,atol=1e-7)
        torch.testing.assert_close(reverse_value,value,rtol=0,atol=0)
        reset,reset_value,_=model.step(prepared,memory,episode_start=True)
        torch.testing.assert_close(reset,logits,rtol=0,atol=0)
        torch.testing.assert_close(reset_value,value,rtol=0,atol=0)
        continued,_,_=model.step(prepared,memory)
        assert not torch.equal(continued,logits)
        changed=copy.deepcopy(prepared)
        node=next(i for i,row in enumerate(changed['state']['nodes'])
                  if row[0]==0 and row[1]>=0 and builder.tensors.fields[row[1]]=='Step')
        changed['state']['integers'][node]+=1000
        ignored,ignored_value,_=model.step(changed,episode_start=True)
        torch.testing.assert_close(ignored,logits,rtol=0,atol=0)
        torch.testing.assert_close(ignored_value,value,rtol=0,atol=0)
        stream=io.BytesIO();torch.save(model.state_dict(),stream);stream.seek(0)
        reload=SoloStructuredPolicy(schema,assets.catalog.signature).eval()
        reload.load_state_dict(torch.load(stream,weights_only=True))
        restored,restored_value,_=reload.step(prepared,episode_start=True)
        torch.testing.assert_close(restored,logits,rtol=0,atol=0)
        torch.testing.assert_close(restored_value,value,rtol=0,atol=0)
    result=dict(passed=True,candidates=len(commands),state_nodes=len(prepared['state']['nodes']),
                seconds=time.perf_counter()-start,parameters=sum(p.numel() for p in model.parameters()),
                candidate_permutation=True,transport_step_ignored=True,recurrent_reset=True,reload_exact=True,
                scope='CPU inference of fresh random A-family policy on controlled native unit fixture; no gradients/PPO/CUDA or capability claim',
                training_started=False)
    (evidence/'structured_policy_inference_gate.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps(result))


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('evidence');check(parser.parse_args().evidence)
