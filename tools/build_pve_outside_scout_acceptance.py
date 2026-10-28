"""Replay established world route, checking only the new owned-scout consumer."""
import json
import runpy
import sys
from pathlib import Path

route = runpy.run_path('tools/build_pve_world_spells_acceptance.py')
steps = route['steps']
for step in steps:
    step['after'] = []
    step['skipScreenshot'] = True

def step(path, label, expected=None, capture=False):
    return dict(buttonPath='Canvas/BattleTable/'+path, label=label,
                waitAfterSeconds=.3, skipScreenshot=not capture,
                after=[dict(key=k, expected=str(v), rule='原斥候私看同一守军、原部队耗竭、真实击杀额外名望及跨回合保留')
                       for k,v in {'ui.truncated':'', **(expected or {})}.items()])

at = next(i for i,s in enumerate(steps) if s['buttonPath'].endswith('/Btn_ChooseCards'))
steps[at:at] = [
    step('CampShade/Camp/Btn_ChooseUnits','合法指挥槽选择原斥候'),
    step('CampShade/UnitPicker/Btn_NextPage','原部队第二页'),
    step('CampShade/UnitPicker/Btn_Unit_army_2_009','一支原斥候'),
    step('CampShade/UnitPicker/Btn_Back','保留原构筑'),
]
at = next(i for i,s in enumerate(steps) if s['buttonPath'].endswith('/Btn_JourneyTools'))
steps[at:at] = [
    step('Btn_JourneyHand','回合开始实际手牌'),
    step('JourneyHand/Btn_OwnedUnits','战外部队原卡',{'units.count':1,'units.ready':'True','ui.outsideUnitArtCount':1}),
    step('OwnedUnits/Btn_Unit_0','选择原斥候'),
    step('OwnedUnits/Btn_Ability_1','原私看能力'),
    step('OwnedUnits/Btn_ScoutSite_-1_-1_0','三格以内实际法师塔守军'),
    step('OwnedUnits/Btn_Use','私看不公开、不抽走，真实斥候耗竭',{'scout.enemy':'monster_002','world.revealedSites':0,'units.ready':'False','ui.outsideUnitArtCount':1},True),
    step('OwnedUnits/Btn_Close','保留私看结果'),
    step('JourneyHand/Btn_CloseHand','返回同一旅程'),
]
for s in steps:
    if s['buttonPath'].endswith('/Inspector/Btn_Resolve'):
        s.update(step('Inspector/Btn_Resolve','同一被侦察守军真实击杀额外名望1',{'kills':1,'fame':7,'units.ready':'False'},True))
    elif s['buttonPath'].endswith('/Inspector/Btn_EndTurn'):
        s.update(step('Inspector/Btn_EndTurn','正式回合保留同一耗竭斥候与征服结果',{'journey.completedTurns':1,'journey.completedBattles':1,'units.ready':'False','world.conquered':'-1:-1','fame':7},True))
if '--from' in sys.argv:
    for s in steps[:int(sys.argv[sys.argv.index('--from')+1])]:
        s['after']=[]; s['skipScreenshot']=True
path=Path('AutomationConfigs/pve_battle.json')
config=json.loads(path.read_text(encoding='utf8'));config['steps']=steps
path.write_text(json.dumps(config,ensure_ascii=False,indent=2),encoding='utf8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'incremental checks')
