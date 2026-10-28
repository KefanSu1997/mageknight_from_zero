"""Original A/1/2 sites: battle wounds, glade turns, mine crystal and actual payment.

Seven original cards in selection order: basic014, basic021, advanced011,
advanced013, advanced032, advanced037, advanced039. No state/seed overrides.
Invoke only when ready to replace the automation configuration; --from preserves
the actual replay prefix while recording new assertions/screenshots from that step.
"""
import json
import sys
from pathlib import Path

steps = []
RULE = '原地点按真实回合位置结算；林地开始金魔力、末去真实伤牌，矿山结束绿晶及次回合原费用消费；同玩家牌区与肆虐原敌保留'


def click(path, label, expect=None, capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/' + path, label=label,
                      waitAfterSeconds=.3, skipScreenshot=not capture,
                      after=[dict(key=k, expected=str(v), rule=RULE)
                             for k, v in {'ui.truncated': '', **(expect or {})}.items()]))


def hand():
    click('Btn_JourneyHand', '查看同玩家实际手牌')


def closehand():
    click('JourneyHand/Btn_CloseHand', '返回当前旅程')


def play(index, choice, expect=None):
    click('JourneyHand/Btn_Hand_' + str(index), '选择实际原牌实例')
    click('JourneyHand/Btn_OutsideChoice_' + choice, '原印刷选择 ' + choice)
    click('JourneyHand/Btn_PlayOutside', '正式出牌及实际费用', expect)


def move(q, r, remaining, label):
    click(f'JourneyMap/Btn_Cell_{q}_{r}', label)
    click('JourneyMap/Btn_Move', '支付真实移动费用',
          {'journey.position': f'{q},{r}', 'movement': remaining})


