"""Two actual heroes, basic read then next-night steal (or reveal/decline).

No seed dependency: Arythea's own Dark Fire supplies the night black token;
the original White Ring supplies white. Both heroes retain real five-card decks.
Only the caster owns the unique ring/spell. --from preserves replayable prefixes.
"""
import json
import sys
from pathlib import Path

reveal = len(sys.argv) > 1 and sys.argv[1] == 'reveal'
steps = []
RULE = '各实际玩家本人响应同色弃牌或无同色展示；施法者明确可空偷取，原费用/永久归属/实际消费者与正式双人轮转'


def click(path, label, expected=None, capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/' + path, label=label,
                      waitAfterSeconds=.3, skipScreenshot=not capture,
                      after=[dict(key=k, expected=str(v), rule=RULE)
                             for k, v in {'ui.truncated': '', **(expected or {})}.items()]))


def hand():
    click('Btn_JourneyHand', '当前实际玩家手牌')


def close():
    click('JourneyHand/Btn_CloseHand', '返回当前实际玩家旅程')


def travel(card, strong, expected):
    click('JourneyHand/@card=basic_card_' + card, '实际原牌 ' + card)
    click('JourneyHand/Btn_OutsideChoice_' + ('strong' if strong else 'base'), '原移动效果')
    click('JourneyHand/Btn_PlayOutside', '真实原费用及贡献', expected)


def move(site, position, remaining, capture=False):
    click('Btn_JourneyMap', '真实地图消费者')
    click('JourneyMap/Btn_Hex_' + site, '实际相邻地形')
    click('JourneyMap/Btn_Move', '支付原地形移动费用',
          {'journey.position': position, 'movement': remaining}, capture)
    click('JourneyMap/Btn_CloseMap', '保留同一玩家位置')


def ring(fame, white):
    click('JourneyHand/@card=items_006', '施法者唯一原白宝石戒指')
    click('JourneyHand/Btn_ArtifactNormal', '原普通效果')
    click('JourneyHand/Btn_PlayOutside', '原白token及白晶各一，原使用名望1',
          {'tokens.white': 1, 'white': white, 'fame': fame})


def read(strong, color, response=None):
    click('JourneyHand/@card=magic_018', '施法者唯一原思维读取')
    click('JourneyHand/Btn_OutsideChoice_' + ('strong' if strong else 'base'), '原强化' if strong else '原基础')
    click('JourneyHand/Btn_PlayOutside', '正式多人动态输入')
    click('DeedInputs/Btn_InputColor_0_' + color, '施法者选择原基本色')
    click('DeedInputs/Btn_MindRespond', '交接给真正被影响玩家',
          {'mind.responder': 'Tovak', 'hero.id': 'Arythea', 'journey.playerId': 0})
    click('MindResponse/Btn_MindBeginResponse', '托瓦克本人确认并查看响应手牌')
    if response:
        click('MindResponse/@card=basic_card_' + response, '被影响玩家本人选实际同色牌')
    click('MindResponse/Btn_MindConfirmResponse', '本人确认无同色展示' if response is None else '本人确认所选弃牌',
          {'mind.responses': 1, 'hero.id': 'Arythea', 'journey.playerId': 0}, not strong)


