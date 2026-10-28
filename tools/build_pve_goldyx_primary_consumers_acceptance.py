"""Goldyx original Inspiration, Crystal Luck and one-hex Flight consumers.

Plan only unless --write. --from replays an accepted prefix without new evidence.
No fixture mutations; the combined real-backend test must pass before dispatch.
"""
import argparse
import json
from pathlib import Path


def build():
    steps = []
    rule = ('Goldyx合法等级6三自身技能及原16牌；真伤跨回合，绿激励实际付行进，'
            '新抽全神贯注获得第三种库存晶体；森林3费后剩1，一格飞行0费到村庄，'
            '荧光灵运按3种库存晶体产生影响3并真实购买治疗旧伤')

    def click(path, label, expected=None, capture=False):
        steps.append(dict(buttonPath='Canvas/BattleTable/'+path, label=label,
            waitAfterSeconds=.3, skipScreenshot=not capture,
            after=[dict(key=k, expected=str(v), rule=rule)
                   for k,v in {'ui.truncated':'', **(expected or {})}.items()]))

    (click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp', 'Goldyx三个原技能同玩家连续路线'))
    click('CampShade/Camp/Btn_Loadout', '既有HornGold种子0')
    for _ in range(2): click('CampShade/Picker/Btn_NextPage', '原构筑下一页')
    click('CampShade/Picker/Btn_Select_39', '既有红蓝各1晶，无预配部队')
    click('CampShade/Camp/Btn_SmallDeckHeroBuild', '合法原英雄构筑')
    click('CampShade/HeroBuild/Btn_Hero_Goldyx', '原高蒂克斯')
    click('CampShade/HeroBuild/Btn_Level_6', '等级6护甲3，三个自身技能名额')
    for i in [5,6,8]: click('CampShade/HeroBuild/Btn_Skill_'+str(i), '选择本英雄原技能'+str(i))
    click('CampShade/HeroBuild/Btn_DeckSource', '本英雄原16张牌，原种子洗混')
    click('CampShade/HeroBuild/Btn_Begin', '同一原玩家开始',
          {'hero.id':'Goldyx','hero.level':6,'hero.armor':3,'hero.skills':3,
           'hero.deckTotal':16,'hand':6,'deck.count':10,'red':1,'blue':1,'green':0,
           'hand.ids':'basic_card_021,basic_card_014,basic_card_000,basic_card_007,basic_card_023,basic_card_009'})
    click('Tactic_day_1/Btn_Tactic_day_1', '原白昼战术')
    click('Btn_BeginTurn', '第一正式回合')
    click('Btn_JourneyEncounter_0', '原032真实攻击4护甲3，已有演练敌人')
    for _ in range(3): click('Inspector/Btn_Next', '正式无格挡承伤')
    click('Hand/Btn_Card_basic_card_021_0', '原狂怒实际基础攻击', {'wounds':2,'hand':8})
    click('Inspector/Btn_Choice_attack', '原攻击2')
    click('Inspector/Btn_Play', '第一张原牌真实出牌', {'melee':2})
    click('Hand/Btn_Card_basic_card_014_0', '原耐力横置，保留行进作后续绿费消费者')
    click('Inspector/Btn_Sideways', '真实普通攻击1，合3', {'melee':3,'hand':6})
    click('Enemy_0/Btn_Target', '同一实际敌人')
    click('Inspector/Btn_Resolve', '真实攻击3击杀原护甲3', {'kills':1,'fame':37,'wounds':2})
    click('Inspector/Btn_Next', '正式战斗结束')
    click('Inspector/Btn_EndTurn', '保留两张战伤及同一原16牌',
          {'journey.completedTurns':1,'journey.completedBattles':1,'hand':6,
           'deck.count':10,'wounds':2,'journey.samePlayer':'True'})
    click('Btn_BeginTurn', '同一Goldyx第二正式回合')
    click('Btn_JourneyHand', '原手牌与上回合真伤')
    click('JourneyHand/Btn_Support', '本人已学原技能')
    click('JourneySupport/Btn_OutsideSkill_2', 'Goldyx原绿激励',
          {'journey.outsideSkill':'hero_skill_pool_005:08','journey.outsideSkillUsed':'False'})
    click('JourneySupport/Btn_UseOutsideSkill', '抽实际狂怒和全神贯注，获得绿标',
          {'hand':8,'deck.count':8,'tokens.green':1,'green':0,
           'journey.outsideSkillUsed':'True'}, True)
    click('JourneySupport/Btn_BackSupport', '返回实际手牌')
    click('JourneyHand/@card=basic_card_000', '保留的原行进')
    click('JourneyHand/Btn_OutsideChoice_strong', '印刷绿色费用强化移动4')
    click('JourneyHand/Btn_PlayOutside', '实际支付绿激励标记',
          {'movement':4,'tokens.green':0,'green':0,'hand':7})
    click('JourneyHand/Btn_NextHand', '七张真实手牌的第二页，展示刚抽到的全神贯注')
    click('JourneyHand/@card=basic_card_006', '激励刚抽到的原全神贯注')
    click('JourneyHand/Btn_OutsideChoice_green_crystal', '原基础明确选择库存绿色魔晶')
    click('JourneyHand/Btn_PlayOutside', '新抽原牌消费者；红蓝绿三种库存与临时标记分离',
          {'green':1,'red':1,'blue':1,'tokens.green':0,'hand':6}, True)
    click('JourneyHand/Btn_CloseHand', '真实原移动资源用于地图')
    click('Btn_JourneyMap', '已有原费用演练地图')
    click('JourneyMap/Btn_Hex_forest', '相邻森林1,0，白昼费用3')
    click('JourneyMap/Btn_Move', '实际消费行进移动3，剩1',
          {'movement':1,'journey.position':'1,0'})
    click('JourneyMap/Btn_CloseMap', '同玩家继续移动阶段')
    click('Btn_JourneyHand', '原自有飞行入口')
    click('JourneyHand/Btn_Support', '飞行按实际落点距离')
    click('JourneySupport/Btn_OutsideSkill_1', '选择本人原飞行',
          {'journey.outsideSkill':'hero_skill_pool_005:06','journey.outsideSkillUsed':'False'})
    click('JourneySupport/Btn_UseOutsideSkill', '进入正式飞行落点选择')
    click('JourneyMap/Btn_Hex_plains', '森林到村庄0,1恰好一格')
    click('JourneyMap/Btn_Move', '原一格飞行0费用，移动1保留而非支付普通平原2',
          {'movement':1,'journey.position':'0,1','journey.outsideSkillUsed':'True'}, True)
    click('JourneyMap/Btn_CloseMap', '到达真实村庄后开始实际交涉')
    click('Btn_JourneyInteraction', '当前位置实际村庄')
    click('JourneyInteraction/Btn_BeginInteraction', '正式村庄交涉，原声誉0无修正',
          {'journey.interacting':'True','reputation':0,'influence':0})
    click('JourneyInteraction/Btn_CloseInteraction', '保留正式交涉')
    click('Btn_JourneyHand', '实际交涉中的原技能')
    click('JourneyHand/Btn_Support', '荧光灵运读取当前实际库存')
    click('JourneySupport/Btn_OutsideSkill_0', '本人的荧光灵运',
          {'journey.outsideSkill':'hero_skill_pool_005:05','journey.outsideSkillUsed':'False'})
    click('JourneySupport/Btn_UseOutsideSkill', '红蓝绿三种库存晶体产生原影响3',
          {'influence':3,'green':1,'red':1,'blue':1,'skills.used':'True,True,True'}, True)
    click('JourneySupport/Btn_BackSupport', '返回交涉手牌')
    click('JourneyHand/Btn_CloseHand', '进入真实影响力消费者')
    click('Btn_JourneyInteraction', '同一实际村庄与同一次交涉')
    click('JourneyInteraction/Btn_BuyHealing', '实际支付影响3购买治疗1',
          {'influence':0,'healing':1})
    click('JourneyInteraction/Btn_CloseInteraction', '使用刚购买的真实治疗点')
    click('Btn_JourneyHand', '上回合两张真实战伤')
    click('JourneyHand/Btn_HealHero', '治疗上回合实际伤牌1，第二伤仍保留',
          {'wounds':1,'healing':0,'hand':5,'site.discardWounds':0})
    click('JourneyHand/Btn_CloseHand', '保留全部原牌与真实库存')
    click('Btn_EndTurn', '两回合同一Goldyx，三技能及实际费用/治疗消费者完成',
          {'journey.completedTurns':2,'journey.completedBattles':1,
           'journey.samePlayer':'True','hero.id':'Goldyx','hero.skills':3,
           'hand':6,'deck.count':7,'journey.discardCount':4,'hero.deckTotal':17,
           'wounds':1,'site.discardWounds':0,'green':1,'red':1,'blue':1,
           'tokens.green':0,'influence':0,'fame':37,'journey.position':'0,1'}, True)
    return steps


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write', action='store_true')
    parser.add_argument('--from', dest='start', type=int, default=0)
    parser.add_argument('--output', default='AutomationConfigs/pve_battle.json')
    args = parser.parse_args()
    steps = build()
    if not 0 <= args.start <= len(steps): parser.error('Invalid --from index')
    for step in steps[:args.start]: step['after']=[]; step['skipScreenshot']=True
    if args.write:
        Path(args.output).write_text(json.dumps(dict(
            scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,
            initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=360,
            useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
    print(len(steps),'operations;',sum(len(s['after']) for s in steps),'assertions;',
          sum(not s['skipScreenshot'] for s in steps),'screenshots;',
          'written' if args.write else 'plan only; backend gate before scene')


if __name__ == '__main__': main()
