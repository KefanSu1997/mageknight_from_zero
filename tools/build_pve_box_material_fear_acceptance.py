"""Physical box, actual sword material, then next-round fear multi-target burn.

Original shuffle seed 907: items_008, items_001, items_003 (44 advanced,
24 spells, 24 artifacts; only the physical box is owned). Five real cards,
formal round recovery and two complete battles. Never executes Unity.
"""
import json
import sys
from pathlib import Path

# Reuse only the existing legal five-card setup and exact-byte seed guard.
# The prefix contains no configuration write or battle execution.
base = Path('tools/build_pve_box_weapon_acceptance.py')
source = base.read_text(encoding='utf8').split("click('Btn_JourneyEncounter_2'", 1)[0]
source = source.replace('SEED = 917', 'SEED = 907')
source = source.replace('tmp/box_weapon_HornGold.original', 'tmp/box_material_fear_HornGold.original')
source = source.replace('显式种子917', '显式种子907')
source = source.replace('原宝盒两轮武器连续场景', '原宝盒材料与多目标连续场景')
exec(compile(source, str(base), 'exec'))
RULE = '宝盒普通公正剑弃真实材料、原击杀名望；同实体正式换轮后恐惧焚毁多目标，永久移除仅宝盒且借牌归底'


def play(card_id, choice, expected=None):
    click('Hand/Btn_Card_basic_card_' + card_id + '_0', '实际原牌 ' + card_id)
    click('Inspector/Btn_Choice_' + choice, '原印刷选择 ' + choice)
    click('Inspector/Btn_Play', '正式原费用及效果', expected)


click('Btn_JourneyEncounter_0', '第一战原哨卫032')
click('Inspector/Btn_Next', '正式格挡阶段')
play('016', 'strong', {'block': 5, 'blue': 0})
click('Enemy_0/Btn_Target', '实际原攻击4')
click('Inspector/Btn_Resolve', '原蓝费格挡5完整抵挡4', {'block': 0})
click('Inspector/Btn_Next', '正式承伤阶段')
click('Inspector/Btn_Next', '实际无伤近战', {'wounds': 0})
reveal('items_008', capture=True)
click('Inspector/Btn_ArtifactMaterials', '公正剑普通选择真实弃牌材料')
click('Hand/Btn_Card_basic_card_000_0', '选实际行进为材料，不选使用中宝盒')
click('Inspector/Btn_ArtifactMaterials', '确认实际材料引用，尚未付款',
      {'material.ids': 'basic_card_000', 'box.used': 'False'})
click('Inspector/Btn_ArtifactPlay', '真实弃行进换原攻击3，使用奖励名望1',
      {'discard.ids': 'basic_card_000', 'melee': 3, 'material.ids': '',
       'removed.ids': '', 'box.used': 'True', 'fame': 16}, True)
click('Enemy_0/Btn_Target', '原护甲3哨卫')
click('Inspector/Btn_Resolve', '原敌名望2加公正剑本回合击杀奖励1',
      {'kills': 1, 'melee': 0, 'fame': 19})
click('Inspector/Btn_Next', '正式战斗结束，剑奖励仍依原回合期限')
click('Inspector/Btn_EndTurn', '正式回合末清剑奖励并将借剑归底',
      {'journey.completedTurns': 1, 'supply.artifacts': 24,
       'box.revealed': '', 'removed.ids': '', 'wounds': 0, 'fame': 19})
click('Btn_Announce', '五原牌实际空库，正式声明轮末', {'deck.count': 0})
click('Btn_EndRound', '真实宝盒及材料行进一起洗回', {'journey.round': 2})
click('Btn_PrepareRound', '原五牌补回手，无伪造新材料',
      {'hand': 5, 'deck.count': 0, 'supply.artifacts': 24})
click('Tactic_night_1/Btn_Tactic_night_1', '正式夜间战术')
click('Btn_BeginTurn', '同玩家第二实际回合')
click('Btn_JourneyEncounter_2', '两枚独立实际哨卫032')
click('Inspector/Btn_Next', '原格挡阶段可以使用恐惧焚毁')
reveal('items_001', burn=True)
click('Enemy_0/Btn_Target', '选择第一个实际敌人')
click('Enemy_1/Btn_Target', '选择第二个实际敌人，保留多选')
click('Inspector/Btn_ArtifactPlay', '借原恐惧焚毁取消两个敌人，永久移除宝盒本体',
      {'enemies.skipped': '0,1', 'removed.ids': 'items_024',
       'box.used': 'True', 'box.revealed': 'items_001', 'fame': 20}, True)
click('Inspector/Btn_Next', '正式承伤阶段')
click('Inspector/Btn_Next', '两实际攻击均取消，无伤且不冒充成功格挡', {'wounds': 0})
play('021', 'strong', {'melee': 4, 'red': 0})
play('016', 'attack', {'melee': 6})
click('Enemy_0/Btn_Target', '选择第一原护甲3')
click('Enemy_1/Btn_Target', '选择第二原护甲3')
click('Inspector/Btn_Resolve', '原攻击6击杀两敌，只得两份原名望2，不残留上回合剑奖励',
      {'kills': 2, 'fame': 24, 'melee': 0})
click('Inspector/Btn_Next', '正式战斗结束')
click('Inspector/Btn_EndTurn', '借恐惧旗归供应底，只有物理盒永久移除',
      {'journey.completedTurns': 2, 'journey.completedBattles': 2,
       'supply.artifacts': 24, 'box.revealed': '', 'removed.ids': 'items_024',
       'wounds': 0, 'fame': 24, 'journey.samePlayer': 'True'}, True)

if '--from' in sys.argv:
    for step in steps[:int(sys.argv[sys.argv.index('--from') + 1])]:
        step['after'] = []
        step['skipScreenshot'] = True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(
    scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity', captureInitialView=False,
    initialDelaySeconds=2, defaultWaitAfterSeconds=.3, maxRunSeconds=360,
    useRunSubfolder=False, steps=steps), ensure_ascii=False, indent=2), encoding='utf8')
print(len(steps), 'steps', sum(len(step['after']) for step in steps), 'assertions')