(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp', '双英雄真实读取与' + ('展示/放弃' if reveal else '永久偷取')))
click('CampShade/Camp/Btn_Loadout', '既有原红蓝库存、零部队')
for _ in range(2):
    click('CampShade/Picker/Btn_NextPage', '下一页')
click('CampShade/Picker/Btn_Select_39', '原HornGold构筑资源，不取随机源')
p = 'CampShade/DeedPicker/'
h = 'CampShade/HeroBuild/'
click('CampShade/Camp/Btn_ChooseCards', '施法者五张原牌')
click(p + 'Btn_ClearDeeds', '清空待选')
if reveal:
    click(p + 'Btn_ChooseDeed_basic_card_000', '本人原行进，放弃偷取后的绿晶消费者')
click(p + 'Btn_DeedNext', '基础第二页')
for card in (('014', '021') if reveal else ('014', '016', '021')):
    click(p + 'Btn_ChooseDeed_basic_card_' + card, '原基本牌 ' + card)
click(p + 'Btn_DeedKind_2', '原法术目录')
click(p + 'Btn_DeedNext', '法术第二页')
click(p + 'Btn_ChooseDeed_magic_018', '唯一原思维读取')
click(p + 'Btn_DeedKind_3', '原神器目录')
click(p + 'Btn_ChooseDeed_items_006', '唯一原白宝石戒指')
click(p + 'Btn_DoneDeeds', '保存施法者五牌')
click('CampShade/Camp/Btn_SmallDeckHeroBuild', '合法原英雄')
click(h + 'Btn_Hero_Arythea', '阿瑞希亚原技能归属')
click(h + 'Btn_Level_2', '合法等级2一个技能')
click(h + 'Btn_Skill_4', '本人原暗火魔法，每轮一次')
click(h + 'Btn_AddCompanion', '保留真实第一玩家及其唯一牌')
click(h + 'Btn_Back', '仅返回构筑配置第二位')
click('CampShade/Camp/Btn_ChooseCards', '第二玩家独立五基本牌，绝不重复法术神器')
click(p + 'Btn_ClearDeeds', '清空第二位待选')
for card in ('000', '001'):
    click(p + 'Btn_ChooseDeed_basic_card_' + card, '原绿色基本牌 ' + card)
click(p + 'Btn_DeedNext', '基础第二页')
for card in ('014', '016', '021'):
    click(p + 'Btn_ChooseDeed_basic_card_' + card, '原基本牌 ' + card)
click(p + 'Btn_DoneDeeds', '第二位五张不同原基本牌')
click('CampShade/Camp/Btn_SmallDeckHeroBuild', '第二位合法原英雄')
click(h + 'Btn_Hero_Tovak', '不同原英雄托瓦克')
click(h + 'Btn_Level_2', '合法等级2，不添加其他英雄技能')
click(h + 'Btn_Begin', '同一实际双英雄连续旅程', {'journey.players': 2})
click('Tactic_day_1/Btn_Tactic_day_1', '阿瑞希亚先手')
click('Tactic_day_4/Btn_Tactic_day_4', '托瓦克后手')
click('Btn_BeginTurn', '施法者第一实际回合', {'hero.id': 'Arythea', 'journey.playerId': 0})
hand()
ring(4, 1)
read(False, 'White' if reveal else 'Blue', None if reveal else '016')
expected = {'mind.resultOpen': 'True', 'tokens.white': 0, 'hero.id': 'Arythea', 'journey.playerId': 0}
if reveal:
    expected.update(white=2, **{'mind.reveals': 'Tovak:basic_card_000,basic_card_001,basic_card_014,basic_card_016,basic_card_021',
                              'party.discards': 'Arythea:;Tovak:'})
else:
    expected.update(blue=2, **{'mind.reveals': '', 'party.discards': 'Arythea:;Tovak:basic_card_016'})
click('DeedInputs/Btn_ConfirmInputs', '白token支付原费，原效果真实结算各玩家响应', expected, True)
click('MindResult/Btn_CloseMindResult', '返回施法者而非切换活动玩家')
travel('014', True, {'movement': 4, 'blue': 0 if reveal else 1})
close()
move('forest', '1,0', 1)
click('Btn_EndTurn', '施法者正式结束，保留独立玩家牌区', {'journey.completedTurns': 1})
click('Btn_BeginTurn', '托瓦克现在才开始真正回合', {'hero.id': 'Tovak', 'journey.playerId': 1})
hand()
travel('014', False, {'movement': 2})
close()
move('plains', '0,1', 0)
click('Btn_EndTurn', '第二位完成真实回合', {'journey.completedTurns': 2})
click('Btn_Announce', '阿瑞希亚空库宣布轮末，不跳过对方最后回合', {'deck.count': 0})
click('Btn_BeginTurn', '托瓦克最后一个合法回合', {'hero.id': 'Tovak'})
hand()
click('JourneyHand/@card=basic_card_021', '原狂怒横置，保留两绿牌')
click('JourneyHand/Btn_SidewaysMove', '最后回合实际出牌移动1', {'movement': 1})
close()
click('Btn_EndTurn', '正式结束对方最后回合', {'journey.completedTurns': 3})
click('Btn_EndRound', '同一两玩家原手牌及弃牌正式洗回', {'journey.round': 2})
click('Btn_PrepareRound', '正式夜间补牌')
click('Tactic_night_1/Btn_Tactic_night_1', '阿瑞希亚夜间先手')
click('Tactic_night_4/Btn_Tactic_night_4', '托瓦克夜间后手，无强制随机源输入')
click('Btn_BeginTurn', '施法者第二轮真实回合', {'hero.id': 'Arythea', 'journey.playerId': 0, 'hand': 5})
hand()
ring(5, 3 if reveal else 2)
click('JourneyHand/Btn_Support', '本人原技能入口')
click('JourneySupport/Btn_OutsideSkill_0', '本人暗火魔法')
click('JourneySupport/Btn_OutsideOption_1', '明确选择原黑色token')
click('JourneySupport/Btn_UseOutsideSkill', '真实每轮技能红晶与黑魔力', {'tokens.black': 1, 'red': 2})
click('JourneySupport/Btn_BackSupport', '返回本人原手牌')
read(True, 'Green', '000')
click('DeedInputs/Btn_MindStealNone' if reveal else 'DeedInputs/Btn_MindSteal_0',
      '施法者明确放弃偷取' if reveal else '施法者选择本次原行动000永久偷取',
      {'mind.stolen': '' if reveal else 'basic_card_000'}, True)
own = 'basic_card_000,basic_card_014,basic_card_021' if reveal else 'basic_card_000,basic_card_014,basic_card_016,basic_card_021'
click('DeedInputs/Btn_ConfirmInputs', '真实白黑原费用、绿晶与永久归属结算',
      {'tokens.white': 0, 'tokens.black': 0, 'green': 1, 'mind.resultOpen': 'True',
       'party.hands': 'Arythea:' + own + ';Tovak:basic_card_001,basic_card_014,basic_card_016,basic_card_021',
       'party.discards': 'Arythea:;Tovak:' + ('basic_card_000' if reveal else '')})
click('MindResult/Btn_CloseMindResult', '保留正式归属结果继续原回合')
travel('000', True, {'movement': 4, 'green': 0})
close()
move('camp', '0,0', 2, True)
click('Btn_EndTurn', '原行动真实消费后正式弃牌，未偷者牌留原主，被偷者牌留施法者',
      {'journey.completedTurns': 4, 'journey.samePlayer': 'True', 'hero.id': 'Tovak', 'fame': 3,
       'party.discards': 'Arythea:basic_card_000,items_006,magic_018;Tovak:' + ('basic_card_000' if reveal else '')})

if '--from' in sys.argv:
    for step in steps[:int(sys.argv[sys.argv.index('--from') + 1])]:
        step['after'] = []
        step['skipScreenshot'] = True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(
    scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity', captureInitialView=False,
    initialDelaySeconds=2, defaultWaitAfterSeconds=.3, maxRunSeconds=420,
    useRunSubfolder=False, steps=steps), ensure_ascii=False, indent=2), encoding='utf8')
print('reveal' if reveal else 'primary', len(steps), 'steps', sum(len(s['after']) for s in steps), 'assertions')
