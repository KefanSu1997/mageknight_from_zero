"""Wolfhawk's original fame Inspiration, defensive Insight and attack-reducing Taunt.

Two actual battles and formal turns. Plan only unless --write; --from replays
an accepted prefix without duplicate evidence. No shared fixture mutations.
"""
import argparse
import json
from pathlib import Path


def build():
    steps = []
    rule = ('Wolfhawk合法等级6三自身技能和原16牌；名望激励抽牌实际消费，'
            '洞悉只忽略原巫妖物抗后原物理4击杀；正式次回合嘲讽使原攻击4到3，'
            '护甲3真实仅加1伤；同玩家旧伤/牌区/费用保留')

    def click(path, label, expected=None, capture=False):
        steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,
            waitAfterSeconds=.3,skipScreenshot=not capture,
            after=[dict(key=k,expected=str(v),rule=rule)
                   for k,v in {'ui.truncated':'',**(expected or {})}.items()]))

    (click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','Wolfhawk三个本人原技能'))
    click('CampShade/Camp/Btn_Loadout','既有HornGold种子0')
    for _ in range(2): click('CampShade/Picker/Btn_NextPage','原构筑下一页')
    click('CampShade/Picker/Btn_Select_39','红蓝各1晶，无预配部队')
    click('CampShade/Camp/Btn_SmallDeckHeroBuild','合法原英雄构筑')
    click('CampShade/HeroBuild/Btn_Hero_Wolfhawk','原Wolfhawk自身池004')
    click('CampShade/HeroBuild/Btn_Level_6','等级6护甲3三个合法自身技能')
    for i in [5,6,8]: click('CampShade/HeroBuild/Btn_Skill_'+str(i),'选择原自身技能'+str(i))
    click('CampShade/HeroBuild/Btn_DeckSource','本英雄原16牌正式洗混')
    click('CampShade/HeroBuild/Btn_Begin','同玩家原牌库开始',
          {'hero.id':'Wolfhawk','hero.level':6,'hero.armor':3,'hero.skills':3,
           'hero.deckTotal':16,'hand':6,'deck.count':10,'fame':35,'red':1,'blue':1})
    click('Tactic_day_1/Btn_Tactic_day_1','正式昼战术')
    click('Btn_BeginTurn','本人第一回合')
    click('Btn_JourneyHand','本人实际原手牌')
    click('JourneyHand/Btn_Support','原自身技能')
    click('JourneySupport/Btn_OutsideSkill_2','原名望型激励',
          {'journey.outsideSkill':'hero_skill_pool_004:08'})
    click('JourneySupport/Btn_UseOutsideSkill','真实抽2并名望35到36，不是魔力奖励',
          {'hand':8,'deck.count':8,'fame':36,'tokens.blue':0,'tokens.green':0,
           'skills.used':'False,False,True'},True)
    click('JourneySupport/Btn_BackSupport','保留激励新抽原狂怒')
    click('JourneyHand/Btn_CloseHand','开始第一场真实战斗')
    click('Btn_JourneyEncounter_7','已有演练原寒冰巫妖004，物抗/冰抗护甲4')
    click('Btn_Skills','原洞悉猎物')
    click('Skills/Btn_Skill_0','本人洞悉，不借公共技能')
    click('Enemy_0/Btn_Target','实际原巫妖实例')
    click('Inspector/Btn_SkillChoice_115','只忽略原物理抗性，保留原寒冰抗性')
    click('Inspector/Btn_UseSkill','实际移除同敌防御能力',
          {'skills.used':'True,False,True','enemies.armor':'4'},True)
    click('Btn_Skills','返回真实原牌')
    for _ in range(3): click('Inspector/Btn_Next','正式原冰攻2分配给护甲3英雄')
    click('Hand/Btn_Card_basic_card_021_0','初始第一张狂怒，保留激励抽入的另一张',{'wounds':1})
    click('Inspector/Btn_Choice_strong','原红晶费用强化物理攻击4')
    click('Inspector/Btn_Play','实际红费支付，物理贡献4',{'red':0,'melee':4})
    click('Enemy_0/Btn_Target','同一被洞悉忽略物抗的原巫妖')
    click('Inspector/Btn_Resolve','物理4完整消费击杀甲4；原物抗若在只生效2',
          {'kills':1,'fame':41,'wounds':1},True)
    click('Inspector/Btn_Next','第一战正式结束')
    click('Inspector/Btn_EndTurn','同玩家保留激励抽牌和实际冰攻旧伤',
          {'journey.completedTurns':1,'journey.completedBattles':1,'hand':8,
           'deck.count':8,'journey.discardCount':1,'wounds':1,'journey.samePlayer':'True'})
    click('Btn_BeginTurn','本人第二正式回合')
    click('Btn_JourneyEncounter_0','实际原032攻击4护甲3')
    click('Inspector/Btn_Next','格挡阶段，原嘲讽合法时机')
    click('Btn_Skills','本人原嘲讽')
    click('Skills/Btn_NextSkill','从已用洞悉翻到本人嘲讽')
    click('Skills/Btn_Skill_1','嘲讽每回合一次，选攻击槽')
    click('Enemy_0/Btn_Target','实际原032唯一攻击槽')
    click('Inspector/Btn_SkillChoice_0','原降攻分支，不是增攻减甲')
    click('Inspector/Btn_UseSkill','同敌真实攻击4降低到3',
          {'enemies.attacks':'3','enemies.armor':'3','skills.used':'True,True,True'})
    click('Btn_Skills','不格挡，直接检验真实承伤差异')
    click('Inspector/Btn_Next','正式分配伤害')
    click('Inspector/Btn_Next','原护甲3承攻击3只加一伤，旧伤仍在',
          {'wounds':2},True)
    click('Hand/Btn_Card_basic_card_021_0','激励第一回合抽入并保留的另一张真实狂怒')
    click('Inspector/Btn_Choice_attack','原基础攻击2')
    click('Inspector/Btn_Play','实际消费激励新抽牌',{'melee':2})
    click('Hand/Btn_Card_basic_card_000_0','真实原行进横置')
    click('Inspector/Btn_Sideways','追加原攻击1，合计3',{'melee':3})
    click('Enemy_0/Btn_Target','同一实际降攻敌人')
    click('Inspector/Btn_Resolve','真实攻击3击杀原032',{'kills':1,'fame':43,'wounds':2})
    click('Inspector/Btn_Next','正式第二战结束')
    click('Inspector/Btn_EndTurn','两回合两战，同玩家原16牌加两张实际伤守恒',
          {'journey.completedTurns':2,'journey.completedBattles':2,'journey.samePlayer':'True',
           'hero.id':'Wolfhawk','hero.skills':3,'fame':43,'wounds':2,
           'hero.deckTotal':18,'hand':7,'deck.count':8,'journey.discardCount':3,
           'red':0,'blue':1},True)
    return steps


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--write',action='store_true')
    p.add_argument('--from',dest='start',type=int,default=0)
    p.add_argument('--output',default='AutomationConfigs/pve_battle.json')
    a=p.parse_args();steps=build()
    if not 0 <= a.start <= len(steps): p.error('Invalid --from index')
    for s in steps[:a.start]: s['after']=[];s['skipScreenshot']=True
    if a.write:
        Path(a.output).write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',
            captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,
            maxRunSeconds=360,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
    print(len(steps),'operations;',sum(len(s['after']) for s in steps),'assertions;',
          sum(not s['skipScreenshot'] for s in steps),'screenshots;',
          'written' if a.write else 'plan only; backend gate before scene')


if __name__=='__main__': main()
