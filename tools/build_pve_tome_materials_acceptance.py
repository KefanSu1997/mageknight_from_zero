"""Actual Tome payment and separate materials, then selected discard recovery.

Five original cards; no battle snapshot or resource/supply injection. Seed 218
shuffles the actual 44 advanced/24 spells: 011,005,002,001. The formal second
round retains Meditation at offer 1. JourneyRoute forest (1,0) and village
(0,1) are adjacent; forest costs 5 at night. Four key images only.
"""
import json
import sys
from pathlib import Path

base = Path('tools/build_pve_box_weapon_acceptance.py')
source = base.read_text(encoding='utf8').split('\ndef reveal(', 1)[0]
source = source.replace('SEED = 917', 'SEED = 218')
source = source.replace('tmp/box_weapon_HornGold.original', 'tmp/tome_materials_HornGold.original')
exec(compile(source, str(base), 'exec'))
RULE = '宝典同色支付与额外材料保持不同原牌实例；真实库存付款移动、正式弃牌、神游回库与补牌后再次消费'


def open_hand():
    click('Btn_JourneyHand', '查看同一玩家实际原手牌')


def close_hand():
    click('JourneyHand/Btn_CloseHand', '返回真实旅程')


def stamina(strong, expected):
    click('JourneyHand/@card=basic_card_014', '选择实际耐力原图')
    click('JourneyHand/Btn_OutsideChoice_' + ('strong' if strong else 'base'),
          '原蓝费移动4' if strong else '原基础移动2')
    click('JourneyHand/Btn_PlayOutside', '真实出牌与原印刷费用', expected)


def sideways_rage(expected):
    click('JourneyHand/@card=basic_card_021', '选择实际狂怒原图')
    click('JourneyHand/Btn_SidewaysMove', '原普通牌横置移动1', expected)


def move(site, position, remaining, capture=False):
    click('Btn_JourneyMap', '原移动服务地图入口')
    click('JourneyMap/Btn_Hex_' + site, '选择实际相邻地形')
    click('JourneyMap/Btn_Move', '按实际昼夜地形费用移动',
          {'journey.position': position, 'movement': remaining}, capture)
    click('JourneyMap/Btn_CloseMap', '保留同一玩家位置')


def tome(material, offer, choice, burn=False):
    click('JourneyHand/@card=items_023', '同一真实法术宝典')
    click('JourneyHand/Btn_PlayOutside', '打开真实材料与法术供给')
    click('MaterialArtifact/Btn_Burn' if burn else 'MaterialArtifact/Btn_Normal',
          '焚毁借原神游' if burn else '普通借原献祭')
    click('MaterialArtifact/@card=' + material, '真实同色原牌付款',
          {'material.selected': material})
    click('MaterialArtifact/Btn_ArtifactOffer_' + str(offer), '选择实际供应法术')
    click('MaterialArtifact/Btn_TomeChoice_' + choice, '原法术效果选择')
    click('MaterialArtifact/Btn_ConfirmMaterial', '进入动态额外材料选择，尚未付款')


