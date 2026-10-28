"""Two legal R5/R6 routes for the same recruited original Magic Familiar.

The already-proven recruitment prefix is replayed without duplicate evidence.
Default is plan-only; --write is explicit. No asset, seed or state mutations.
"""
import argparse
import json
from pathlib import Path


def build(mode):
    steps = []
    rule = ('原魔宠实际供应招募色标及战术前库存维持；同实体红攻击5/蓝格挡7/'
            '白移动或影响5/绿治疗3，原地图、原敌及真实牌区消费')
    recording = False

    def click(path, label, expected=None, capture=False):
        checks = {'ui.truncated': '', **(expected or {})} if recording else {}
        steps.append(dict(buttonPath='Canvas/BattleTable/' + path, label=label,
            waitAfterSeconds=.3, skipScreenshot=not (capture and recording),
            after=[dict(key=k, expected=str(v), rule=rule) for k, v in checks.items()]))

    def outside(card, choice='base', expected=None):
        click('JourneyHand/@card=' + card, '实际可见原牌 ' + card)
        click('JourneyHand/Btn_OutsideChoice_' + choice, '原印刷模式 ' + choice)
        click('JourneyHand/Btn_PlayOutside', '正式原牌支付和效果', expected)

    def move(q, r, remaining):
        click(f'JourneyMap/Btn_Cell_{q}_{r}', '实际相邻原地形')
        click('JourneyMap/Btn_Move', '原地形费用实际消费',
              {'journey.position': f'{q},{r}', 'movement': remaining})

    def next_round(number):
        click('Btn_Announce', '实际空牌库声明轮末', {'deck.count': 0})
        click('Btn_EndRound', '同玩家正式回收原牌', {'journey.round': number})
        click('Btn_PrepareRound', '真实补牌、供应轮换与部队重整')

    def unit(ability, option=None, expected=None, capture=False):
        click('JourneyHand/Btn_OwnedUnits', '同一实际招募魔宠')
        click('OwnedUnits/Btn_Unit_0', '原魔宠实体及卡面')
        click('OwnedUnits/Btn_Ability_' + str(ability), '原印刷能力')
        if option is not None:
            click('OwnedUnits/Btn_Option_' + str(option), '移动/影响实际二选一')
        click('OwnedUnits/Btn_Use', '正式发动并耗竭，不再支付调谐费', expected, capture)
        click('OwnedUnits/Btn_Close', '保留部队实体状态')

    def challenge(q, r):
        click('JourneyMap/Btn_Cell_' + str(q) + '_' + str(r), '实际尚存肆虐敌人')
        click('JourneyMap/Btn_Challenge', '挑战同一地图标记',
              {'journey.samePlayer': 'True', 'movement': 0})

    def sideways(card, total):
        click('Hand/Btn_Card_' + card + '_0', '实际原手牌横置 ' + card)
        click('Inspector/Btn_Sideways', '原近战横置1', {'melee': total})

    white = mode == 'white-green'
    (click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp', '原魔宠四色实际消费者合法构筑'))
    click('CampShade/Camp/Btn_Loadout', '已有HornGold原资源起点')
    for _ in range(2): click('CampShade/Picker/Btn_NextPage', '下一页')
    click('CampShade/Picker/Btn_Select_39', '原红蓝晶各一，无起始部队')
    click('CampShade/Camp/Btn_MapMode', '原A/1/2地图')
    click('CampShade/Camp/Btn_ChooseCards', '明确原牌自定构筑，非标准16牌')
    p = 'CampShade/DeedPicker/'
    click(p + 'Btn_ClearDeeds', '清除待选构筑')
    for card in ['000', '008']:
        click(p + 'Btn_ChooseDeed_basic_card_' + card, '原行进/承诺')
    click(p + 'Btn_DeedNext', '基本第二页')
    click(p + 'Btn_ChooseDeed_basic_card_014', '原耐力')
    click(p + 'Btn_DeedKind_1', '高级原卡')
    click(p + 'Btn_DeedNext', '高级第二页')
    if white:
        for card in ['012', '015']:
            click(p + 'Btn_ChooseDeed_advanced_card_' + card, '原鲜血仪式/再生')
    click(p + 'Btn_ChooseDeed_advanced_card_018', '原外交')
    if not white:
        for _ in range(2): click(p + 'Btn_DeedNext', '高级下一页')
        click(p + 'Btn_ChooseDeed_advanced_card_039', '原山川逸闻')
    click(p + 'Btn_DoneDeeds', '保存六张或五张实际原牌')
    click('CampShade/Camp/Btn_SmallDeckHeroBuild', '合法等级和手牌上限')
    click('CampShade/HeroBuild/Btn_Hero_Tovak', '原托瓦克')
    click('CampShade/HeroBuild/Btn_Level_6', '等级6名望35、护甲3、手限6，未选择技能')
    click('CampShade/HeroBuild/Btn_Begin', '连续原玩家，无直接初选魔宠')
    click('Tactic_day_1/Btn_Tactic_day_1', '正式首日战术')
    click('Btn_BeginTurn', '正式第一回合')
    click('Btn_JourneyHand', '实际原牌移动')
    outside('basic_card_000')
    outside('basic_card_014')
    if white:
        for card in ['advanced_card_012', 'advanced_card_015', 'advanced_card_018']:
            click('JourneyHand/@card=' + card, '原牌实际横置移动')
            click('JourneyHand/Btn_SidewaysMove', '横置移动1')
    else:
        outside('advanced_card_039')
    click('JourneyHand/Btn_CloseHand', '实际移动7')
    click('Btn_JourneyMap', '原林地路线')
    for q, r, left in [(0, -1, 5), (1, -2, 3), (1, -3, 0)]: move(q, r, left)
    click('JourneyMap/Btn_CloseMap', '实际林地')
    click('Btn_EndTurn', '正式移动回合结束')
    for number in range(2, 6):
        next_round(number)
        part = 'night' if number % 2 == 0 else 'day'
        click('Tactic_' + part + '_1/Btn_Tactic_' + part + '_1', '正式昼夜战术')
    click('Btn_BeginTurn', '第五轮林地开始真实金标记')
    click('Btn_JourneyTools', '实际林地金色转换')
    click('JourneyTools/Btn_SourceColor_White', '选择白色')
    click('JourneyTools/Btn_UseGold', '金标记转白')
    click('JourneyTools/Btn_CloseTools', '返回真实手牌')
    click('Btn_JourneyHand', '实际第五轮手牌')
    prefix_end = len(steps)
    recording = True
    if white:
        outside('advanced_card_012', 'strong')
        for row, color in enumerate(['Blue', 'Green', 'White']):
            click(f'DeedInputs/Btn_InputColor_{row}_{color}', '本次独立基本色')
        click('DeedInputs/Btn_InputCrystal_Green', '本次绿魔力转库存，为下轮维持保存')
        click('DeedInputs/Btn_ConfirmInputs', '付红晶、真实手伤，保留白标与绿晶',
              {'red': 0, 'blue': 1, 'green': 1, 'tokens.white': 2,
               'tokens.green': 0, 'tokens.blue': 1, 'wounds': 1})
    outside('basic_card_008', 'strong', {'influence': 4})
    outside('advanced_card_018', 'base', {'influence': 6})
    click('JourneyHand/Btn_CloseHand', '实际林地非交涉招募')
    click('Btn_JourneyMap', '原当前位置')
    click('JourneyMap/Btn_Cell_1_-3', '当前实际林地')
    click('JourneyMap/Btn_CurrentGlade', '正式林地招募入口')
    click('JourneyInteraction/Btn_Offer_army_2_004', '真实R5供应魔宠')
    color = 'White' if white else 'Red'
    click('JourneyInteraction/Btn_RecruitMana_' + color, '额外实际魔力与留在部队上的色标')
    click('JourneyInteraction/Btn_Recruit', '真实影响6和所选魔力1取得原供应',
          {'influence': 0, 'red': 0, 'blue': 1, 'units.count': 1,
           'units.ready': 'True', 'units.attunements': 'army_2_004:' + color + ':False',
           'hero.level': 6, 'fame': 35, 'reputation': 0, 'tokens.white': 0})
    click('JourneyInteraction/Btn_CloseInteraction', '保留真实招募实体')
    click('Btn_EndTurn', '正式结束招募回合')
    if white:
        click('SiteEnd/Btn_Skip', '保留仪式真实伤牌供后续治疗', {'wounds': 1})
    click('Btn_BeginTurn', '同轮同实体后续消费者',
          {'journey.completedTurns': 2, 'units.ready': 'True', 'journey.samePlayer': 'True',
           'units.count': 1, 'units.attunements': 'army_2_004:' + color + ':False',
           'red': 0, 'blue': 1, 'hero.level': 6, 'fame': 35})
    if not white:
        # Existing red recruitment proof is reused. Only the current consumer
        # starting state is checked, not the full previously passed batch.
        prefix_end = len(steps) - 1
        for step in steps[:prefix_end]:
            step['after'] = []
            step['skipScreenshot'] = True

    if not white:
        click('Btn_JourneyMap', '仍在实际林地，邻接哨兵')
        challenge(1, -4)
        for _ in range(3): click('Inspector/Btn_Next', '原防御结算，真实英雄承伤')
        click('Btn_Roster', '实际原魔宠卡面')
        click('Units/Btn_Unit_0', '同一红色魔宠')
        click('Inspector/Btn_UnitAbility_0', '红标原攻击3+2，无额外魔力费',
              {'melee': 5, 'units.ready': 'False', 'wounds': 2,
               'red': 0, 'blue': 1, 'units.attunements': 'army_2_004:Red:False'}, True)
        click('Enemy_0/Btn_Target', '原护甲3哨兵')
        click('Inspector/Btn_Resolve', '红魔宠攻击真实击杀', {'kills': 1, 'fame': 37})
        click('Inspector/Btn_Next', '实际地图移除被击败标记，奖励声誉',
              {'reputation': 1, 'map.rampaging': '3:-3=monster_007'})
        click('Btn_JourneyHand', '正式回合至少出/弃一张原牌')
        click('JourneyHand/@card=basic_card_000', '实际未用行进')
        click('JourneyHand/Btn_DiscardAtEnd', '选择正式回合末弃牌，非空过')
        click('JourneyHand/Btn_CloseHand', '保留同一战果')
        click('Inspector/Btn_EndTurn', '正式结束实际战斗回合')
        click('SiteEnd/Btn_Wound_0', '林地原可选去除一张真实战伤')
        click('SiteEnd/Btn_Confirm', '仍保留另一张旧伤跨轮', {'wounds': 1})
        next_round(6)
        click('Btn_Upkeep_Blue', '战术前真实蓝库存1维持同魔宠',
              {'blue': 0, 'units.ready': 'True', 'units.attunements': 'army_2_004:Blue:False',
               'wounds': 1, 'hand': 6, 'deck.count': 0})
        click('Tactic_night_1/Btn_Tactic_night_1', '维持之后原黑夜战术')
        click('Btn_BeginTurn', '同魔宠与同伤牌实际下一轮')
        click('Btn_JourneyHand', '实际原行进')
        outside('basic_card_000', 'base', {'movement': 2})
        click('JourneyHand/Btn_CloseHand', '实际平原移动')
        click('Btn_JourneyMap', '首次进入狼骑相邻圈')
        move(2, -3, 0)
        challenge(3, -3)
        click('Inspector/Btn_Next', '真实格挡阶段')
        click('Btn_Roster', '同魔宠已由正式新轮重整')
        click('Units/Btn_Unit_0', '同一蓝标实体')
        click('Inspector/Btn_UnitAbility_1', '蓝标原格挡4+3，不再次收费',
              {'block': 7, 'units.ready': 'False', 'blue': 0, 'wounds': 1}, True)
        click('Enemy_0/Btn_Target', '原狼骑迅捷3需格挡6')
        click('Inspector/Btn_Resolve', '真实完整格挡迅捷攻击')
        click('Inspector/Btn_Next', '正式承伤阶段')
        click('Inspector/Btn_Next', '真实防御结算无新增伤',
              {'wounds': 1, 'attacks.successfullyBlocked': '0:0'})
        click('Btn_Roster', '四张实际原牌完成原敌')
        for total, card in enumerate(['basic_card_008', 'basic_card_014', 'advanced_card_018', 'advanced_card_039'], 1):
            sideways(card, total)
        click('Enemy_0/Btn_Target', '同一甲4狼骑')
        click('Inspector/Btn_Resolve', '横置原牌攻击4实际击杀', {'kills': 1, 'fame': 40})
        click('Inspector/Btn_Next', '实际原狼骑移除与声誉奖励',
              {'reputation': 2, 'map.rampaging': ''}, True)
        click('Inspector/Btn_EndTurn', '正式双战跨轮保留同魔宠与旧伤',
              {'journey.completedBattles': 2, 'journey.completedTurns': 4, 'wounds': 1,
               'units.ready': 'False', 'units.wounds': 0, 'blue': 0, 'red': 0,
               'journey.samePlayer': 'True'})
    else:
        click('Btn_JourneyHand', '白标同魔宠实际移动')
        unit(2, 0, {'movement': 5, 'units.ready': 'False', 'wounds': 1}, True)
        click('JourneyHand/Btn_CloseHand', '移动到实际村庄')
        click('Btn_JourneyMap', '原邻格平原')
        move(2, -3, 3)
        click('JourneyMap/Btn_CloseMap', '实际村庄后恢复同部队')
        click('Btn_JourneyHand', '原再生重整不自动治疗英雄')
        click('JourneyHand/@card=advanced_card_015', '原再生')
        click('JourneyHand/Btn_OutsideChoice_base', '原治疗1及重整等级1/2')
        click('JourneyHand/Btn_RecoveryUnit_0', '同一等级2白标魔宠')
        click('JourneyHand/Btn_PlayOutside', '无绿色费重整，不改变调谐标记',
              {'units.ready': 'True', 'healing': 1, 'wounds': 1, 'green': 1})
        unit(2, 1, {'influence': 5, 'movement': 3, 'units.ready': 'False'}, True)
        click('JourneyHand/Btn_CloseHand', '实际村庄交涉')
        click('Btn_JourneyInteraction', '原村庄消费者')
        click('JourneyInteraction/Btn_BeginInteraction', '原声誉0，保留影响5', {'influence': 5})
        click('JourneyInteraction/Btn_BuyHealing', '实际消费白标影响中的3购买治疗',
              {'influence': 2, 'healing': 2})
        click('JourneyInteraction/Btn_CloseInteraction', '消费真实仪式伤')
        click('Btn_JourneyHand', '原手牌伤牌')
        click('JourneyHand/Btn_HealHero', '治疗真实同一仪式伤', {'wounds': 0, 'healing': 1})
        click('JourneyHand/Btn_CloseHand', '保留实际原牌区')
        click('Btn_EndTurn', '正式清理回合临时资源',
              {'movement': 0, 'influence': 0, 'healing': 0, 'green': 1})
        next_round(6)
        click('Btn_Upkeep_Green', '支付仪式实际保存的绿晶维持同魔宠',
              {'green': 0, 'units.ready': 'True', 'units.attunements': 'army_2_004:Green:False',
               'hand': 6, 'deck.count': 0, 'wounds': 0})
        click('Tactic_night_1/Btn_Tactic_night_1', '正式夜战术')
        click('Btn_BeginTurn', '同玩家实际第六轮')
        click('Btn_JourneyHand', '原仪式与原移动费用')
        outside('advanced_card_012', 'base')
        click('DeedInputs/Btn_InputColor_0_Green', '基础真实红晶和绿标选择')
        click('DeedInputs/Btn_ConfirmInputs', '真实新伤1、红晶1、绿标1',
              {'red': 1, 'green': 0, 'tokens.green': 1, 'wounds': 1})
        outside('basic_card_000', 'strong', {'movement': 4, 'tokens.green': 0})
        outside('basic_card_014', 'base', {'movement': 6})
        click('JourneyHand/Btn_CloseHand', '原夜间森林费用')
        click('Btn_JourneyMap', '离开狼骑圈，不触发持续相邻')
        move(1, -3, 1)
        challenge(1, -4)
        for _ in range(3): click('Inspector/Btn_Next', '真实哨兵攻击4由等级6护甲3承伤')
        for total, card in enumerate(['basic_card_008', 'advanced_card_018', 'advanced_card_015'], 1):
            sideways(card, total)
        click('Enemy_0/Btn_Target', '原甲3哨兵')
        click('Inspector/Btn_Resolve', '原牌横置3实际击杀，仪式1加战伤2',
              {'kills': 1, 'fame': 37, 'wounds': 3})
        click('Inspector/Btn_Next', '正式战斗结束，保留同魔宠与三实际伤牌')
        click('Btn_JourneyHand', '实际战后开放正式部队治疗')
        unit(3, None, {'healing': 3, 'units.ready': 'False', 'wounds': 3,
                       'units.attunements': 'army_2_004:Green:False'}, True)
        for remaining in [2, 1, 0]:
            click('JourneyHand/Btn_HealHero', '逐张消费真实英雄伤牌',
                  {'wounds': remaining, 'healing': remaining}, remaining == 0)
        click('JourneyHand/Btn_CloseHand', '保留同一正式战果')
        click('Inspector/Btn_EndTurn', '同一魔宠跨轮两色与实际消费者结束',
              {'journey.completedTurns': 4, 'journey.completedBattles': 1,
               'wounds': 0, 'healing': 0, 'units.ready': 'False', 'units.wounds': 0,
               'green': 0, 'blue': 1, 'red': 1, 'journey.samePlayer': 'True'})
    return steps, prefix_end


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('mode', choices=['red-blue', 'white-green'])
    parser.add_argument('--write', action='store_true')
    parser.add_argument('--from', dest='start', type=int, default=0)
    parser.add_argument('--output', default='AutomationConfigs/pve_battle.json')
    args = parser.parse_args()
    steps, prefix = build(args.mode)
    if not 0 <= args.start <= len(steps): parser.error('--from outside route')
    for step in steps[:args.start]:
        step['after'] = []
        step['skipScreenshot'] = True
    if args.write:
        Path(args.output).write_text(json.dumps(dict(
            scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity', captureInitialView=False,
            initialDelaySeconds=2, defaultWaitAfterSeconds=.3, maxRunSeconds=600,
            useRunSubfolder=False, steps=steps), ensure_ascii=False, indent=2), encoding='utf-8')
    print(args.mode, len(steps), 'steps;', sum(len(s['after']) for s in steps), 'assertions;',
          sum(not s['skipScreenshot'] for s in steps), 'captures; replay prefix', prefix,
          ';', 'written' if args.write else 'plan only')


if __name__ == '__main__': main()
