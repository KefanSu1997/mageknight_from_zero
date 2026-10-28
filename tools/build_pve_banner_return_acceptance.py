"""Original banners: keep both, actual unit combat, return both, reuse actual cards.

Level-2 hero owns one Northland Monk. Five original cards; the command banner
recruits from the real rotating supply without consuming an ordinary slot.
No configuration is written until this generator is explicitly invoked.
"""
import json
import sys
from pathlib import Path

steps = []
RULE = '原附旗默认保留及轮末实际取回：普通单位保留，指挥旗单位移除；同玩家真实洗回与后续招募消费者'


def click(path, label, expected=None, capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/' + path, label=label,
                      waitAfterSeconds=.3, skipScreenshot=not capture,
                      after=[dict(key=k, expected=str(v), rule=RULE)
                             for k, v in {'ui.truncated': '', **(expected or {})}.items()]))


def hand():
    click('Btn_JourneyHand', '同玩家实际手牌')


def close_hand():
    click('JourneyHand/Btn_CloseHand', '返回实际旅程')


def play(card, choice='base', expected=None):
    click('JourneyHand/@card=' + card, '选择真实原牌 ' + card)
    click('JourneyHand/Btn_OutsideChoice_' + choice, '原基础效果')
    click('JourneyHand/Btn_PlayOutside', '实际出牌', expected)


def attach_honor(expected):
    click('JourneyHand/@card=items_000', '实际原荣誉旌旗')
    click('JourneyHand/Btn_ArtifactNormal', '原普通附旗')
    click('JourneyHand/Btn_RecoveryUnit_0', '同一原北地僧侣为附旗目标')
    click('JourneyHand/Btn_PlayOutside', '真实牌离开手牌并附着', expected, True)


def command():
    click('JourneyHand/@card=items_017', '实际原指挥旌旗')
    click('JourneyHand/Btn_PlayOutside', '展开原指挥能力')
    click('SpecialArtifact/Btn_ConfirmSpecial', '原普通影响力4', {'influence': 4})


def recruit(card, expected):
    click('Btn_JourneyInteraction', '当前真实村庄供应')
    click('JourneyInteraction/Btn_BeginInteraction', '正式村庄交涉')
    click('JourneyInteraction/Btn_CommandRecruit_0', '本次招募以实际指挥旗代指挥槽',
          {'command.selected': 'items_017'})
    click('JourneyInteraction/Btn_Offer_' + card, '当前真实供应原单位 ' + card)
    click('JourneyInteraction/Btn_Recruit', '正式付款招募并附实际指挥旗', expected, True)
    click('JourneyInteraction/Btn_CloseInteraction', '保留真实新招部队')


