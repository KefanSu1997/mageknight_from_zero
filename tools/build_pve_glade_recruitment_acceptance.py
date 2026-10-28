"""Original supply R5 familiar, real glade recruitment and same-unit R6 upkeep.

Five original cards; @card selects the visible original-card button after real
round shuffles. No seed, supply, player, resource or card-zone overrides.
"""
import json
import sys
from pathlib import Path

steps = []
RULE = '原供应逐轮轮换；林地真实影响与额外色费招募，部队实体跨轮库存维持及真实移动消费'


def click(path, label, expect=None, capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/' + path, label=label,
                      waitAfterSeconds=.3, skipScreenshot=not capture,
                      after=[dict(key=k, expected=str(v), rule=RULE)
                             for k, v in {'ui.truncated': '', **(expect or {})}.items()]))


def play(card, choice, expect=None):
    click('JourneyHand/@card=' + card, '选择实际可见原牌 ' + card)
    click('JourneyHand/Btn_OutsideChoice_' + choice, '原印刷选择 ' + choice)
    click('JourneyHand/Btn_PlayOutside', '正式原牌费用和效果', expect)


def move(q, r, remaining):
    click(f'JourneyMap/Btn_Cell_{q}_{r}', '原相邻地形')
    click('JourneyMap/Btn_Move', '实际位置与移动费用',
          {'journey.position': f'{q},{r}', 'movement': remaining})


def round_end(next_round):
    click('Btn_Announce', '实际牌库空，单人声明轮次结束', {'deck.count': 0})
    click('Btn_EndRound', '正式轮末回收同一玩家原牌', {'journey.round': next_round})
    click('Btn_PrepareRound', '新轮真实供给轮换与补牌', {'journey.roundPrepared': 'True'})


def familiar_move(total):
    click('JourneyHand/Btn_OwnedUnits', '同一自有魔宠能力')
    click('OwnedUnits/Btn_Unit_0', '选择实际招募部队实体')
    click('OwnedUnits/Btn_Ability_2', '原移动或影响力3')
    click('OwnedUnits/Btn_Option_0', '选择移动')
    click('OwnedUnits/Btn_Use', '部队正式激活并耗竭',
          {'movement': total, 'units.ready': 'False', 'units.count': 1}, True)
    click('OwnedUnits/Btn_Close', '返回手牌')


