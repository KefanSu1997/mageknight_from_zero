"""Original Arythea red Inspiration and Secret Negotiation; plan only unless --write."""
import argparse
import json
from pathlib import Path


def build():
    steps = []
    rule = '原阿瑞希亚合法两技能与16牌；红激励付灵活战斗原红费，真实两战伤跨回合，秘密谈判2加新抽原牌横置1购买治疗，牌区与实际位置保留'

    def click(path, label, expected=None, capture=False):
        steps.append(dict(buttonPath='Canvas/BattleTable/' + path, label=label,
            waitAfterSeconds=.3, skipScreenshot=not capture,
            after=[dict(key=k, expected=str(v), rule=rule)
                   for k,v in {'ui.truncated':'', **(expected or {})}.items()]))

    (click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp', '本人秘密谈判与红激励连续路线'))
    click('CampShade/Camp/Btn_Loadout', '既有HornGold种子0')
    for _ in range(2): click('CampShade/Picker/Btn_NextPage', '原构筑列表')
    click('CampShade/Picker/Btn_Select_39', '既有红蓝各一晶，零部队')
    click('CampShade/Camp/Btn_SmallDeckHeroBuild', '原英雄合法构筑')
    click('CampShade/HeroBuild/Btn_Hero_Arythea', '阿瑞希亚原自身技能池')
    click('CampShade/HeroBuild/Btn_Level_6', '等级6名望35护甲3')
    click('CampShade/HeroBuild/Btn_Skill_3', '秘密谈判昼2夜3')
    click('CampShade/HeroBuild/Btn_Skill_8', '本人红色激励')
    click('CampShade/HeroBuild/Btn_DeckSource', '原16牌真实洗混')
    click('CampShade/HeroBuild/Btn_Begin', '同一原英雄真实牌库',
          {'hero.id':'Arythea','hero.skills':2,'hero.deckTotal':16,'hand':6,'deck.count':10,'red':1,'blue':1})
    click('Tactic_day_1/Btn_Tactic_day_1', '正式昼战术')
    click('Btn_BeginTurn', '第一回合')
    click('Btn_JourneyHand', '实际手牌')
    click('JourneyHand/Btn_Support', '原自身技能')
    click('JourneySupport/Btn_OutsideSkill_1', '本人激励')
    click('JourneySupport/Btn_UseOutsideSkill', '实际抽两原牌与红色标记',
          {'hand':8,'deck.count':8,'tokens.red':1,'red':1,'journey.outsideSkillUsed':'True'},True)
    click('JourneySupport/Btn_BackSupport', '保留实际新抽原牌')
    click('JourneyHand/Btn_CloseHand', '实际战斗')
    click('Btn_JourneyEncounter_0', '原032甲3攻击4')
    for _ in range(3): click('Inspector/Btn_Next', '无格挡正式承伤')
    click('Hand/Btn_Card_basic_card_024_0', '本人原灵活战斗', {'wounds':2,'hand':10})
    click('Inspector/Btn_Choice_strong_attack', '原红费强化攻击4')
    click('Inspector/Btn_Play', '激励红标真实付费而库存晶体保留',
          {'melee':4,'tokens.red':0,'red':1,'hand':9})
    click('Enemy_0/Btn_Target', '同一原怪物')
    click('Inspector/Btn_Resolve', '强化攻击4实际杀甲3',{'kills':1,'fame':37,'wounds':2},True)
    click('Inspector/Btn_Next', '正式战斗结束')
    click('Inspector/Btn_EndTurn', '两真实战伤跨回合',
          {'hand':9,'deck.count':8,'journey.discardCount':1,'wounds':2,'journey.completedTurns':1})
    click('Btn_BeginTurn', '同玩家第二回合')
    click('Btn_JourneyHand', '原行进支付地形')
    click('JourneyHand/@card=basic_card_000', '保留的原行进')
    click('JourneyHand/Btn_OutsideChoice_base', '原移动2')
    click('JourneyHand/Btn_PlayOutside', '实际原卡移动池',{'movement':2,'hand':8})
    click('JourneyHand/Btn_CloseHand', '实际位置')
    click('Btn_JourneyMap', '既有演练地图，不替代原探索')
    click('JourneyMap/Btn_Hex_plains', '邻接村庄')
    click('JourneyMap/Btn_Move', '付平原2',{'movement':0,'journey.position':'0,1'})
    click('JourneyMap/Btn_CloseMap', '正式村庄入口')
    click('Btn_JourneyInteraction', '当前实际村庄')
    click('JourneyInteraction/Btn_BeginInteraction', '正式同一次交涉',{'influence':0,'journey.interacting':'True'})
    click('JourneyInteraction/Btn_CloseInteraction', '保留交涉')
    click('Btn_JourneyHand', '本人影响技能')
    click('JourneyHand/Btn_Support', '原本人技能')
    click('JourneySupport/Btn_OutsideSkill_0', '秘密谈判')
    click('JourneySupport/Btn_UseOutsideSkill', '原白昼影响2',
          {'influence':2,'journey.outsideSkillUsed':'True'})
    click('JourneySupport/Btn_BackSupport', '激励所抽真实原牌')
    click('JourneyHand/@card=basic_card_021', '激励实际新抽狂怒')
    click('JourneyHand/Btn_SidewaysInfluence', '原横置1补足治疗费用',{'influence':3,'hand':7})
    click('JourneyHand/Btn_CloseHand', '保留实际影响与旧伤')
    click('Btn_JourneyInteraction', '真实治疗消费者')
    click('JourneyInteraction/Btn_BuyHealing', '支付影响3，秘密谈判贡献不可缺',{'influence':0,'healing':1})
    click('JourneyInteraction/Btn_CloseInteraction', '同一张真实战伤')
    click('Btn_JourneyHand', '手伤实际治疗')
    click('JourneyHand/Btn_HealHero', '治疗一张旧伤归公共供应，不是弃伤',{'wounds':1,'healing':0,'hand':6},True)
    click('JourneyHand/Btn_CloseHand', '正式次回合结算')
    click('Btn_EndTurn', '同原16牌与一剩余真伤保留',
          {'hero.id':'Arythea','hero.deckTotal':17,'hand':6,'deck.count':8,'journey.discardCount':3,
           'wounds':1,'site.discardWounds':0,'journey.completedTurns':2,'journey.completedBattles':1,
           'journey.samePlayer':'True','red':1,'blue':1,'journey.position':'0,1'})
    return steps


if __name__ == '__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--write',action='store_true')
    parser.add_argument('--from',dest='start',type=int,default=0)
    args=parser.parse_args(); steps=build()
    if not 0 <= args.start <= len(steps): parser.error('invalid start')
    for step in steps[:args.start]: step['after']=[]; step['skipScreenshot']=True
    if args.write:
        Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(
            scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,
            initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=420,
            useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
    print(len(steps),'steps',sum(len(s['after']) for s in steps),'assertions',sum(not s['skipScreenshot'] for s in steps),'screenshots')
