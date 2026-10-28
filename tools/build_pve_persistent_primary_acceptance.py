"""Incremental original Puppet block / Duel actual unit intervention scenes.

Reuses Commoners and visible legal level-4 build. No fixture or seed mutation.
Generation writes only the normal acceptance config when explicitly executed.
"""
import json
import sys
from pathlib import Path

mode = sys.argv[1]
assert mode in ('puppet', 'duel')
steps = []
rule = '原自有傀儡实际保存标记跨回合消费格挡；或决斗同敌实际部队介入取消额外名望'


def click(path, label, expected=None, capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/' + path, label=label,
                      waitAfterSeconds=.3, skipScreenshot=not capture,
                      after=[dict(key=k, expected=str(v), rule=rule)
                             for k, v in {'ui.truncated': '', **(expected or {})}.items()]))


def play(card, choice, expected=None):
    click('Hand/Btn_Card_basic_card_' + card + '_0', '实际原牌 ' + card)
    click('Inspector/Btn_Choice_' + choice, '原印刷选择')
    click('Inspector/Btn_Play', '真实费用及原贡献', expected)


def skill():
    click('Btn_Skills', '自有原技能')
    click('Skills/Btn_Skill_0', '本人合法技能')


def target():
    click('Enemy_0/Btn_Target', '当前实际原敌')


def endbattle():
    click('Inspector/Btn_Next', '正式结束战斗')
    click('Inspector/Btn_EndTurn', '正式结束实际回合')
    click('Btn_BeginTurn', '同一玩家及牌区下回合', {'journey.samePlayer': 'True'})


def move():
    click('Btn_JourneyHand', '实际原手牌')
    click('JourneyHand/@card=basic_card_000', '实际原行进')
    click('JourneyHand/Btn_OutsideChoice_base', '原移动2')
    click('JourneyHand/Btn_PlayOutside', '产生实际移动', {'movement': 2})
    click('JourneyHand/Btn_CloseHand', '返回实际地图')
    click('Btn_JourneyMap', '原费用地图')
    click('JourneyMap/Btn_Hex_plains', '相邻村庄')
    click('JourneyMap/Btn_Move', '实际支付平原2', {'movement': 0, 'journey.position': '0,1'})
    click('JourneyMap/Btn_CloseMap', '保留位置与同一玩家')


