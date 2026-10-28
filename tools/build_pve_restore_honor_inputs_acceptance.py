"""Actual two-unit Rebirth, or fourth offered Honor Summons; no fixture units.

Commoners is an existing fame-8 two-slot loadout. Its visible unit picker removes
Thugs and adds Northern Monks before a legal level-4 HeroBuild. Seed 27 gives
actual spells 020/005/006 (44 advanced, 24 spells). Only replay flags are prepared.
"""
import json
import sys
from pathlib import Path

variant = sys.argv[1] if len(sys.argv) > 1 and sys.argv[1] in ('restore', 'honor') else 'restore'
restore = variant == 'restore'
fixture = Path('Assets/GameData/BattleDemo/' + ('Commoners.asset' if restore else 'HornGold.asset'))
backup = Path('tmp/restore_honor_' + variant + '.original')


def prepared(original):
    assert original.count(b'effectRandomSeed: 0') == 1
    result = original.replace(b'effectRandomSeed: 0', b'effectRandomSeed: 27')
    if restore:
        assert original.count(b'replayEffectRandom: 0') == 1
        result = result.replace(b'replayEffectRandom: 0', b'replayEffectRandom: 1')
    return result


if '--restore' in sys.argv:
    original = backup.read_bytes()
    assert fixture.read_bytes() == prepared(original), 'Fixture changed independently; refusing overwrite.'
    fixture.write_bytes(original)
    print('Exact original bytes restored')
    raise SystemExit
if '--prepare' in sys.argv:
    original = fixture.read_bytes()
    patched = prepared(original)
    if backup.exists():
        assert backup.read_bytes() == original
    else:
        backup.parent.mkdir(parents=True, exist_ok=True)
        backup.write_bytes(original)
    fixture.write_bytes(patched)

steps = []
RULE = '原部队合法指挥槽、实际耗竭与多选重整；或忠诚取得真实供应追加及荣誉分页招募，后续能力真实消费'


def click(path, label, expected=None, capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/' + path, label=label,
                      waitAfterSeconds=.3, skipScreenshot=not capture,
                      after=[dict(key=k, expected=str(v), rule=RULE)
                             for k, v in {'ui.truncated': '', **(expected or {})}.items()]))


def play(card, choice, expected=None):
    click('Hand/Btn_Card_basic_card_' + card + '_0', '实际原牌 ' + card)
    click('Inspector/Btn_Choice_' + choice, '原印刷效果')
    click('Inspector/Btn_Play', '真实原牌贡献', expected)


def unit(index, expected=None):
    click('Units/Btn_Unit_' + str(index), '选择同一实际部队')
    click('Inspector/Btn_UnitAbility_0', '正式原能力并耗竭', expected)


