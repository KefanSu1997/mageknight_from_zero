"""Tovak's own Fearless Pain and Inspiration, with actual wounds and original cards.

Plan only by default. --write emits configuration; --from replays an accepted
prefix without repeating its assertions/screenshots. Does not alter fixtures.
Requires the new combined backend path to pass before Unity dispatch.
"""
import argparse
import json
from pathlib import Path


def build():
    steps = []
    rule = ('托瓦克原等级6及两自身技能；前战真实两伤保留至正式次回合，'
            '无惧痛苦弃第一张实际伤而非去除并抽原牌；激励抽原牌/蓝标实际支付，'
            '同玩家手牌牌库弃牌与原移动费用守恒')

    def click(path, label, expected=None, capture=False):
        steps.append(dict(buttonPath='Canvas/BattleTable/' + path, label=label,
            waitAfterSeconds=.3, skipScreenshot=not capture,
            after=[dict(key=k, expected=str(v), rule=rule)
                   for k, v in {'ui.truncated': '', **(expected or {})}.items()]))

    (click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp', '托瓦克自身两技能连续路线'))
    click('CampShade/Camp/Btn_Loadout', '复用既有HornGold种子0')
    for _ in range(2):
        click('CampShade/Picker/Btn_NextPage', '实际构筑列表下一页')
    click('CampShade/Picker/Btn_Select_39', '既有红蓝各一晶且无预配单位')
    click('CampShade/Camp/Btn_SmallDeckHeroBuild', '原英雄合法等级入口')
    click('CampShade/HeroBuild/Btn_Hero_Tovak', '原托瓦克自身技能池007')
    click('CampShade/HeroBuild/Btn_Level_6', '等级6名望35护甲3，最多三个技能')
    click('CampShade/HeroBuild/Btn_Skill_5', '原无惧痛苦，每回合一次战斗外')
    click('CampShade/HeroBuild/Btn_Skill_8', '原激励，每轮一次蓝色奖励')
    click('CampShade/HeroBuild/Btn_DeckSource', '托瓦克原16牌按既有种子正式洗混')
    click('CampShade/HeroBuild/Btn_Begin', '保留同一真实原英雄和牌库',
          {'hero.id': 'Tovak', 'hero.level': 6, 'hero.armor': 3,
           'hero.skills': 2, 'hero.deckTotal': 16, 'hand': 6, 'deck.count': 10,
           'hand.ids': 'basic_card_021,basic_card_014,basic_card_000,basic_card_007,basic_card_027,basic_card_009',
           'units.count': 0, 'red': 1, 'blue': 1})
    click('Tactic_day_1/Btn_Tactic_day_1', '正式白昼战术')
    click('Btn_BeginTurn', '本人正式第一回合')
    click('Btn_JourneyEncounter_0', '既有演练原032护甲3攻击4，不冒充原地图肆虐')
    for _ in range(3):
        click('Inspector/Btn_Next', '无格挡，正式推进真实攻击分配')
    click('Hand/Btn_Card_basic_card_021_0', '原初始狂怒，与后续抽回的另一张区别',
          {'wounds': 2, 'hand': 8})
    click('Inspector/Btn_Choice_attack', '狂怒原基础攻击2')
    click('Inspector/Btn_Play', '实际原狂怒进已打出区', {'melee': 2, 'hand': 7})
    click('Hand/Btn_Card_basic_card_000_0', '原行进不强化，保留蓝晶和耐力')
    click('Inspector/Btn_Sideways', '实际横置攻击1，合计3', {'melee': 3, 'hand': 6})
    click('Enemy_0/Btn_Target', '同一原032护甲3')
    click('Inspector/Btn_Resolve', '消费真实攻击3击杀，保留两张战伤',
          {'kills': 1, 'fame': 37, 'wounds': 2, 'blue': 1})
    click('Inspector/Btn_Next', '正式战斗结束')
    click('Inspector/Btn_EndTurn', '第一回合正式弃已打出牌，两伤实际跨回合',
          {'journey.completedTurns': 1, 'journey.completedBattles': 1,
           'hand': 6, 'deck.count': 10, 'journey.discardCount': 2,
           'wounds': 2, 'site.discardWounds': 0, 'fame': 37,
           'journey.samePlayer': 'True'}, True)

    click('Btn_BeginTurn', '正式次回合，同玩家实际旧伤仍在')
    click('Btn_JourneyHand', '现有原手牌与实际战伤')
    click('JourneyHand/Btn_Support', '原自身技能，不经过头环或临时授予')
    click('JourneySupport/Btn_OutsideSkill_0', '选择本人无惧痛苦',
          {'journey.outsideSkill': 'hero_skill_pool_007:05',
           'journey.outsideSkillUsed': 'False'})
    click('JourneySupport/Btn_UseOutsideSkill', '按正式默认弃第一张实际伤至弃牌堆并抽下一张原狂怒',
          {'hand': 6, 'deck.count': 9, 'wounds': 1, 'site.discardWounds': 1,
           'journey.discardCount': 3, 'journey.outsideSkillUsed': 'True',
           'hero.deckTotal': 18}, True)
    click('JourneySupport/Btn_OutsideSkill_1', '选择本人原激励；无惧痛苦不受激励互锁限制',
          {'journey.outsideSkill': 'hero_skill_pool_007:08',
           'journey.outsideSkillUsed': 'False'})
    click('JourneySupport/Btn_UseOutsideSkill', '实际抽两张原牌，并记录本轮次数和蓝标',
          {'hand': 8, 'deck.count': 7, 'tokens.blue': 1, 'blue': 1,
           'wounds': 1, 'site.discardWounds': 1,
           'journey.outsideSkillUsed': 'True'}, True)
    click('JourneySupport/Btn_BackSupport', '回到同一真实手牌，按原图ID选择')
    click('JourneyHand/@card=basic_card_014', '原耐力，保留的唯一蓝色原牌')
    click('JourneyHand/Btn_OutsideChoice_strong', '原蓝费强化移动4')
    click('JourneyHand/Btn_PlayOutside', '优先支付激励蓝标，库存蓝晶仍1',
          {'movement': 4, 'tokens.blue': 0, 'blue': 1, 'hand': 7}, True)
    click('JourneyHand/@card=basic_card_021', '无惧痛苦实际新抽的另一张狂怒')
    click('JourneyHand/Btn_SidewaysMove', '消费新抽狂怒，移动增为5',
          {'movement': 5, 'hand': 6, 'deck.count': 7})
    click('JourneyHand/@card=basic_card_002', '激励本次实际抽入的原凝结')
    click('JourneyHand/Btn_SidewaysMove', '消费激励新抽原牌，移动增为6',
          {'movement': 6, 'hand': 5, 'wounds': 1, 'site.discardWounds': 1})
    click('JourneyHand/Btn_CloseHand', '保留真实费用与伤牌，前往实际地图')
    click('Btn_JourneyMap', '已有演练地图，沿正式地形费用')
    click('JourneyMap/Btn_Hex_plains', '实际邻接平原村庄0,1')
    click('JourneyMap/Btn_Move', '真实支付平原移动2',
          {'movement': 4, 'journey.position': '0,1'})
    click('JourneyMap/Btn_CloseMap', '保持同一玩家实际位置')
    click('Btn_EndTurn', '两正式回合：弃伤未去除、两个新抽消费者及蓝费用守恒',
          {'journey.completedTurns': 2, 'journey.completedBattles': 1,
           'journey.samePlayer': 'True', 'hero.id': 'Tovak', 'hero.skills': 2,
           'hero.deckTotal': 18, 'hand': 6, 'deck.count': 6,
           'journey.discardCount': 6, 'wounds': 1, 'site.discardWounds': 1,
           'blue': 1, 'red': 1, 'tokens.blue': 0, 'fame': 37,
           'journey.position': '0,1'}, True)
    return steps


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write', action='store_true')
    parser.add_argument('--from', dest='start', type=int, default=0)
    parser.add_argument('--output', default='AutomationConfigs/pve_battle.json')
    args = parser.parse_args()
    steps = build()
    if not 0 <= args.start <= len(steps):
        parser.error('--from must be between 0 and the total step count')
    for step in steps[:args.start]:
        step['after'] = []
        step['skipScreenshot'] = True
    if args.write:
        Path(args.output).write_text(json.dumps(dict(
            scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',
            captureInitialView=False, initialDelaySeconds=2,
            defaultWaitAfterSeconds=.3, maxRunSeconds=300,
            useRunSubfolder=False, steps=steps), ensure_ascii=False, indent=2),
            encoding='utf-8')
    print(len(steps), 'operations;', len(steps)-args.start, 'evidence steps;',
          sum(len(s['after']) for s in steps), 'assertions;',
          sum(not s['skipScreenshot'] for s in steps), 'screenshots;',
          'written' if args.write else 'plan only; backend gate before scene')


if __name__ == '__main__':
    main()
