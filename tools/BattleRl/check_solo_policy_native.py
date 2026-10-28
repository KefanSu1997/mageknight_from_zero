"""Short native inference integration; neither training nor full-scenario acceptance."""
import argparse
import json
from pathlib import Path
import time
import torch
from solo_native_env import SoloNativeEnv
from solo_public_schema import PublicSchema
from solo_text_assets import SoloTextAssets
from solo_policy_inputs import SoloPolicyInputs
from solo_structured_policy import SoloStructuredPolicy


def check(evidence):
    evidence=Path(evidence); root=Path(__file__).resolve().parents[2]
    out=evidence/'policy_native_inference'; out.mkdir(exist_ok=True)
    torch.set_num_threads(1); torch.manual_seed(7112026)
    schema=PublicSchema(evidence/'public_dto_schema.json')
    assets=SoloTextAssets(evidence/'solo_text_assets_v2',root)
    builder=SoloPolicyInputs(schema,assets)
    model=SoloStructuredPolicy(schema,assets.catalog.signature).eval()
    setup=dict(Version=1,Hero='Tovak',DummyHero='Goldyx',DummyCrystals=[0,1,2,0],DualColorRule=1,
               Seeds=dict(Tiles=1,Player=191,Enemies=192,Mana=193,Offers=194,RoundShuffle=195,
                          Progression=196,Dummy=197,Tactics=198,DummySkills=199,Ruins=200))
    dll=root/'tools/SoloConquestRl/bin/Debug/net8.0/SoloConquestRl.dll'
    rows=[]; memory=None; started=time.perf_counter()
    with SoloNativeEnv(dll,root,out/'source',preparation=True) as env:
        try:
            state=env.reset(setup)
            with torch.inference_mode():
                for step in range(8):
                    if state['observation']['Terminal']:break
                    tick=time.perf_counter(); prepared=builder.prepare(state)
                    prepare_seconds=time.perf_counter()-tick; tick=time.perf_counter()
                    logits,value,next_memory=model.step(prepared,memory,episode_start=step==0)
                    logp=torch.log_softmax(logits,0)
                    assert torch.isfinite(logp).all() and torch.isfinite(value)
                    index=int(torch.multinomial(logp.exp(),1))
                    inference_seconds=time.perf_counter()-tick
                    if step==4:
                        archive=env.export()
                        with SoloNativeEnv(dll,root,out/'restored',preparation=True) as other:
                            restored=other.restore(archive)
                            restored_logits,restored_value,_=model.step(builder.prepare(restored),memory)
                            torch.testing.assert_close(restored_logits,logits,rtol=0,atol=0)
                            torch.testing.assert_close(restored_value,value,rtol=0,atol=0)
                            other.step(index,log_probability=float(logp[index]),value=float(value))
                            restored_after=other.state
                    command=json.loads(state['commands'][index])
                    env.step(index,log_probability=float(logp[index]),value=float(value))
                    if step==4:
                        # Runtime measurement is not deterministic game state.
                        for key in ('observation','support_sha256','support_payload_json','commands'):
                            assert env.state[key]==restored_after[key],key
                    rows.append(dict(step=step,support_sha256=prepared['support_sha256'],
                                     candidates=len(logits),index=index,command=command,
                                     log_probability=float(logp[index]),value=float(value),
                                     prepare_seconds=prepare_seconds,inference_seconds=inference_seconds))
                    memory=next_memory;state=env.state
            saved=env.save(out/'native_journal.json')
            result=dict(passed=True,decisions=len(rows),steps=rows,archive=saved,
                        seconds=time.perf_counter()-started,training_started=False,
                        scope='Fresh random policy CPU inference on one native opening; no PPO, CUDA or conquest capability claim')
            (out/'result.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
            print(json.dumps({k:v for k,v in result.items() if k!='steps'}))
        except Exception as error:
            (out/'failure.json').write_text(json.dumps(dict(error=repr(error),completed=rows),indent=2),encoding='utf-8')
            if env.state is not None:env.save(out/'failure_native_journal.json')
            raise


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('evidence');check(parser.parse_args().evidence)
