"""Original Arythea day-black/night-gold costs, one persistent two-round journey.
Default plan only. --write emits config; --from N replays prior steps quietly.
"""
import argparse
import json
from pathlib import Path


def build():
    steps=[]
    rule='原两极每回合一次：原黑暗护符实际产昼黑/夜金，支付原白费/强化法术黑费；真实弃洗抽跨轮，不注入骰面'
    def click(path,label,expected=None,capture=False):
        steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,
            skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule=rule)
            for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
    (click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','合法自定原牌构筑'))
    click('CampShade/Camp/Btn_Loadout','既有HornGold红蓝各一晶')
    for _ in range(2):click('CampShade/Picker/Btn_NextPage','下一页')
    click('CampShade/Picker/Btn_Select_39','沿用原起点，不改资源/种子')
    click('CampShade/Camp/Btn_ChooseCards','五张实际原牌')
    p='CampShade/DeedPicker/'
    click(p+'Btn_ClearDeeds','清空待选旧牌')
    for card in ['basic_card_000','basic_card_007','basic_card_008']:
        click(p+'Btn_ChooseDeed_'+card,'原行进/敏捷/承诺')
    click(p+'Btn_DeedKind_2','原法术')
    click(p+'Btn_ChooseDeed_magic_009','原火球/火焰风暴')
    click(p+'Btn_DeedKind_3','原神器')
    click(p+'Btn_DeedNext','神器第二页')
    click(p+'Btn_ChooseDeed_items_015','原黑暗护符，两轮正常使用同牌')
    click(p+'Btn_DoneDeeds','确认五牌')
    click('CampShade/Camp/Btn_SmallDeckHeroBuild','合法原英雄技能')
    click('CampShade/HeroBuild/Btn_Hero_Arythea','原阿瑞希亚')
    click('CampShade/HeroBuild/Btn_Level_4','原等级4允许两技能，仅选一个')
    click('CampShade/HeroBuild/Btn_Skill_7','原两极分化000:07')
    click('CampShade/HeroBuild/Btn_Begin','正式同一玩家',
          {'hero.id':'Arythea','hero.level':4,'hero.skills':1,'fame':15,'hand':5,'red':1,'blue':1})
    click('Tactic_day_1/Btn_Tactic_day_1','正式昼战术')
    click('Btn_BeginTurn','白昼第一回合')

    def amulet(color,count):
        click('Btn_JourneyHand','同一真实原手牌')
        click('JourneyHand/@card=items_015','原黑暗护符，选择牌重置输入')
        click('JourneyHand/Btn_ArtifactNormal','正常使用，不永久移除')
        for _ in range(count):click('JourneyHand/Btn_ArtifactColor_0','实际选择'+color)
        click('JourneyHand/Btn_PlayOutside','原牌实际产生'+color+'标记',
              {'tokens.'+color.lower():1,'skills.used':'False'})

    def discard(cards):
        for card in cards:
            click('JourneyHand/@card='+card,'选择实际未用原牌')
            click('JourneyHand/Btn_DiscardAtEnd','正式回合末弃牌标记')
        click('JourneyHand/Btn_CloseHand','保留真实手牌进入战斗')

    def pay(card,required,source,expected):
        click('Hand/Btn_Card_'+card+'_0','实际原牌消费者')
        click('Inspector/Btn_Choice_strong','原强化印刷费用')
        click('Inspector/Btn_ManaPayment','选择真实付款技能')
        click('ManaPayment/Btn_Required_'+required,'原费用中的'+required)
        click('ManaPayment/Btn_Source_'+source,'实际库存'+source+'，不是源骰注入')
        click('ManaPayment/Btn_Select','选择尚未支付')
        click('Inspector/Btn_Play','正式整笔原费用支付并标记技能本回合已用',expected,True)

    def finish(turn,hand,wounds,fame,red):
        for _ in range(4):click('Inspector/Btn_Next','真实清敌后的剩余阶段')
        click('Inspector/Btn_EndTurn','正式回合末弃牌与临时费用清理',
              {'journey.completedTurns':turn,'journey.completedBattles':turn,'hand':hand,
               'journey.discardCount':5,'wounds':wounds,'fame':fame,'red':red,'blue':1,'journey.samePlayer':'True'})

    amulet('Black',5)
    discard(['basic_card_000','basic_card_008','magic_009'])
    click('Btn_JourneyEncounter_0','实际原032甲3')
    pay('basic_card_007','White','Black',{'tokens.black':0,'white':0,'ranged':3,'skills.used':'True','red':1,'blue':1})
    click('Enemy_0/Btn_Target','原甲3敌人')
    click('Inspector/Btn_Resolve','原白费远程3实际击杀',{'kills':1,'fame':17,'wounds':0})
    finish(1,0,0,17,1)
    click('Btn_Announce','实际空库声明轮末')
    click('Btn_EndRound','同五原牌实际洗回，正式进入黑夜',{'deck.count':5,'journey.dayPart':'Night'})
    click('Btn_PrepareRound','正式抽回实际五牌，不重建玩家',{'hand':5,'journey.samePlayer':'True'})
    click('Tactic_night_1/Btn_Tactic_night_1','正式夜战术')
    click('Btn_BeginTurn','黑夜第二回合技能次数恢复',{'skills.used':'False'})
    amulet('Gold',4)
    discard(['basic_card_000','basic_card_007','basic_card_008'])
    click('Btn_JourneyEncounter_4','实际原狼骑兵007甲4')
    pay('magic_009','Black','Gold',{'tokens.gold':0,'tokens.black':0,'red':0,'blue':1,'siege':8,'wounds':1,'skills.used':'True'})
    click('Enemy_0/Btn_Target','真实狼骑兵')
    click('Inspector/Btn_Resolve','原强化火焰攻城8实际击杀且保留自伤',{'kills':1,'fame':20,'wounds':1},True)
    finish(2,1,1,20,0)
    return steps


def main():
    p=argparse.ArgumentParser();p.add_argument('--write',action='store_true')
    p.add_argument('--from',dest='start',type=int,default=0)
    p.add_argument('--output',default='AutomationConfigs/pve_battle.json');a=p.parse_args();steps=build()
    if not 0<=a.start<=len(steps):p.error('--from outside route')
    for s in steps[:a.start]:s['after']=[];s['skipScreenshot']=True
    if a.write:Path(a.output).write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=420,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
    print(len(steps),'operations',sum(len(s['after']) for s in steps),'assertions',sum(not s['skipScreenshot'] for s in steps),'captures','written' if a.write else 'plan only')


if __name__=='__main__':main()