(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp', '原宝典材料与回收连续旅程'))
click('CampShade/Camp/Btn_Loadout', '既有可重放资源起点')
for _ in range(2):
    click('CampShade/Picker/Btn_NextPage', '下一页')
click('CampShade/Picker/Btn_Select_39', '显式种子218，红蓝各一原库存')
click('CampShade/Camp/Btn_ChooseCards', '五原牌构筑')
p = 'CampShade/DeedPicker/'
click(p + 'Btn_ClearDeeds', '清空待选')
click(p + 'Btn_ChooseDeed_basic_card_000', '绿色行进，留作神游付款')
click(p + 'Btn_DeedNext', '基础第二页')
for card in ('014', '016', '021'):
    click(p + 'Btn_ChooseDeed_basic_card_' + card, '原基本牌 ' + card)
click(p + 'Btn_DeedKind_3', '原神器目录')
click(p + 'Btn_DeedNext', '神器第二页')
click(p + 'Btn_ChooseDeed_items_023', '原法术宝典')
click(p + 'Btn_DoneDeeds', '保存五张原牌')
click('CampShade/Camp/Btn_SmallDeckHeroBuild', '合法原英雄构筑')
click('CampShade/HeroBuild/Btn_Hero_Tovak', '原托瓦克')
click('CampShade/HeroBuild/Btn_Level_4', '合法等级4，原名望15')
click('CampShade/HeroBuild/Btn_Begin', '同一玩家正式旅程',
      {'fame': 15, 'units.count': 0, 'supply.spells': 'magic_011,magic_005,magic_002'})
click('Tactic_day_1/Btn_Tactic_day_1', '正式白昼战术')
click('Btn_BeginTurn', '第一实际回合')
open_hand()
tome('basic_card_021', 0, 'base')
# Eligible visible materials are 000,014,016: Tome and red payment are excluded.
click('DeedInputs/Btn_InputMaterial_2', '额外材料选择蓝色决心，与红付款狂怒是不同实体',
      {'red': 1, 'blue': 1}, True)
click('DeedInputs/Btn_ConfirmInputs', '弃红付款与蓝材料；献祭给原红晶及材料蓝晶',
      {'red': 2, 'blue': 2, 'tokens.red': 0, 'tokens.blue': 0,
       'discard.ids': 'basic_card_016,basic_card_021', 'hand': 2,
       'removed.ids': '', 'fame': 15, 'supply.spells': 'magic_011,magic_005,magic_002'})
stamina(True, {'movement': 4, 'blue': 1})
close_hand()
move('plains', '0,1', 2, True)
click('Btn_EndTurn', '正式回合末弃已打出宝典与耐力，清剩余移动',
      {'journey.completedTurns': 1, 'hand': 1, 'deck.count': 0, 'movement': 0, 'blue': 1})
click('Btn_Announce', '真实空库宣轮')
click('Btn_EndRound', '正式洗回同一宝典及材料，实际供给只轮换最底法术',
      {'journey.round': 2, 'supply.spells': 'magic_005,magic_002,magic_001'})
click('Btn_PrepareRound', '正式补回原五牌', {'hand': 5, 'deck.count': 0})
click('Tactic_night_1/Btn_Tactic_night_1', '正式夜间战术')
click('Btn_BeginTurn', '第二实际回合，先形成真实弃牌')
open_hand()
stamina(True, {'movement': 4, 'blue': 0})
sideways_rage({'movement': 5})
close_hand()
move('forest', '1,0', 0)
click('Btn_EndTurn', '按正式回合末把耐力和横置狂怒转入弃牌',
      {'journey.completedTurns': 2, 'hand': 3,
       'discard.ids': 'basic_card_021,basic_card_014', 'journey.position': '1,0'})
click('Btn_BeginTurn', '第三实际回合保留同一宝典和原弃牌')
open_hand()
tome('basic_card_000', 1, 'strong_top', burn=True)
click('DeedInputs/Btn_InputMaterial_1', '选择上回合真实弃牌耐力，作为牌库顶第一张')
click('DeedInputs/Btn_InputMaterial_0', '选择上回合真实弃牌狂怒，作为牌库顶第二张', capture=True)
click('DeedInputs/Btn_ConfirmInputs', '另弃绿色行进，焚毁宝典；所选两弃牌真实移回牌库顶',
      {'removed.ids': 'items_023', 'discard.ids': 'basic_card_000',
       'deck.count': 2, 'draw.nextBonus': 2, 'hand': 1, 'green': 0,
       'tokens.black': 0, 'fame': 15, 'supply.spells': 'magic_005,magic_002,magic_001'})
close_hand()
click('Btn_EndTurn', '正式补牌实际抽回所选两牌，消耗本次手牌上限奖励',
      {'journey.completedTurns': 3, 'deck.count': 0, 'draw.nextBonus': 0,
       'hand.ids': 'basic_card_016,basic_card_014,basic_card_021'})
click('Btn_BeginTurn', '第四实际回合消费真正抽回的原牌')
open_hand()
stamina(False, {'movement': 2, 'blue': 0})
sideways_rage({'movement': 3})
close_hand()
move('plains', '0,1', 1, True)
click('Btn_EndTurn', '回收两牌再次实际移动后正式弃牌；没有重建玩家或凭空抽牌',
      {'journey.completedTurns': 4, 'journey.samePlayer': 'True',
       'journey.position': '0,1', 'movement': 0, 'fame': 15, 'wounds': 0,
       'removed.ids': 'items_023', 'hand.ids': 'basic_card_016',
       'discard.ids': 'basic_card_021,basic_card_014,basic_card_000',
       'supply.spells': 'magic_005,magic_002,magic_001'})

if '--from' in sys.argv:
    for step in steps[:int(sys.argv[sys.argv.index('--from') + 1])]:
        step['after'] = []
        step['skipScreenshot'] = True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(
    scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity', captureInitialView=False,
    initialDelaySeconds=2, defaultWaitAfterSeconds=.3, maxRunSeconds=360,
    useRunSubfolder=False, steps=steps), ensure_ascii=False, indent=2), encoding='utf8')
print(len(steps), 'steps', sum(len(step['after']) for step in steps), 'assertions')

