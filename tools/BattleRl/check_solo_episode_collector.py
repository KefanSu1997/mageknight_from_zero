"""Native collector gate: scripted clock completion and neural partials, no RL."""
import argparse
import copy
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


class ClockFixture:
    def begin(self,seed):return {}
    def snapshot(self,context):return dict(scripted=True)
    def save_policy(self,path):return dict(scripted_clock_fixture=True,policy_checkpoint=None)
    def select(self,state,context):
        commands=[json.loads(c) for c in state['commands']]
        def priority(i):
            c=commands[i]
            rank=next((n for n,name in enumerate(('Clock','TacticInput','EndTurn','Rest'))
                       if c.get(name) is not None),100)
            return rank,-len((c.get('EndTurn') or {}).get('DiscardHandSlots',[]))
        index=min(range(len(commands)),key=priority)
        assert priority(index)[0]<100
        return dict(index=index,log_probability=0.,value=0.,scripted=True)


def check(evidence):
    root=Path(__file__).resolve().parents[2];evidence=Path(evidence)
    out=Path(tempfile.mkdtemp(prefix='collector_gate_',dir=evidence))
    setup=dict(Version=1,Hero='Tovak',DummyHero='Goldyx',DummyCrystals=[0,1,2,0],DualColorRule=1,
               Seeds=dict(Tiles=1,Player=191,Enemies=192,Mana=193,Offers=194,RoundShuffle=195,
                          Progression=196,Dummy=197,Tactics=198,DummySkills=199,Ruins=200))
    tasks=[dict(id=f'controlled-fixture-{i}',setup=copy.deepcopy(setup),actor_seed=7112026+i) for i in range(2)]
    tasks[1]['setup']['Seeds']['Player']=2191
    dll=root/'tools/SoloConquestRl/bin/Debug/net8.0/SoloConquestRl.dll'
    with SoloNativePool(dll,root,out/'clock_workers',workers=2,preparation=True) as pool:
        clock=collect_scenarios(pool,ClockFixture(),tasks,out/'clock',max_decisions=400)
    assert clock['complete']==2 and all(x['audit']['passed'] for x in clock['episodes'])
    torch.set_num_threads(1);torch.manual_seed(7112026)
    schema=PublicSchema(evidence/'public_dto_schema.json')
    assets=SoloTextAssets(evidence/'solo_text_assets_v2',root)
    behavior=SoloBehavior(SoloStructuredPolicy(schema,assets.catalog.signature),SoloPolicyInputs(schema,assets))
    outcomes=[]
    for workers in (1,2):
        with SoloNativePool(dll,root,out/f'neural_workers_{workers}',workers=workers,preparation=True) as pool:
            result=collect_scenarios(pool,behavior,tasks,out/f'neural_{workers}',max_decisions=3)
        assert result['complete']==0
        assert all(x['status']=='engineering_partial' and x['decisions']==3 for x in result['episodes']),result
        outcomes.append(result)
    for i in range(2):
        archives=[]
        for result in outcomes:
            archives.append(json.loads(Path(result['episodes'][i]['archive']['path']).read_text())['native'])
        # Keep timings in the archives; compare deterministic semantics only.
        for archive in archives:
            for row in archive['records']:
                for key in ('SupportSeconds','ExecuteAndObserveSeconds'):
                    assert row[key]>=0
                    del row[key]
        assert archives[0]==archives[1],f'Task RNG/schedule affected native trajectory {i}'
    report=dict(passed=True,evidence=str(out),scripted_complete_episodes=2,
                neural_partial_episodes_per_schedule=2,neural_steps_per_episode=3,
                one_two_worker_native_journals_equal=True,partial_excluded=True,
                training_started=False,scope='Collector integration; scripted rest clock termination is not policy conquest')
    (out/'gate.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print(json.dumps(report))


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('evidence');check(parser.parse_args().evidence)
