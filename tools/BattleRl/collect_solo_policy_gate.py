"""One actual random-policy scenario, inference only, fixed controlled opening."""
import argparse
import json
from pathlib import Path
import tempfile
import torch
from solo_native_pool import SoloNativePool
from solo_public_schema import PublicSchema
from solo_text_assets import SoloTextAssets
from solo_policy_inputs import SoloPolicyInputs
from solo_structured_policy import SoloStructuredPolicy
from solo_episode_collector import SoloBehavior,collect_scenarios


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('evidence')
    parser.add_argument('--family',help='Actual exported family; run every hero without task replacement')
    parser.add_argument('--workers',type=int,default=1)
    parser.add_argument('--dll',help='Explicit isolated native build for a versioned preparation gate')
    parser.add_argument('--text-assets',help='Explicit version-bound frozen text assets')
    args=parser.parse_args();e=Path(args.evidence);root=Path(__file__).resolve().parents[2]
    out=Path(tempfile.mkdtemp(prefix='neural_scenario_gate_',dir=e))
    torch.set_num_threads(1);torch.manual_seed(7112026)
    schema=PublicSchema(e/'public_dto_schema.json');assets=SoloTextAssets(args.text_assets or e/'solo_text_assets_v2',root)
    model=SoloStructuredPolicy(schema,assets.catalog.signature)
    behavior=SoloBehavior(model,SoloPolicyInputs(schema,assets))
    setup=dict(Version=1,Hero='Tovak',DummyHero='Goldyx',DummyCrystals=[0,1,2,0],DualColorRule=1,
               Seeds=dict(Tiles=1,Player=191,Enemies=192,Mana=193,Offers=194,RoundShuffle=195,
                          Progression=196,Dummy=197,Tactics=198,DummySkills=199,Ruins=200))
    tasks=[dict(id='controlled-neural-full-scenario',setup=setup,actor_seed=7112026)]
    if args.family:
        source=json.loads(Path(args.family).read_text(encoding='utf-8'))
        tasks=[dict(t,id='actual-family/'+t['setup']['Hero']) for t in source['tasks']]
    print(json.dumps(dict(output=str(out),mode='CPU inference only; no local RL update')),flush=True)
    dll=Path(args.dll).resolve() if args.dll else root/'tools/SoloConquestRl/bin/Debug/net8.0/SoloConquestRl.dll'
    (out/'gate_source.json').write_text(json.dumps(dict(dll=str(dll),family=args.family,
        preparation=True,formal_training=False)),encoding='utf-8')
    with SoloNativePool(dll,root,
                        out/'worker',workers=args.workers,preparation=True) as pool:
        report=collect_scenarios(pool,behavior,tasks,out/'collection',max_decisions=512)
    print(json.dumps(dict(output=str(out),complete=report['complete'],
                         states=[x['status'] for x in report['episodes']],seconds=report['wall_seconds'])),flush=True)
