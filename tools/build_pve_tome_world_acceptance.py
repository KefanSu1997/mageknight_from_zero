"""Five original cards across two formal rounds: Tome Wings, then Tunnel assault.
Seed 57: original 44 advanced shuffle then 23 spells (owned magic_009 excluded).
Offer 023/011/014 becomes 011/014/004. Only --prepare/--restore mutates replay fixture.
"""
import json
import sys
from pathlib import Path

base = Path('tools/build_pve_box_weapon_acceptance.py')
source = base.read_text(encoding='utf8').split('\ndef reveal(', 1)[0]
source = source.replace('SEED = 917', 'SEED = 57')
source = source.replace('tmp/box_weapon_HornGold.original', 'tmp/tome_world_HornGold.original')
exec(compile(source, str(base), 'exec'))
RULE = '同一原宝典与真实同色材料；清风逐格消费实际移动，正式换轮后地道绑定实际紫色守军、原火球击杀并正式领取法术奖励'


def tome(material, offer, choice, burn=False):
    click('JourneyHand/@card=items_023', '同一实际法术宝典')
    click('JourneyHand/Btn_PlayOutside', '原材料与实际法术供应')
    click('MaterialArtifact/Btn_Burn' if burn else 'MaterialArtifact/Btn_Normal', '借原强效' if burn else '借原基础')
    click('MaterialArtifact/@card=' + material, '原同色实际支付牌', {'material.selected': material})
    click('MaterialArtifact/Btn_ArtifactOffer_' + str(offer), '实际供应法术')
    click('MaterialArtifact/Btn_TomeChoice_' + choice, '原法术分支')
    click('MaterialArtifact/Btn_ConfirmMaterial', '选择真实路径，尚未付款')


