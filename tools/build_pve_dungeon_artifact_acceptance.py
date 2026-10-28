"""Replay the accepted dungeon route with a recorded, temporary seed-1 fixture.

No rule/outcome overrides: the actual reward die and shuffled artifact supply run.
Run --prepare before Unity import, then --restore after inspection (including failures).
"""
import json
import runpy
import sys
from pathlib import Path

fixture = Path('Assets/GameData/BattleDemo/HornBlack.asset')
backup = Path('tmp/dungeon_artifact_HornBlack.original')
if '--restore' in sys.argv:
    original = backup.read_bytes()
    assert fixture.read_bytes() == original.replace(b'effectRandomSeed: 6', b'effectRandomSeed: 1')
    fixture.write_bytes(original)
    print('Restored original fixture bytes')
    raise SystemExit
if '--prepare' in sys.argv:
    original = fixture.read_bytes()
    assert b'effectRandomSeed: 6' in original, 'Expected untouched existing seed-6 fixture'
    if backup.exists():
        assert backup.read_bytes() == original, 'Preserve previous fixture backup'
    else:
        backup.write_bytes(original)
    fixture.write_bytes(original.replace(b'effectRandomSeed: 6', b'effectRandomSeed: 1'))

route = runpy.run_path('tools/build_pve_loyalty_dungeon_acceptance.py')
steps = route['steps']
for step in steps:
    for assertion in step['after']:
        if assertion['key'] == 'dungeon.reward':
            assertion['expected'] = 'Artifact'
    if '原法术奖励供应' in step['label']:
        step['label'] = '实际神器奖励翻二选一'
    if '选择实际原法术' in step['label']:
        step['label'] = '选择翻出的第一件真实神器'
# Reuse accepted route; only new reward transition and downstream state need evidence.
start = next(i for i,s in enumerate(steps) if s['label'] == '真实地点战后结算')
for step in steps[:start]:
    step['after'] = []
    step['skipScreenshot'] = True
click = route['click']
click('Btn_JourneyEncounter_0','同玩家携所得亡君之盾进入下一战',{'journey.samePlayer':'True','wounds':1})
click('Inspector/Btn_Next','正式格挡阶段')
click('Hand/Btn_Card_items_020_0','实际所得亡君之盾原卡',capture=True)
click('Inspector/Btn_ArtifactNormal','原普通单次格挡')
click('Inspector/Btn_ArtifactPlay','所得神器实际格挡6',{'block':6},True)
click('Enemy_0/Btn_Target','选择实际兽人攻击')
click('Inspector/Btn_Resolve','实际消费所得神器格挡',{'block':0,'rejections':1},True)
click('Inspector/Btn_Next','进入承伤')
click('Inspector/Btn_Next','实际已格挡无新伤',{'wounds':1})
click('Inspector/Btn_Next','保留未击败同一兽人并结束第二战')
click('Inspector/Btn_EndTurn','正常回合末保留神器弃牌去向',{'journey.completedTurns':3,'journey.completedBattles':2,'wounds':1},True)
for step in steps[start:]:
    for assertion in step['after']:
        assertion['rule'] = '原地下城首胜六面骰，基本色神器翻二选一；正式回合末取得，未选归底；所得亡君之盾普通格挡6实际消费，伤牌及单位状态跨战斗保留'
if '--from' in sys.argv:
    for step in steps[:int(sys.argv[sys.argv.index('--from')+1])]:
        step['after'] = []
        step['skipScreenshot'] = True
path = Path('AutomationConfigs/pve_battle.json')
config = json.loads(path.read_text(encoding='utf-8'))
config['steps'] = steps
path.write_text(json.dumps(config,ensure_ascii=False,indent=2),encoding='utf-8')
print('Dungeon artifact:',len(steps),'steps; new evidence from',start)