(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp', '原旗帜轮末连续场景'))
click('CampShade/Camp/Btn_Loadout', '既有原构筑')
for _ in range(2):
    click('CampShade/Picker/Btn_NextPage', '下一页')
click('CampShade/Picker/Btn_Select_39', '原红蓝库存，不增加临时资源')
click('CampShade/Camp/Btn_ChooseUnits', '选择合法已有普通槽部队')
click('CampShade/UnitPicker/Btn_Unit_army_2_005', '原北地僧侣')
click('CampShade/UnitPicker/Btn_Back', '保留原单位')
click('CampShade/Camp/Btn_ChooseCards', '五张原牌')
p = 'CampShade/DeedPicker/'
click(p + 'Btn_ClearDeeds', '清空待选构筑')
for card in ('000', '008'):
    click(p + 'Btn_ChooseDeed_basic_card_' + card, '原基本牌 ' + card)
click(p + 'Btn_DeedNext', '原基本第二页')
click(p + 'Btn_ChooseDeed_basic_card_016', '原决心')
click(p + 'Btn_DeedKind_3', '原神器')
click(p + 'Btn_ChooseDeed_items_000', '原荣誉旌旗')
click(p + 'Btn_DeedNext', '原神器第二页')
click(p + 'Btn_ChooseDeed_items_017', '原指挥旌旗')
click(p + 'Btn_DoneDeeds', '保存五牌')
click('CampShade/Camp/Btn_SmallDeckHeroBuild', '合法原英雄等级')
click('CampShade/HeroBuild/Btn_Hero_Tovak', '原托瓦克')
click('CampShade/HeroBuild/Btn_Level_2', '等级2仅一个普通指挥槽')
click('CampShade/HeroBuild/Btn_Begin', '同玩家和真实供应',
      {'hero.level': 2, 'units.count': 1, 'fame': 3, 'command.free': 0})
click('Tactic_day_1/Btn_Tactic_day_1', '正式第一轮战术')
click('Btn_BeginTurn', '第一实际回合',
      {'offer.ids': 'army_2_001,army_2_006,army_2_007'})
hand()
play('basic_card_000', expected={'movement': 2})
close_hand()
click('Btn_JourneyMap', '自定验收布局中的真实村庄')
click('JourneyMap/Btn_Hex_plains', '原地形费用的村庄格')
click('JourneyMap/Btn_Move', '实际支付平原2', {'movement': 0, 'journey.position': '0,1'})
click('JourneyMap/Btn_CloseMap', '实际村庄')
hand()
attach_honor({'units.bannerIds': 'items_000', 'units.count': 1})
command()
close_hand()
recruit('army_2_006', {'influence': 0, 'units.count': 2, 'command.free': 0,
                       'command.banners': 1, 'units.bannerIds': 'items_000,items_017'})
click('Btn_EndTurn', '实际首回合结束', {'journey.completedTurns': 1})
click('Btn_Announce', '原牌库空，正式声明轮末',
      {'deck.count': 0, 'banners.window': 'True', 'banners.returnSelections': ''}, True)
click('Btn_EndRound', '不选择取回，默认保留两附旗',
      {'journey.round': 2, 'units.count': 2, 'units.bannerIds': 'items_000,items_017',
       'banners.window': 'False'})
click('Btn_PrepareRound', '正式新轮补牌，不抽回保留的旗',
      {'hand': 3, 'deck.count': 0, 'units.bannerIds': 'items_000,items_017',
       'units.ready': 'True,True', 'banners.returnSelections': ''}, True)
click('Tactic_night_1/Btn_Tactic_night_1', '正式夜间战术')
click('Btn_BeginTurn', '第二实际回合')
click('Btn_JourneyEncounter_0', '同两部队实际对原哨卫032')
click('Inspector/Btn_Next', '实际格挡阶段')
click('Hand/Btn_Card_basic_card_016_0', '原决心')
click('Inspector/Btn_Choice_block', '原基础格挡2')
click('Inspector/Btn_Play', '实际出牌', {'block': 2})
click('Btn_Roster', '同一保留指挥旗农民')
click('Units/Btn_Unit_1', '实际新招农民实体')
click('Inspector/Btn_UnitAbility_0', '原农民格挡2', {'block': 4, 'units.ready': 'True,False'})
click('Enemy_0/Btn_Target', '原攻击4目标')
click('Inspector/Btn_Resolve', '正式完整格挡消费4', {'block': 0})
click('Inspector/Btn_Next', '正式承伤阶段')
click('Inspector/Btn_Next', '进入近战且无伤', {'wounds': 0, 'units.wounds': '0,0'})
click('Units/Btn_Unit_0', '同一保留荣誉旗僧侣')
click('Inspector/Btn_UnitAbility_0', '原攻击3加荣誉1并得名望1',
      {'melee': 4, 'fame': 4, 'units.ready': 'False,False'}, True)
click('Enemy_0/Btn_Target', '原护甲3哨卫')
click('Inspector/Btn_Resolve', '真实攻击消费者及原名望2', {'kills': 1, 'fame': 6})
click('Inspector/Btn_Next', '正式战斗结束')
click('Inspector/Btn_EndTurn', '同玩家第二回合结束',
      {'journey.completedTurns': 2, 'journey.completedBattles': 1, 'wounds': 0})
click('Btn_Announce', '第二次实际轮末选择窗口', {'banners.window': 'True'})
click('Btn_BannerReturn_0_0', '选择取回原荣誉旗，尚不移除单位',
      {'banners.returnSelections': '0:0:items_000', 'units.count': 2})
click('Btn_BannerReturn_0_1', '选择取回指挥旗，尚未结算',
      {'banners.returnSelections': '0:0:items_000,0:1:items_017',
       'units.bannerIds': 'items_000,items_017', 'units.count': 2}, True)
click('Btn_EndRound', '正式取回两旗：仅移除指挥旗农民',
      {'journey.round': 3, 'units.count': 1, 'units.bannerIds': '',
       'command.banners': 0, 'command.free': 0, 'banners.returnSelections': '',
       'banners.window': 'False'}, True)
click('Btn_PrepareRound', '原两旗与三行动实际洗混回手',
      {'hand': 5, 'deck.count': 0, 'units.ready': 'True', 'units.wounds': 0,
       'offer.ids': 'army_2_011,army_2_013,army_2_002'}, True)
click('Tactic_day_1/Btn_Tactic_day_1', '正式第三轮战术')
click('Btn_BeginTurn', '取回后真实消费者回合')
hand()
attach_honor({'units.count': 1, 'units.bannerIds': 'items_000'})
command()
play('basic_card_008', expected={'influence': 6})
close_hand()
recruit('army_2_011', {'influence': 1, 'units.count': 2, 'command.free': 0,
                       'command.banners': 1, 'units.bannerIds': 'items_000,items_017'})
click('Btn_EndTurn', '取回原牌真实再附着与再招募，保留同一僧侣',
      {'journey.completedTurns': 3, 'journey.completedBattles': 1,
       'units.count': 2, 'units.bannerIds': 'items_000,items_017',
       'fame': 6, 'wounds': 0, 'journey.samePlayer': 'True'}, True)

# Keep only the meaningful round choices, combat consumer and final result.
for index, step in enumerate(steps):
    step['skipScreenshot'] = index not in (50, 68, 75, 98)
if '--from' in sys.argv:
    for step in steps[:int(sys.argv[sys.argv.index('--from') + 1])]:
        step['after'] = []
        step['skipScreenshot'] = True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(
    scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity', captureInitialView=False,
    initialDelaySeconds=2, defaultWaitAfterSeconds=.3, maxRunSeconds=360,
    useRunSubfolder=False, steps=steps), ensure_ascii=False, indent=2), encoding='utf8')
print(len(steps), 'steps', sum(len(step['after']) for step in steps), 'assertions')
