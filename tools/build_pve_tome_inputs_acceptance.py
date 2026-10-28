"""Five original cards, two formal rounds: Tome Claim then Honor Summons.

Seed 1825 uses the actual shuffle: 44 advanced cards, then 24 spells.
Initial spell offer 012/010/020 becomes 010/020/015 at the formal round end.
The independent source RNG starts Red/Blue/Green. No supply or mana is injected.
Original Tome (printed 310/377) has no fame bonus; enemy kills give 15->17->19.
"""
import json
import sys
from pathlib import Path

# Reuse only seed backup/restore and the step helper, not another route's setup.
base = Path('tools/build_pve_box_weapon_acceptance.py')
source = base.read_text(encoding='utf8').split('\ndef reveal(', 1)[0]
source = source.replace('SEED = 917', 'SEED = 1825')
source = source.replace('tmp/box_weapon_HornGold.original', 'tmp/tome_inputs_HornGold.original')
exec(compile(source, str(base), 'exec'))
RULE = '原宝典弃真实同色牌，免所借法术印刷费用；保留实际供应，动态蓝骰及非首部队选择必须有真实消费者'


def play(card, choice, expected=None):
    click('Hand/Btn_Card_basic_card_' + card + '_0', '实际原牌 ' + card)
    click('Inspector/Btn_Choice_' + choice, '原印刷效果 ' + choice)
    click('Inspector/Btn_Play', '真实原费用与贡献', expected)


def tome(material, offer, choice, burn=False):
    click('Hand/Btn_Card_items_023_0', '当前真实手牌法术宝典')
    click('Inspector/Btn_OpenMaterialArtifact', '原宝典实际材料及当前法术供应')
    click('MaterialArtifact/Btn_Burn' if burn else 'MaterialArtifact/Btn_Normal',
          '焚毁借原强效' if burn else '普通借原基础效果')
    click('MaterialArtifact/@card=' + material, '可见原图选择同色支付牌',
          {'material.selected': material})
    click('MaterialArtifact/Btn_ArtifactOffer_' + str(offer), '当前实际供应法术')
    click('MaterialArtifact/Btn_TomeChoice_' + choice, '选择所借原效果')
    click('MaterialArtifact/Btn_ConfirmMaterial', '进入所借原法术动态输入，尚未付款')