(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp', '合法原英雄构筑'))
click('CampShade/Camp/Btn_Loadout', '既有双部队起点')
for _ in range(3):
    click('CampShade/Picker/Btn_NextPage', '下一页')
click('CampShade/Picker/Btn_Select_60', '既有农民与暴徒')
click('CampShade/Camp/Btn_ChooseUnits', '合法两个指挥槽')
click('CampShade/UnitPicker/Btn_Unit_army_2_011', '移除暴徒')
click('CampShade/UnitPicker/Btn_Unit_army_2_005', '选择原北地僧侣')
click('CampShade/UnitPicker/Btn_Back', '保留农民与僧侣')
click('CampShade/Camp/Btn_ChooseCards', '五张原牌')
p = 'CampShade/DeedPicker/'
click(p + 'Btn_ClearDeeds', '清空旧待选')
for card in ['000', '008']:
    click(p + 'Btn_ChooseDeed_basic_card_' + card, '原基本牌')
click(p + 'Btn_DeedNext', '基础第二页')
for card in ['016', '021', '022']:
    click(p + 'Btn_ChooseDeed_basic_card_' + card, '原基本牌')
click(p + 'Btn_DoneDeeds', '确认五原牌')
click('CampShade/Camp/Btn_SmallDeckHeroBuild', '合法等级与自身技能')
hero = 'Krang' if mode == 'puppet' else 'Wolfhawk'
click('CampShade/HeroBuild/Btn_Hero_' + hero, '原英雄')
click('CampShade/HeroBuild/Btn_Level_4', '合法等级4两个普通指挥槽')
click('CampShade/HeroBuild/Btn_Skill_' + ('6' if mode == 'puppet' else '7'), '自身原技能')
click('CampShade/HeroBuild/Btn_Begin', '正式连续旅程',
      {'hero.id': hero, 'hero.level': 4, 'hero.skills': 1, 'fame': 15, 'units.count': 2})
click('Tactic_day_1/Btn_Tactic_day_1', '原白昼战术')
click('Btn_BeginTurn', '正式首回合')
if mode == 'puppet':
    move()
click('Btn_JourneyEncounter_0', '实际单哨卫032')
click('Inspector/Btn_Next', '原格挡阶段')
if mode == 'duel':
    skill()
    target()
    click('Inspector/Btn_UseSkill', '决斗绑定原敌与格挡1', {'duel.target': 'monster_032', 'block': 1})
    click('Btn_Skills', '返回原牌')
    play('016', 'block', {'block': 3})
    click('Btn_Roster', '实际原部队')
    click('Units/Btn_Unit_0', '原农民')
    click('Inspector/Btn_UnitAbility_0', '农民格挡2并耗竭', {'block': 5, 'units.ready': 'False,True'})
    target()
    click('Inspector/Btn_Resolve', '实际单位贡献参与同敌完整格挡',
          {'block': 0}, True)
click('Inspector/Btn_Next', '承伤阶段')
click('Inspector/Btn_Next', '真实承伤后近战', {'wounds': 2} if mode == 'puppet' else {'wounds': 0, 'duel.unitAffected': 'True'})
if mode == 'duel':
    click('Btn_Roster', '返回原手牌')
    play('021', 'attack', {'melee': 3})
    target()
    click('Inspector/Btn_Resolve', '单位已作用该敌，仅原名望2，没有决斗额外1',
          {'kills': 1, 'fame': 17, 'duel.unitAffected': 'True'}, True)
    endbattle()
    move()
    click('Btn_EndTurn', '真实下一回合移动后正式结束')
else:
    click('Btn_Roster', '原部队实际消费者')
    click('Units/Btn_Unit_1', '本人北地僧侣')
    click('Inspector/Btn_UnitAbility_0', '僧侣原攻击3并耗竭', {'melee': 3, 'units.ready': 'True,False'})
    target()
    click('Inspector/Btn_Resolve', '击败实际护甲3哨卫', {'kills': 1, 'fame': 17, 'puppet.candidates': 'monster_032'})
    skill()
    click('Inspector/Btn_UseSkill', '选择本人真实战果')
    click('EnemyTokenSkill/Btn_Enemy_0', '原哨卫标记')
    click('EnemyTokenSkill/Btn_Confirm', '保存实际标记至以后回合', {'puppet.stored': 'monster_032'}, True)
    endbattle()
    click('Btn_JourneyEncounter_4', '另一实际狼骑兵007')
    click('Inspector/Btn_Next', '迅捷格挡阶段')
    skill()
    click('Inspector/Btn_UseSkill', '原持久库存')
    click('EnemyTokenSkill/Btn_Mode_2', '明确选择原格挡用途')
    click('EnemyTokenSkill/Btn_Enemy_0', '上回合实际保存的同一标记')
    click('EnemyTokenSkill/Btn_Confirm', '哨卫护甲3向上取半产生物理格挡2',
          {'puppet.stored': '', 'block': 2}, True)
    click('Btn_Skills', '返回实际手牌')
    play('016', 'strong', {'block': 7, 'blue': 0})
    target()
    click('Inspector/Btn_Resolve', '傀儡加真实蓝费原决心完整格挡迅捷6', {'block': 0}, True)
    click('Inspector/Btn_Next', '承伤阶段')
    click('Inspector/Btn_Next', '格挡成功无新增伤牌', {'wounds': 2})
    play('021', 'attack', {'melee': 2})
    for card in ['008', '022']:
        click('Hand/Btn_Card_basic_card_' + card + '_0', '保留真实原手牌')
        click('Inspector/Btn_Sideways', '正式横置攻击1')
    target()
    click('Inspector/Btn_Resolve', '真实攻击4击败原狼骑兵', {'kills': 1, 'fame': 20}, True)
    endbattle()

if '--from' in sys.argv:
    for step in steps[:int(sys.argv[sys.argv.index('--from') + 1])]:
        step['after'] = []
        step['skipScreenshot'] = True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(
    scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity', captureInitialView=False,
    initialDelaySeconds=2, defaultWaitAfterSeconds=.3, maxRunSeconds=300,
    useRunSubfolder=False, steps=steps), ensure_ascii=False, indent=2), encoding='utf-8')
print(mode, len(steps), 'steps', sum(len(s['after']) for s in steps), 'checks')