(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp', '原重生多选' if restore else '原荣誉号召供应分页'))
click('CampShade/Camp/Btn_Loadout', '选择既有合法构筑')
for _ in range(3 if restore else 2):
    click('CampShade/Picker/Btn_NextPage', '下一页')
click('CampShade/Picker/Btn_Select_' + ('60' if restore else '39'), '既有原资源和合法指挥槽')
if restore:
    click('CampShade/Camp/Btn_ChooseUnits', '既有名望8允许两个普通指挥槽')
    click('CampShade/UnitPicker/Btn_Unit_army_2_011', '移除已选暴徒，保留原农民')
    click('CampShade/UnitPicker/Btn_Unit_army_2_005', '合法选择北地僧侣，占第二普通槽')
    click('CampShade/UnitPicker/Btn_Back', '原农民等级1与僧侣等级2')
click('CampShade/Camp/Btn_ChooseCards', '五张原牌')
p = 'CampShade/DeedPicker/'
click(p + 'Btn_ClearDeeds', '清空待选')
for card in (('000', '007') if restore else ('008',)):
    click(p + 'Btn_ChooseDeed_basic_card_' + card, '原基本牌 ' + card)
click(p + 'Btn_DeedNext', '基础第二页')
for card in (('016', '021') if restore else ('014', '016', '021')):
    click(p + 'Btn_ChooseDeed_basic_card_' + card, '原基本牌 ' + card)
click(p + 'Btn_DeedKind_3', '原神器')
click(p + 'Btn_DeedNext', '神器第二页')
click(p + 'Btn_ChooseDeed_items_023', '原宝典')
click(p + 'Btn_DoneDeeds', '保存五原牌')
click('CampShade/Camp/Btn_SmallDeckHeroBuild', '合法原英雄构筑')
click('CampShade/HeroBuild/Btn_Hero_' + ('Tovak' if restore else 'Norowas'), '原英雄')
click('CampShade/HeroBuild/Btn_Level_4', '合法等级4及正式指挥槽')
if not restore:
    click('CampShade/HeroBuild/Btn_Skill_7', '诺若威斯本人忠诚取得时追加真实常规供应')
click('CampShade/HeroBuild/Btn_Begin', '正式同一玩家',
      {'hero.level': 4, 'fame': 15, 'units.count': 2 if restore else 0,
       'supply.spells': 'magic_020,magic_005,magic_006'})
click('Tactic_day_1/Btn_Tactic_day_1', '原白昼战术')
click('Btn_BeginTurn', '正式首回合')

if restore:
    click('Btn_JourneyEncounter_0', '实际首战单哨卫032')
    click('Inspector/Btn_Next', '原格挡阶段')
    play('016', 'block', {'block': 2})
    click('Btn_Roster', '两支实际原部队')
    unit(0, {'block': 4, 'units.ready': 'False,True'})
    click('Enemy_0/Btn_Target', '原攻击4')
    click('Inspector/Btn_Resolve', '农民与原决心共同完整格挡', {'block': 0})
    click('Inspector/Btn_Next', '承伤阶段')
    click('Inspector/Btn_Next', '无伤近战', {'wounds': 0})
    unit(1, {'melee': 3, 'units.ready': 'False,False'})
    click('Enemy_0/Btn_Target', '原护甲3')
    click('Inspector/Btn_Resolve', '僧侣实际攻击击杀', {'kills': 1, 'fame': 17}, True)
    click('Inspector/Btn_Next', '正式结束战斗，仍为同一回合')
    click('Btn_JourneyHand', '战后真实剩余手牌')
    click('JourneyHand/@card=items_023', '原宝典')
    click('JourneyHand/Btn_PlayOutside', '战外原材料入口')
else:
    click('Btn_JourneyEncounter_0', '实际单哨卫032')
    click('Hand/Btn_Card_items_023_0', '原宝典')
    click('Inspector/Btn_OpenMaterialArtifact', '原材料入口')

click('MaterialArtifact/Btn_Burn', '焚毁借原强效')
click('MaterialArtifact/@card=basic_card_' + ('000' if restore else '008'), '实际同色付款牌')
click('MaterialArtifact/Btn_ArtifactOffer_' + ('1' if restore else '0'), '当前真实原法术供应')
click('MaterialArtifact/Btn_TomeChoice_strong', '原强化效果')
click('MaterialArtifact/Btn_ConfirmMaterial', '进入真实部队输入')
if restore:
    click('DeedInputs/Btn_RestoreUnit_0', '选择实际耗竭农民等级1')
    click('DeedInputs/Btn_RestoreUnit_1', '另选实际耗竭僧侣等级2',
          {'restore.selected': 'army_2_006,army_2_005', 'restore.levels': 3,
           'units.ready': 'False,False'}, True)
    click('DeedInputs/Btn_ConfirmInputs', '平原总等级3合法重整两原单位',
          {'units.ready': 'True,True', 'units.wounds': '0,0',
           'removed.ids': 'items_023', 'fame': 17})
    click('JourneyHand/Btn_CloseHand', '保留真实恢复结果')
    click('Inspector/Btn_EndTurn', '正式结束首回合，不额外重整单位', {'journey.completedTurns': 1})
    click('Btn_BeginTurn', '下一实际回合')
    click('Btn_JourneyHand', '同一玩家战外单位入口')
    click('JourneyHand/Btn_OwnedUnits', '查看两实际部队')
    click('OwnedUnits/Btn_Unit_0', '重生恢复后的同一农民')
    click('OwnedUnits/Btn_Ability_2', '原农民移动2')
    click('OwnedUnits/Btn_Use', '实际移动贡献并再耗竭', {'movement': 2, 'units.ready': 'False,True'})
    click('OwnedUnits/Btn_Close', '返回手牌')
    click('JourneyHand/Btn_CloseHand', '返回地图')
    click('Btn_JourneyMap', '正式移动')
    click('JourneyMap/Btn_Hex_plains', '相邻村庄')
    click('JourneyMap/Btn_Move', '消费农民真实移动2', {'movement': 0, 'journey.position': '0,1'}, True)
    click('JourneyMap/Btn_CloseMap', '保持原位置')
    click('Btn_JourneyEncounter_4', '另一未击败真实狼骑兵007，不复用已死哨卫')
    click('Inspector/Btn_Next', '原迅捷格挡阶段')
    click('Inspector/Btn_Next', '不伪造不足格挡，真实承伤')
    click('Inspector/Btn_Next', '原攻击3与英雄护甲3造成一真实伤牌', {'wounds': 1})
    play('021', 'attack', {'melee': 2})
    click('Btn_Roster', '重生恢复后的同一僧侣')
    unit(1, {'melee': 5, 'units.ready': 'False,False'})
    click('Enemy_0/Btn_Target', '原狼骑兵护甲4')
    click('Inspector/Btn_Resolve', '恢复僧侣原攻击3加狂怒2真实击杀', {'kills': 1, 'fame': 20}, True)
else:
    click('DeedInputs/Btn_HonorNext', '真实五张供应第二页', {'honor.page': 1})
    click('DeedInputs/Btn_HonorOffer_3', '选择第四张实际赤袍僧侣', {'honor.selected': 'army_2_007'}, True)
    click('DeedInputs/Btn_HonorPrevious', '翻回首页保留实际选择', {'honor.page': 0, 'honor.selected': 'army_2_007'})
    click('DeedInputs/Btn_HonorNext', '再次查看第二页', {'honor.page': 1, 'honor.selected': 'army_2_007'})
    click('DeedInputs/Btn_ConfirmInputs', '白牌付款与真实供应转入合法自有槽',
          {'units.count': 1, 'units.ready': 'True', 'removed.ids': 'items_023',
           'offer.ids': 'army_2_001,army_2_005,army_2_006,army_2_008', 'fame': 15}, True)
    click('Inspector/Btn_Next', '原格挡阶段')
    play('016', 'strong', {'block': 5, 'blue': 0})
    click('Enemy_0/Btn_Target', '实际原攻击4')
    click('Inspector/Btn_Resolve', '实际蓝费格挡5消费', {'block': 0})
    click('Inspector/Btn_Next', '承伤阶段')
    click('Inspector/Btn_Next', '无伤近战', {'wounds': 0})
    click('Btn_Roster', '实际新取得原赤袍僧侣')
    unit(0, {'melee': 3, 'units.ready': 'False'})
    click('Enemy_0/Btn_Target', '实际原护甲3')
    click('Inspector/Btn_Resolve', '第四张所选单位真实攻击击杀', {'kills': 1, 'fame': 17}, True)
click('Inspector/Btn_Next', '正式战斗结束')
click('Inspector/Btn_EndTurn', '保留真实玩家、单位耗竭及伤牌',
      {'journey.completedTurns': 2 if restore else 1,
       'journey.completedBattles': 2 if restore else 1,
       'journey.samePlayer': 'True', 'wounds': 1 if restore else 0,
       'removed.ids': 'items_023', 'supply.spells': 'magic_020,magic_005,magic_006'})

if '--from' in sys.argv:
    for step in steps[:int(sys.argv[sys.argv.index('--from') + 1])]:
        step['after'] = []
        step['skipScreenshot'] = True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(
    scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity', captureInitialView=False,
    initialDelaySeconds=2, defaultWaitAfterSeconds=.3, maxRunSeconds=360,
    useRunSubfolder=False, steps=steps), ensure_ascii=False, indent=2), encoding='utf8')
print(variant, len(steps), 'steps', sum(len(s['after']) for s in steps), 'assertions')