(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp', '宝典原世界连续路线'))
click('CampShade/Camp/Btn_Loadout', '既有可重放资源起点')
for _ in range(2):
    click('CampShade/Picker/Btn_NextPage', '下一页')
click('CampShade/Picker/Btn_Select_39', '显式种子57，既有红蓝各一库存')
click('CampShade/Camp/Btn_ChooseCards', '五张原牌')
p = 'CampShade/DeedPicker/'
click(p + 'Btn_ClearDeeds', '清除待选牌')
for card in ('000', '008'):
    click(p + 'Btn_ChooseDeed_basic_card_' + card, '原行进或承诺')
click(p + 'Btn_DeedKind_2', '原法术目录')
click(p + 'Btn_ChooseDeed_magic_009', '真实火焰魔球，依法排除公共供应')
click(p + 'Btn_DeedKind_3', '原神器目录')
click(p + 'Btn_ChooseDeed_items_004', '红宝石戒指')
click(p + 'Btn_DeedNext', '神器第二页')
click(p + 'Btn_ChooseDeed_items_023', '法术宝典')
click(p + 'Btn_DoneDeeds', '保存五原牌')
click('CampShade/Camp/Btn_SmallDeckHeroBuild', '正式英雄构筑')
click('CampShade/HeroBuild/Btn_Hero_Tovak', '原托瓦克')
click('CampShade/HeroBuild/Btn_Level_4', '合法等级4，原名望15')
click('CampShade/HeroBuild/Btn_Begin', '同玩家实际供给与既有世界地图',
      {'fame': 15, 'units.count': 0, 'supply.spells': 'magic_023,magic_011,magic_014'})
click('Tactic_day_1/Btn_Tactic_day_1', '正式白昼战术')
click('Btn_BeginTurn', '第一实际回合')
click('Btn_JourneyHand', '真实原手牌')
click('JourneyHand/@card=basic_card_000', '原绿色行进')
click('JourneyHand/Btn_OutsideChoice_base', '基础移动2')
click('JourneyHand/Btn_PlayOutside', '提供真实移动点', {'movement': 2})
tome('basic_card_008', 0, 'base')
click('DeedInputs/Btn_Path_mountain', '清风经过实际高山(0,-1)')
click('DeedInputs/Btn_Path_desert', '落在实际安全沙漠(1,-1)', capture=True)
click('DeedInputs/Btn_ConfirmInputs', '弃白承诺免白费；两格每格花移动1',
      {'journey.position': '1,-1', 'journey.terrain': 'Desert', 'movement': 0,
       'hand': 2, 'discard.ids': 'basic_card_008', 'removed.ids': '',
       'tokens.white': 0, 'fame': 15, 'supply.spells': 'magic_023,magic_011,magic_014'}, True)
click('JourneyHand/Btn_CloseHand', '返回同一实际旅程')
click('Btn_EndTurn', '正式弃已打出行进和宝典',
      {'journey.completedTurns': 1, 'hand': 2, 'deck.count': 0, 'journey.position': '1,-1'})
click('Btn_Announce', '实际空库正式宣轮')
click('Btn_EndRound', '仅底部清风供给回底；同宝典及材料正式洗回',
      {'journey.round': 2, 'supply.spells': 'magic_011,magic_014,magic_004'})
click('Btn_PrepareRound', '五张原牌全部补回，无需依赖洗牌顺序', {'hand': 5, 'deck.count': 0})
click('Tactic_night_1/Btn_Tactic_night_1', '正式夜间战术')
click('Btn_BeginTurn', '第二真实回合')
click('Btn_JourneyHand', '同一实际原手牌')
click('JourneyHand/@card=items_004', '红戒原图')
click('JourneyHand/Btn_ArtifactNormal', '普通红晶及红标')
click('JourneyHand/Btn_PlayOutside', '真实库存和临时标记分开', {'red': 2, 'tokens.red': 1, 'fame': 16})
tome('basic_card_000', 2, 'strong', burn=True)
click('DeedInputs/Btn_Path_mountain', '从沙漠进入相邻高山(0,-1)')
click('DeedInputs/Btn_Path_tower', '地道终点实际法师塔(-1,-1)', capture=True)
click('DeedInputs/Btn_ConfirmInputs', '弃绿行进并焚毁宝典，免印刷绿黑费；绑定原紫色守军',
      {'world.tunnel': 'True', 'world.ignoreSite': 'True', 'journey.position': '-1,-1',
       'journey.terrain': 'Plains', 'reputation': -1, 'green': 0, 'tokens.black': 0,
       'removed.ids': 'items_023', 'supply.spells': 'magic_011,magic_014,magic_004'})
click('Hand/Btn_Card_magic_009_0', '原火球')
click('Inspector/Btn_Choice_base', '火焰远程5')
click('Inspector/Btn_Play', '实际支付红戒提供的红token', {'tokens.red': 0, 'red': 2, 'ranged': 5})
click('Enemy_0/Btn_Target', '真实紫堆顶毒巫师002，护甲5，无固有城防')
click('Inspector/Btn_Resolve', '地道忽略地点城防，远程5击杀实际护甲5', {'kills': 1, 'fame': 20, 'wounds': 0})
for phase in ('格挡', '承伤', '近战', '结束'):
    click('Inspector/Btn_Next', '正式推进空敌阶段：' + phase, {'wounds': 0})
click('Inspector/Btn_FinishSite', '正式征服法师塔，守军归实际弃置堆',
      {'world.conquered': '-1:-1', 'world.rewardPending': 'True',
       'world.discard': 'monster_002', 'removed.ids': 'items_023'}, True)
click('SiteReward/Btn_Reward_0', '选择当前实际供应献祭作为正式法术奖励', {'world.reward': 'magic_011'})
click('Inspector/Btn_EndTurn', '正式回合末奖励原法术进手，保留原位置和已焚毁宝典',
      {'journey.completedTurns': 2, 'journey.completedBattles': 1,
       'journey.samePlayer': 'True', 'journey.position': '-1,-1', 'journey.terrain': 'Plains',
       'world.conquered': '-1:-1', 'world.tunnel': 'False', 'world.rewardPending': 'False',
       'hand.ids': 'basic_card_008,magic_011', 'removed.ids': 'items_023', 'fame': 20, 'wounds': 0})

if '--from' in sys.argv:
    for step in steps[:int(sys.argv[sys.argv.index('--from') + 1])]:
        step['after'] = []
        step['skipScreenshot'] = True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(
    scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity', captureInitialView=False,
    initialDelaySeconds=2, defaultWaitAfterSeconds=.3, maxRunSeconds=360,
    useRunSubfolder=False, steps=steps), ensure_ascii=False, indent=2), encoding='utf8')
print(len(steps), 'steps', sum(len(step['after']) for step in steps), 'assertions')
