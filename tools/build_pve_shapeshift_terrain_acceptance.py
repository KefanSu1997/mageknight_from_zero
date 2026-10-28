"""Same physical Druidic Paths across two formal rounds: base block/strong attack.

Five original cards, legal Braevalar level 4 and one owned skill. The card is
recovered only by formal EndRound. Terrain discounts are observed, never used
to fabricate movement after combat. Invoke to emit configuration; --from N
keeps replay prefix without repeating its evidence.
"""
import json
import sys
from pathlib import Path

steps = []
RULE = '原变换身形保留德鲁伊之道真实格/地形附效与蓝费；正式格挡攻击消费，同实体经真实轮末洗回'


def click(path, label, expected=None, capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/' + path, label=label,
                      waitAfterSeconds=.3, skipScreenshot=not capture,
                      after=[dict(key=k, expected=str(v), rule=RULE)
                             for k, v in {'ui.truncated': '', **(expected or {})}.items()]))


def play(card, choice, expected=None):
    click('Hand/Btn_Card_basic_card_' + card + '_0', '选择实际原牌 ' + card)
    click('Inspector/Btn_Choice_' + choice, '原印刷选择 ' + choice)
    click('Inspector/Btn_Play', '正式出牌', expected)


def shape(strong=False):
    click('Btn_Skills', '本人原技能')
    click('Skills/Btn_Skill_0', '本人变换身形')
    click('Inspector/Btn_UseSkill', '实际基本牌选择')
    click('Shapeshift/@card=basic_card_018', '同一原德鲁伊之道实体')
    click('Shapeshift/Btn_Strong' if strong else 'Shapeshift/Btn_Basic',
          '原蓝色强化' if strong else '原基础效果')
    click('Shapeshift/Btn_Output_0', '原固定移动项转换')


(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp', '原变换地形输入连续场景'))
click('CampShade/Camp/Btn_Loadout', '既有构筑起点')
for _ in range(2):
    click('CampShade/Picker/Btn_NextPage', '下一页')
click('CampShade/Picker/Btn_Select_39', 'HornGold原红蓝库存各一')
click('CampShade/Camp/Btn_ChooseCards', '五张原基本牌')
p = 'CampShade/DeedPicker/'
click(p + 'Btn_ClearDeeds', '清空待选构筑')
for card in ('000', '007'):
    click(p + 'Btn_ChooseDeed_basic_card_' + card, '原基本牌 ' + card)
click(p + 'Btn_DeedNext', '基本第二页')
for card in ('016', '018', '021'):
    click(p + 'Btn_ChooseDeed_basic_card_' + card, '原基本牌 ' + card)
click(p + 'Btn_DoneDeeds', '保存五张原牌')
click('CampShade/Camp/Btn_SmallDeckHeroBuild', '合法原英雄技能构筑')
click('CampShade/HeroBuild/Btn_Hero_Braevalar', '原布拉沃')
click('CampShade/HeroBuild/Btn_Level_4', '合法等级4指挥槽与护甲')
click('CampShade/HeroBuild/Btn_Skill_6', '原自有变换身形')
click('CampShade/HeroBuild/Btn_Begin', '同一玩家开局',
      {'hero.id': 'Braevalar', 'hero.skills': 1, 'hero.level': 4})
click('Tactic_day_1/Btn_Tactic_day_1', '正式白昼战术')
click('Btn_BeginTurn', '第一真实回合')
click('Btn_JourneyEncounter_0', '真实哨卫032')
click('Inspector/Btn_Next', '正式格挡阶段')
shape()
# The existing custom route sorted by q/r has camp (0,0) on its first six-cell page.
click('Shapeshift/Btn_Hex_0_0', '选择实际已揭示营地格，不猜地形', capture=True)
click('Shapeshift/Btn_Confirm', '原移动2转换物理格挡2并保留单格减费',
      {'power.blocks': 'Physical:2', 'movement': 0, 'blue': 1,
       'skills.used': 'True', 'travel.hexReductions': '0:0=1',
       'travel.terrainReductions': ''}, True)
click('Btn_Skills', '返回原手牌')
play('016', 'block', {'block': 4})
click('Enemy_0/Btn_Target', '实际攻击4的原敌人')
click('Inspector/Btn_Resolve', '转换2加原格挡2，实际完整格挡4', {'block': 0}, True)
click('Inspector/Btn_Next', '正式承伤阶段')
click('Inspector/Btn_Next', '进入近战，无新增伤牌', {'wounds': 0})
play('021', 'attack', {'melee': 2})
click('Hand/Btn_Card_basic_card_000_0', '真实原行进')
click('Inspector/Btn_Sideways', '原横置攻击1', {'melee': 3})
click('Enemy_0/Btn_Target', '原哨卫护甲3')
click('Inspector/Btn_Resolve', '正式攻击3击杀', {'kills': 1}, True)
click('Inspector/Btn_Next', '正式战斗结束，禁止追加移动',
      {'movement': 0, 'travel.hexReductions': '0:0=1'})
click('Inspector/Btn_EndTurn', '结束第一回合并清回合减费',
      {'journey.completedTurns': 1, 'journey.completedBattles': 1, 'wounds': 0,
       'travel.hexReductions': '', 'travel.terrainReductions': ''}, True)
click('Btn_Announce', '真实五牌库为空，声明轮次结束', {'deck.count': 0})
click('Btn_EndRound', '原轮末将同一德鲁伊之道洗回', {'journey.round': 2})
click('Btn_PrepareRound', '正式新轮补齐五原牌', {'hand': 5, 'deck.count': 0})
click('Tactic_night_1/Btn_Tactic_night_1', '正式夜间战术')
click('Btn_BeginTurn', '第二真实回合')
click('Btn_JourneyEncounter_4', '真实狼骑兵007')
for _ in range(3):
    click('Inspector/Btn_Next', '原阶段推进，真实承伤后近战')
shape(True)
click('Shapeshift/Btn_Terrain_Forest', '选择实际森林地形的强化附效', capture=True)
click('Shapeshift/Btn_Confirm', '支付原蓝晶，固定移动4转攻击4保留森林减费',
      {'power.attacks': 'Melee:Physical:4', 'melee': 4, 'movement': 0,
       'blue': 0, 'skills.used': 'True', 'travel.hexReductions': '',
       'travel.terrainReductions': 'Forest=1'}, True)
click('Btn_Skills', '返回本场实际敌人')
click('Enemy_0/Btn_Target', '原狼骑兵护甲4')
click('Inspector/Btn_Resolve', '真实转换攻击4完整击杀', {'kills': 1, 'melee': 0}, True)
click('Inspector/Btn_Next', '正式战斗结束，不伪造战后移动',
      {'movement': 0, 'travel.terrainReductions': 'Forest=1'})
click('Inspector/Btn_EndTurn', '第二回合结束清森林减费',
      {'journey.completedTurns': 2, 'journey.completedBattles': 2,
       'journey.samePlayer': 'True', 'hero.skills': 1, 'blue': 0,
       'travel.hexReductions': '', 'travel.terrainReductions': ''}, True)

if '--from' in sys.argv:
    for step in steps[:int(sys.argv[sys.argv.index('--from') + 1])]:
        step['after'] = []
        step['skipScreenshot'] = True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(
    scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity', captureInitialView=False,
    initialDelaySeconds=2, defaultWaitAfterSeconds=.3, maxRunSeconds=360,
    useRunSubfolder=False, steps=steps), ensure_ascii=False, indent=2), encoding='utf8')
print(len(steps), 'steps', sum(len(step['after']) for step in steps), 'assertions')
