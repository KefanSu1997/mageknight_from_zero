"""Actual native opening export/hidden-input isolation gate, not world admission."""
import copy
import json
import subprocess
from pathlib import Path


def check():
    root=Path(__file__).resolve().parents[2]
    dll=root/'tools/SoloConquestRl/bin/Debug/net8.0/SoloConquestRl.dll'
    setup=dict(Version=1,Hero='Tovak',DummyHero='Goldyx',DummyCrystals=[0,1,2,0],DualColorRule=1,
               Seeds=dict(Tiles=1,Player=191,Enemies=192,Mana=193,Offers=194,RoundShuffle=195,
                          Progression=196,Dummy=197,Tactics=198,DummySkills=199,Ruins=200))
    exports=[]
    for player_seed in (191,191,192):
        current=copy.deepcopy(setup);current['Seeds']['Player']=player_seed
        p=subprocess.Popen(['dotnet',str(dll),str(root)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,
                           stderr=subprocess.PIPE,text=True,encoding='utf-8')
        def request(op,**kwargs):
            p.stdin.write(json.dumps(dict(op=op,**kwargs))+'\n');p.stdin.flush()
            line=p.stdout.readline()
            if not line:raise RuntimeError(p.stderr.read())
            return json.loads(line)
        try:
            state=request('reset',setup=current);assert state['ok'],state
            before=request('export');assert before['ok'],before
            world=request('private_initial_world');assert world['ok'],world
            exports.append(world['result'])
            assert request('export')==before,'Export mutated private state'
            observed=request('observe');assert observed==state,'Export modified/exposed public state'
            assert 'private_metadata' not in observed['result']['observation']
            result=request('step',support_sha256=state['result']['support_sha256'],index=0)
            assert result['ok'],result
            assert not request('private_initial_world')['ok'],'Mid-episode world export accepted'
        finally:
            p.stdin.close();p.wait(timeout=30)
    assert exports[0]==exports[1],'Cross-process world export differs'
    assert exports[0]==exports[2],'Player shuffle leaked into world identity'
    report=dict(passed=True,scope='actual_controlled_opening_export_only',cross_process_equal=True,
                independent_of_player_shuffle=True,public_state_unchanged=True,
                complete_ruin_inventory=exports[0]['complete_ruin_inventory'],formal_world_manifest_created=False)
    out=Path('H:/MageKnight_RL_Runs/20261021_solo_conquest_prepare')
    (out/'private_initial_world_gate.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    (out/'controlled_private_initial_world.json').write_text(json.dumps(exports[0],indent=2),encoding='utf-8')
    print(json.dumps(report))


if __name__=='__main__':check()
