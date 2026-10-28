"""Scripted native path probes for seven real starts; not PPO or capability scores."""
import json
from pathlib import Path
import tempfile
import time
from solo_native_env import SoloNativeEnv
from solo_world_manifest import HEROES


def rank(command,state):
    # Public coordinates only. Deliberately seek exploration/encounters rather
    # than filtering worlds whose hidden next tile is inconvenient.
    spatial=command.get('Spatial')
    if spatial:
        kind=spatial['Kind'];forward=spatial['Q']-spatial['R']
        return ({2:0,3:0,1:1,0:4,4:4}[kind],-forward)
    if command.get('Clock') and command['Clock']['Kind'] in (0,1):return (-1,0)
    for key in ('TacticInput','Repeated','CityArtifactReward','LevelReward','AssaultSettlement','CombatFollowup'):
        if command.get(key):return (0,0)
    if command.get('Attack') or command.get('Block') or command.get('Damage'):return (1,0)
    if command.get('Adventure'):return (1,0)
    if command.get('Site') and command['Site']['Operation'] not in (3,4):return (2,0)
    if command.get('Deed'):return (5,0)
    if command.get('Basic'):
        return (3 if command['Basic']['Kind']==0 else 6,0)
    if command.get('EndTurn'):return (8,0)
    if command.get('Clock'):return (9,0)
    return (7,0)


def check():
    root=Path(__file__).resolve().parents[2]
    evidence=Path('H:/MageKnight_RL_Runs/20261021_solo_conquest_prepare')
    out=Path(tempfile.mkdtemp(prefix='seven_hero_world_paths_',dir=evidence));results=[]
    dll=root/'tools/SoloConquestRl/bin/Debug/net8.0/SoloConquestRl.dll'
    for hero in HEROES:
        directory=out/hero;directory.mkdir()
        setup=dict(Version=1,Hero=hero,DummyHero='Tovak' if hero!='Tovak' else 'Goldyx',
                   DummyCrystals=[0,1,2,0],DualColorRule=1,
                   Seeds=dict(Tiles=1,Player=191,Enemies=192,Mana=193,Offers=194,RoundShuffle=195,
                              Progression=196,Dummy=197,Tactics=198,DummySkills=199,Ruins=200))
        row=dict(hero=hero,steps=0,selected_branches=[],error=None,scope='controlled_original_start_scripted_probe')
        start=time.perf_counter()
        with SoloNativeEnv(dll,root,directory/'native',preparation=True) as env:
            try:
                state=env.reset(setup)
                for _ in range(32):
                    if state['observation']['Terminal']:break
                    commands=[json.loads(c) for c in state['commands']]
                    index=min(range(len(commands)),key=lambda i:rank(commands[i],state))
                    selected=commands[index]
                    row['attempted_command']=selected
                    result=env.step(index)
                    state=result['state'];row['steps']+=1
                    branch=next(k for k,v in selected.items() if v is not None and k not in ('SchemaVersion','ExpectedStep'))
                    row['selected_branches'].append(branch)
                row['terminal']=state['observation']['Terminal']
            except Exception as error:row['error']=repr(error)
            if env.state is not None:
                row['archive']=env.save(directory/'native.json')
        row['wall_seconds']=time.perf_counter()-start
        (directory/'result.json').write_text(json.dumps(row,indent=2),encoding='utf-8')
        results.append(row)
        (out/'progress.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
        print(json.dumps(dict(hero=hero,steps=row['steps'],error=row['error'],evidence=str(out))),flush=True)
    (out/'report.json').write_text(json.dumps(dict(results=results,formal_training=False,
        all_action_coverage_proven=False),indent=2),encoding='utf-8')


if __name__=='__main__':check()