(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp', '原林地招募连续场景'))
click('CampShade/Camp/Btn_Loadout', '既有合法构筑起点')
for _ in range(2):
    click('CampShade/Picker/Btn_NextPage', '下一页')
click('CampShade/Picker/Btn_Select_39', 'HornGold原红蓝库存各一')
click('CampShade/Camp/Btn_MapMode', '原A/1/2开局')
click('CampShade/Camp/Btn_ChooseCards', '选择五张原牌')
p = 'CampShade/DeedPicker/'
click(p + 'Btn_ClearDeeds', '清空待选构筑')
for card in ('000', '008'):
    click(p + 'Btn_ChooseDeed_basic_card_' + card, '原基本牌 ' + card)
click(p + 'Btn_DeedNext', '基本第二页')
click(p + 'Btn_ChooseDeed_basic_card_014', '原耐力')
click(p + 'Btn_DeedKind_1', '原高级行动')
click(p + 'Btn_DeedNext', '高级第二页')
click(p + 'Btn_ChooseDeed_advanced_card_018', '原外交')
for _ in range(2):
    click(p + 'Btn_DeedNext', '高级下一页')
click(p + 'Btn_ChooseDeed_advanced_card_039', '原山川逸闻')
click(p + 'Btn_DoneDeeds', '保存五张不同原牌')
click('CampShade/Camp/Btn_OriginalHero', '合法原英雄')
click('CampShade/HeroStart/Btn_Hero_Tovak', '原托瓦克')
click('CampShade/HeroStart/Btn_ConfiguredHero', '等级1无单位连续玩家')
click('Tactic_day_1/Btn_Tactic_day_1', '正式首日战术')
click('Btn_BeginTurn', '第一回合')
click('Btn_JourneyHand', '真实原手牌')
play('advanced_card_039', 'base', {'movement': 3})
play('basic_card_000', 'base', {'movement': 5})
play('basic_card_014', 'base', {'movement': 7, 'red': 1, 'blue': 1})
click('JourneyHand/Btn_CloseHand', '前往原林地')
click('Btn_JourneyMap', '原地图')
move(0, -1, 5)
move(1, -2, 3)
move(1, -3, 0)
click('JourneyMap/Btn_CloseMap', '实际林地位置')
click('Btn_EndTurn', '结束实际移动回合', {'journey.completedTurns': 1})

# Five cards mean an empty draw pile after every formal round preparation.
# The declaration replaces the player's turn; no artificial empty action needed.
for round_number in range(2, 6):
    round_end(round_number)
    tactic = 'night_1' if round_number % 2 == 0 else 'day_1'
    click(f'Tactic_{tactic}/Btn_Tactic_{tactic}', '正式昼夜战术',
          {'deck.count': 0, 'hand': 5, 'red': 1, 'blue': 1})

click('Btn_BeginTurn', '第五轮白昼林地开始', {'tokens.gold': 1})
click('Btn_JourneyTools', '原林地金魔力选择')
click('JourneyTools/Btn_SourceColor_White', '选择白色')
click('JourneyTools/Btn_UseGold', '真实金标记转白', {'tokens.gold': 0, 'tokens.white': 1})
click('JourneyTools/Btn_CloseTools', '返回')
click('Btn_JourneyHand', '五原牌真实洗混顺序')
play('basic_card_008', 'strong', {'influence': 4, 'tokens.white': 0})
play('advanced_card_018', 'base', {'influence': 6})
click('JourneyHand/Btn_CloseHand', '实际原供应')
click('Btn_JourneyMap', '原林地当前位置入口')
click('JourneyMap/Btn_Cell_1_-3', '选中当前实际林地')
click('JourneyMap/Btn_CurrentGlade', '进入林地招募，非交涉')
click('JourneyInteraction/Btn_Offer_army_2_004', '真实第五轮供应魔宠')
click('JourneyInteraction/Btn_RecruitMana_Red', '额外支付红魔力并留下红标记')
click('JourneyInteraction/Btn_Recruit', '真实影响6及红库存1招募',
      {'influence': 0, 'red': 0, 'blue': 1, 'units.count': 1,
       'units.ready': 'True', 'fame': 0, 'reputation': 0,
       'journey.interacting': 'False', 'journey.offerCount': 2,
       'units.attunements': 'army_2_004:Red:False'}, True)
click('JourneyInteraction/Btn_CloseInteraction', '保留已招同实体')
click('Btn_EndTurn', '招募行动正式结束', {'journey.completedTurns': 2})
click('Btn_BeginTurn', '同轮下一回合使用新招魔宠')
click('Btn_JourneyHand', '保留真实剩余手牌')
play('basic_card_014', 'base', {'movement': 2})
familiar_move(5)
click('JourneyHand/Btn_CloseHand', '消费真实部队移动')
click('Btn_JourneyMap', '向安全平原移动')
move(1, -2, 3)
move(2, -2, 1)
click('JourneyMap/Btn_CloseMap', '首次相邻狼骑而未持续相邻')
click('Btn_EndTurn', '同实体耗竭持续至轮末',
      {'journey.completedTurns': 3, 'units.ready': 'False', 'movement': 0})
round_end(6)
steps[-1]['after'].append(dict(key='units.attunements', expected='army_2_004:Red:True', rule=RULE))
click('Btn_Upkeep_Blue', '新轮战术前真实蓝库存维持同一魔宠',
      {'blue': 0, 'red': 0, 'units.count': 1, 'units.ready': 'True', 'units.wounds': 0,
       'units.attunements': 'army_2_004:Blue:False'}, True)
click('Tactic_night_1/Btn_Tactic_night_1', '完成维持后正式黑夜战术')
click('Btn_BeginTurn', '维持后同实体下一轮行动')
click('Btn_JourneyHand', '实际原牌')
play('advanced_card_039', 'base', {'movement': 3})
familiar_move(6)
click('JourneyHand/Btn_CloseHand', '同部队跨轮移动消费者')
click('Btn_JourneyMap', '离开狼骑相邻圈')
move(2, -1, 3)
click('JourneyMap/Btn_CloseMap', '原绿矿丘陵')
click('Btn_EndTurn', '正式回合结束并领取所在矿晶',
      {'journey.completedTurns': 4, 'journey.completedBattles': 0,
       'units.count': 1, 'units.ready': 'False', 'units.wounds': 0,
       'green': 1, 'blue': 0, 'red': 0, 'movement': 0,
       'journey.samePlayer': 'True'}, True)

if '--from' in sys.argv:
    for step in steps[:int(sys.argv[sys.argv.index('--from') + 1])]:
        step['after'] = []
        step['skipScreenshot'] = True
if '--display-only' in sys.argv:
    for index, step in enumerate(steps):
        step['after'] = [dict(key='ui.truncated', expected='', rule='仅补修改页面显示')] if index in (77, 90, 114) else []
        step['skipScreenshot'] = index not in (77, 90, 114)
    steps = steps[:115]
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(
    scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity', captureInitialView=False,
    initialDelaySeconds=2, defaultWaitAfterSeconds=.3, maxRunSeconds=420,
    useRunSubfolder=False, steps=steps), ensure_ascii=False, indent=2), encoding='utf8')
print(len(steps), 'steps', sum(len(step['after']) for step in steps), 'assertions')