(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp', '原宝典两轮真实动态输入'))
click('CampShade/Camp/Btn_Loadout', '既有可重放起点')
for _ in range(2):
    click('CampShade/Picker/Btn_NextPage', '下一页')
click('CampShade/Picker/Btn_Select_39', '显式种子1825及既有红蓝各一库存')
click('CampShade/Camp/Btn_ChooseCards', '五张原牌，无额外资源')
p = 'CampShade/DeedPicker/'
click(p + 'Btn_ClearDeeds', '清除待选牌')
click(p + 'Btn_ChooseDeed_basic_card_008', '原白色承诺作为第二轮支付牌')
click(p + 'Btn_DeedNext', '基础第二页')
for card in ('014', '016', '021'):
    click(p + 'Btn_ChooseDeed_basic_card_' + card, '原基本牌 ' + card)
click(p + 'Btn_DeedKind_3', '原神器目录')
click(p + 'Btn_DeedNext', '神器第二页')
click(p + 'Btn_ChooseDeed_items_023', '原法术宝典')
click(p + 'Btn_DoneDeeds', '保存五张原牌')
click('CampShade/Camp/Btn_SmallDeckHeroBuild', '合法原等级')
click('CampShade/HeroBuild/Btn_Hero_Tovak', '原托瓦克')
click('CampShade/HeroBuild/Btn_Level_4', '等级4原名望15及指挥槽')
click('CampShade/HeroBuild/Btn_Begin', '正式玩家及真实供应',
      {'hero.level': 4, 'fame': 15, 'units.count': 0,
       'supply.spells': 'magic_012,magic_010,magic_020'})
click('Tactic_day_1/Btn_Tactic_day_1', '原白昼战术')
click('Btn_BeginTurn', '第一实际回合', {'source.faces': 'Red,Blue,Green'})
click('Btn_JourneyEncounter_0', '第一枚原哨卫032')
click('Inspector/Btn_Next', '正式格挡阶段')
tome('basic_card_014', 0, 'base')
click('DeedInputs/Btn_ClaimColor_Blue', '非默认红色：选择实际蓝源骰',
      {'blue': 1, 'tokens.blue': 0, 'fame': 15}, True)
click('DeedInputs/Btn_ConfirmInputs', '真实弃蓝色耐力，免蓝费，保留所选源骰并获三蓝token',
      {'discard.ids': 'basic_card_014', 'blue': 1, 'tokens.blue': 3,
       'source.roundClaims': 'Blue', 'source.ordinaryUsed': 'False',
       'removed.ids': '', 'fame': 15, 'supply.spells': 'magic_012,magic_010,magic_020'})
play('016', 'strong', {'block': 5, 'tokens.blue': 2, 'blue': 1})
click('Enemy_0/Btn_Target', '实际敌人原攻击4')
click('Inspector/Btn_Resolve', '宣夺所得蓝token所付格挡真实消费', {'block': 0}, True)
click('Inspector/Btn_Next', '正式承伤阶段')
click('Inspector/Btn_Next', '完全格挡后无伤近战', {'wounds': 0})
play('021', 'strong', {'melee': 4, 'red': 0})
click('Enemy_0/Btn_Target', '原护甲3')
click('Inspector/Btn_Resolve', '原攻击击杀，宝典没有额外名望', {'kills': 1, 'fame': 17})
click('Inspector/Btn_Next', '正式战斗结束')
click('Inspector/Btn_EndTurn', '同玩家回合结束，临时蓝清空但源骰仍保留',
      {'journey.completedTurns': 1, 'tokens.blue': 0, 'blue': 1,
       'source.roundClaims': 'Blue', 'removed.ids': '', 'wounds': 0})
click('Btn_Announce', '五原牌实际空库，正式宣轮', {'deck.count': 0})
click('Btn_EndRound', '正式归还宣夺骰及法术供应底牌轮换',
      {'journey.round': 2, 'source.roundClaims': '',
       'supply.spells': 'magic_010,magic_020,magic_015'})
click('Btn_PrepareRound', '同一宝典及真实弃牌耐力重新洗回，正式轮换部队供应', {'hand': 5, 'deck.count': 0, 'offer.ids': 'army_2_007,army_2_008,army_2_012'})
click('Tactic_night_1/Btn_Tactic_night_1', '原夜间战术')
click('Btn_BeginTurn', '第二实际回合')
click('Btn_JourneyEncounter_4', '尚未击败的原狼骑兵007，迅捷攻击3与护甲4')
tome('basic_card_008', 1, 'strong', burn=True)
click('DeedInputs/Btn_HonorOffer_1', '明确选择非首供应凶蛮僧侣，合法空指挥槽',
      {'units.count': 0, 'fame': 17}, True)
click('DeedInputs/Btn_ConfirmInputs', '弃原白色承诺，焚毁宝典，免费取得所选真实部队',
      {'removed.ids': 'items_023', 'discard.ids': 'basic_card_008',
       'units.count': 1, 'units.ready': 'True', 'fame': 17,
       'offer.ids': 'army_2_007,army_2_012', 'white': 0, 'tokens.black': 0,
       'supply.spells': 'magic_010,magic_020,magic_015'})
click('Inspector/Btn_Next', '正式格挡阶段')
play('016', 'strong', {'block': 5, 'blue': 0})
click('Hand/Btn_Card_basic_card_014_0', '实际耐力横置补足迅捷格挡')
click('Inspector/Btn_Sideways', '原横置格挡1与5合并6', {'block': 6})
click('Enemy_0/Btn_Target', '实际狼骑兵迅捷攻击3')
click('Inspector/Btn_Resolve', '实际格挡6完整抵挡原迅捷攻击3乘2', {'block': 0})
click('Inspector/Btn_Next', '正式承伤阶段')
click('Inspector/Btn_Next', '无伤进入近战', {'wounds': 0})
play('021', 'attack', {'melee': 2})
click('Btn_Roster', '查看真正加入队伍的原凶蛮僧侣')
click('Units/Btn_Unit_0', '所选供给转入的同一部队')
click('Inspector/Btn_UnitAbility_0', '实际普通攻击3并耗竭，不使用自伤能力',
      {'melee': 5, 'units.ready': 'False', 'units.wounds': '0'})
click('Enemy_0/Btn_Target', '实际原狼骑兵护甲4')
click('Inspector/Btn_Resolve', '新取得部队攻击真正击杀', {'kills': 1, 'fame': 20}, True)
click('Inspector/Btn_Next', '第二场战斗正式结束')
click('Inspector/Btn_EndTurn', '保留同一玩家、耗竭部队与永久移除宝典，法术仍在供给',
      {'journey.completedTurns': 2, 'journey.completedBattles': 2,
       'journey.samePlayer': 'True', 'units.count': 1, 'units.ready': 'False',
       'removed.ids': 'items_023', 'wounds': 0, 'fame': 20,
       'supply.spells': 'magic_010,magic_020,magic_015'})

if '--from' in sys.argv:
    for step in steps[:int(sys.argv[sys.argv.index('--from') + 1])]:
        step['after'] = []
        step['skipScreenshot'] = True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(
    scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity', captureInitialView=False,
    initialDelaySeconds=2, defaultWaitAfterSeconds=.3, maxRunSeconds=360,
    useRunSubfolder=False, steps=steps), ensure_ascii=False, indent=2), encoding='utf8')
print(len(steps), 'steps', sum(len(step['after']) for step in steps), 'assertions')