(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp', '原地点四回合连续构筑'))
click('CampShade/Camp/Btn_Loadout', '已有可重放原资源起点')
for _ in range(2):
    click('CampShade/Picker/Btn_NextPage', '下一页')
click('CampShade/Picker/Btn_Select_39', 'HornGold实际红蓝各一晶')
click('CampShade/Camp/Btn_MapMode', '原A及乡村1和2开局')
click('CampShade/Camp/Btn_ChooseCards', '选择七张原卡')
p = 'CampShade/DeedPicker/'
click(p + 'Btn_ClearDeeds', '清空待选构筑')
click(p + 'Btn_DeedNext', '基本行动第二页')
for card in ('014', '021'):
    click(p + 'Btn_ChooseDeed_basic_card_' + card, '原基本卡 ' + card)
click(p + 'Btn_DeedKind_1', '高级行动第一页')
click(p + 'Btn_ChooseDeed_advanced_card_011', '原迷途')
click(p + 'Btn_DeedNext', '高级行动第二页')
click(p + 'Btn_ChooseDeed_advanced_card_013', '原纯净魔法')
click(p + 'Btn_DeedNext', '高级行动第三页')
click(p + 'Btn_ChooseDeed_advanced_card_032', '原时光传送')
click(p + 'Btn_DeedNext', '高级行动第四页')
for card in ('037', '039'):
    click(p + 'Btn_ChooseDeed_advanced_card_' + card, '原高级卡 ' + card)
click(p + 'Btn_DoneDeeds', '保存七原牌选取顺序')
click('CampShade/Camp/Btn_OriginalHero', '原英雄身份')
click('CampShade/HeroStart/Btn_Hero_Tovak', '原托瓦克')
click('CampShade/HeroStart/Btn_ConfiguredHero', '合法等级1同玩家开局',
      {'map.originalTiles': 3, 'map.originalCells': 21, 'journey.position': '0,0'})
click('Tactic_day_1/Btn_Tactic_day_1', '原战术')
click('Btn_BeginTurn', '第一回合正式开始')
hand()
click('JourneyHand/Btn_NextHand', '第七张原山川逸闻')
play(6, 'base', {'movement': 3})  # Six remain; the hand page clamps back to zero.
play(0, 'strong', {'movement': 7, 'blue': 0})
closehand()
click('Btn_JourneyMap', '真实原地图路径')
move(0, -1, 5, '原平原2')
move(0, -2, 3, '原平原2')
move(0, -3, 0, '白昼森林3，邻实际兽人')
click('JourneyMap/Btn_Cell_1_-4', '实际兽人032所在地')
click('JourneyMap/Btn_Challenge', '原相邻挑战保留同玩家', {'journey.samePlayer': 'True'}, True)
for _ in range(3):
    click('Inspector/Btn_Next', '推进正式战斗阶段，真实承伤')
click('Hand/Btn_Card_basic_card_021_0', '原狂怒')
click('Inspector/Btn_Choice_strong', '原红费攻击4')
click('Inspector/Btn_Play', '真实付红晶并保留两伤', {'red': 0, 'wounds': 2, 'melee': 4})
click('Enemy_0/Btn_Target', '同一原兽人标记')
click('Inspector/Btn_Resolve', '实际攻击击杀护甲3', {'kills': 1, 'fame': 2}, True)
click('Inspector/Btn_Next', '原标记归供给并奖励声誉',
      {'reputation': 1, 'map.rampaging': '3:-3=monster_007'})
click('Inspector/Btn_EndTurn', '第一回合正式结束',
      {'journey.completedTurns': 1, 'wounds': 2, 'tokens.gold': 0})

# T2 hand: 011, 013, 032, 037, wound, wound. Portal opens the map directly.
click('Btn_BeginTurn', '第二回合原位置开始')
hand()
play(2, 'base', {'travel.portal': 'True', 'travel.range': 1})
click('JourneyMap/Btn_Cell_1_-3', '相邻真实魔法林地')
click('JourneyMap/Btn_Move', '原传送安全落点，进入不发魔力',
      {'journey.position': '1,-3', 'travel.portal': 'False', 'tokens.gold': 0, 'wounds': 2}, True)
click('JourneyMap/Btn_CloseMap', '返回已占行动的旅程')
click('Btn_EndTurn', '林地正式末结算选择')
click('SiteEnd/Btn_Wound_0', '选择一张真实手牌战伤', capture=True)
click('SiteEnd/Btn_Confirm', '林地去除一伤而非治疗',
      {'journey.completedTurns': 2, 'wounds': 1, 'site.discardWounds': 0, 'tokens.gold': 0}, True)

# T3 hand: 011, 013, 037, wound. Gold is a temporary token, not a source die.
click('Btn_BeginTurn', '第三回合从林地出发获得实际金魔力', {'tokens.gold': 1}, True)
click('Btn_JourneyTools', '原魔力操作')
click('JourneyTools/Btn_SourceColor_Green', '选择金魔力的实际绿色用途')
click('JourneyTools/Btn_UseGold', '实际一金转绿', {'tokens.gold': 0, 'tokens.green': 1})
click('JourneyTools/Btn_CloseTools', '返回本回合')
hand()
play(0, 'strong', {'movement': 4, 'tokens.green': 0})
play(1, 'base_move')
click('DeedInputs/Btn_InputMaterial_0', '不屈选择仅余实际手牌伤牌')
click('DeedInputs/Btn_ConfirmInputs', '移动2加真实弃伤1',
      {'movement': 7, 'wounds': 0, 'site.discardWounds': 1}, True)
closehand()
click('Btn_JourneyMap', '正式移动到原绿色矿山')
move(1, -2, 5, '原平原2')
move(2, -2, 3, '原平原2，首次邻近狼骑')
move(2, -1, 1, '原绿矿丘陵，迷途费用2；离开狼骑相邻圈')
click('JourneyMap/Btn_CloseMap', '真实矿山结束位置')
click('Btn_EndTurn', '正式回合末采得绿晶一次',
      {'journey.completedTurns': 3, 'green': 1, 'tokens.green': 0,
       'wounds': 0, 'site.discardWounds': 1, 'movement': 0}, True)

# T4 only Pure Magic remains; its move choice specifies the printed green fee.
click('Btn_BeginTurn', '第四回合同玩家从矿山开始', {'green': 1, 'tokens.gold': 0})
hand()
play(0, 'move')
click('DeedInputs/Btn_ConfirmInputs', '纯净魔法实际支付矿山绿晶产移动4',
      {'green': 0, 'movement': 4}, True)
closehand()
click('Btn_JourneyMap', '消费所得晶体对应的真实移动')
move(3, -1, 2, '原相邻平原2，保留弃牌伤牌')
click('JourneyMap/Btn_CloseMap', '结束在平原而非矿山')
click('Btn_EndTurn', '正式回合清余移动且不再获矿晶',
      {'journey.completedTurns': 4, 'journey.completedBattles': 1,
       'journey.samePlayer': 'True', 'green': 0, 'movement': 0,
       'site.discardWounds': 1, 'wounds': 0, 'map.rampaging': '3:-3=monster_007'}, True)

if '--from' in sys.argv:
    for step in steps[:int(sys.argv[sys.argv.index('--from') + 1])]:
        step['after'] = []
        step['skipScreenshot'] = True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(
    scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity', captureInitialView=False,
    initialDelaySeconds=2, defaultWaitAfterSeconds=.3, maxRunSeconds=420,
    useRunSubfolder=False, steps=steps), ensure_ascii=False, indent=2), encoding='utf8')
print(len(steps), 'steps', sum(len(step['after']) for step in steps), 'assertions')

