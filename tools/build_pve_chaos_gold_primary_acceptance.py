"""Seeded Krang actual White/Red/Gold turns, explicit gold ColdFire ranged reward.

Default plan only. --write emits config; --from replays an accepted prefix silently.
Does not import older scripts that write shared configuration on import.
"""
import argparse
import json
from pathlib import Path


def build():
    steps=[]
    rule='Krang本人混乱：seed0实际初Gold，正式移动/两休息空转Blue/Green/Black，随后三回合White/Red/Gold；金明确选黑冰火远程1加原火焰箭矢3实际击杀，连续六回合同玩家'
    def click(path,label,expected=None,capture=False):
        steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,
            waitAfterSeconds=.3,skipScreenshot=not capture,
            after=[dict(key=k,expected=str(v),rule=rule) for k,v in
                   {'ui.truncated':'',**(expected or {})}.items()]))
    def outside(card,choice,expected=None):
        click('Btn_JourneyHand','真实原手牌')
        click('JourneyHand/@card='+card,'选择实际原牌')
        click('JourneyHand/Btn_OutsideChoice_'+choice,'原效果明确选择')
        click('JourneyHand/Btn_PlayOutside','原牌实际消费',expected)
        click('JourneyHand/Btn_CloseHand','返回连续旅程')
    def combat(card,choice,expected=None):
        click('Hand/Btn_Card_'+card+'_0','实际保留原牌')
        click('Inspector/Btn_Choice_'+choice,'原牌印刷效果')
        click('Inspector/Btn_Play','真实原费用与贡献',expected)

    (click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','同一Krang最小合法构筑'))
    click('CampShade/Camp/Btn_Loadout','既有重放种子0构筑')
    for _ in range(2):click('CampShade/Picker/Btn_NextPage','构筑下一页')
    click('CampShade/Picker/Btn_Select_39','库存红蓝各1，无预配部队')
    click('CampShade/Camp/Btn_ChooseCards','六张既有原牌')
    p='CampShade/DeedPicker/'
    click(p+'Btn_ClearDeeds','清空旧选择，不造新牌')
    for c in ['000','001','008']:click(p+'Btn_ChooseDeed_basic_card_'+c,'原行进/宁静/承诺')
    click(p+'Btn_DeedNext','原基础第二页')
    for c in ['014','021']:click(p+'Btn_ChooseDeed_basic_card_'+c,'原耐力/狂怒')
    click(p+'Btn_DeedKind_1','原高级行动')
    click(p+'Btn_ChooseDeed_advanced_card_000','原火焰箭矢，后续金奖励的实际同阶段消费者')
    click(p+'Btn_DoneDeeds','确认六张原卡')
    click('CampShade/Camp/Btn_SmallDeckHeroBuild','实际自身技能合法构筑')
    click('CampShade/HeroBuild/Btn_Hero_Krang','Krang本人')
    click('CampShade/HeroBuild/Btn_Level_6','等级6护甲3，未超过3技能名额')
    click('CampShade/HeroBuild/Btn_Skill_7','唯一选择本人混乱大师')
    click('CampShade/HeroBuild/Btn_Begin','实际取得随机初色，不注入轮盘',
          {'hero.id':'Krang','hero.level':6,'hero.armor':3,'hero.skills':1,
           'skills.chaosColor':'Gold','hero.deckTotal':6,'hand':6,'deck.count':0,'red':1,'blue':1})
    click('Tactic_day_1/Btn_Tactic_day_1','原昼战术')
    click('Btn_BeginTurn','第一个正式回合')
    outside('basic_card_000','base',{'movement':2})
    click('Btn_JourneyMap','真实演练地图')
    click('JourneyMap/Btn_Hex_plains','实际村庄目的地')
    click('JourneyMap/Btn_Move','支付原地形费用2',{'movement':0,'journey.position':'0,1'})
    click('JourneyMap/Btn_CloseMap','保留位置')
    click('Btn_EndTurn','第一回合实际移动后结束，未使用混乱',
          {'journey.completedTurns':1,'skills.chaosPending':'True','skills.chaosColor':'Gold'})
    click('Btn_ChaosRotate','正式未用空转Gold到Blue',{'skills.chaosColor':'Blue','skills.chaosPending':'False'})
    for turn,index,card,color in [(2,0,'宁静001','Green'),(3,1,'耐力014','Black')]:
        click('Btn_BeginTurn','本人正式休息回合'+str(turn))
        click('Btn_JourneyHand','仍持有的真实原牌')
        click('JourneyHand/Btn_Support','正式休息入口')
        click('JourneySupport/Btn_RestCard_'+str(index),'真实弃置'+card)
        click('JourneySupport/Btn_Rest','执行正式休息，仅弃所选原牌')
        click('JourneySupport/Btn_BackSupport','返回手牌')
        click('JourneyHand/Btn_CloseHand','回到同一玩家')
        click('Btn_EndTurn','正式休息回合结束，可选未用空转',
              {'journey.completedTurns':turn,'skills.chaosPending':'True','hand':6-turn})
        click('Btn_ChaosRotate','正式空转到'+color,{'skills.chaosColor':color,'skills.chaosPending':'False'})
    click('Btn_BeginTurn','第四正式回合，保留Black开始White消费者')
    outside('basic_card_008','base',{'influence':2})
    click('Btn_JourneyHand','本人技能入口')
    click('JourneyHand/Btn_Support','唯一技能无需翻页')
    click('JourneySupport/Btn_OutsideSkill_0','本人原003:07')
    click('JourneySupport/Btn_OutsideOption_0','先转White后领奖')
    click('JourneySupport/Btn_UseOutsideSkill','真实White影响2',
          {'skills.chaosColor':'White','influence':4,'journey.outsideSkillUsed':'True'})
    click('JourneySupport/Btn_BackSupport','返回')
    click('JourneyHand/Btn_CloseHand','正式村庄交涉')
    click('Btn_JourneyInteraction','真实村庄')
    click('JourneyInteraction/Btn_BeginInteraction','开始正式交涉')
    click('JourneyInteraction/Btn_Offer_army_2_006','实际原供应农民')
    click('JourneyInteraction/Btn_Recruit','影响4实际取得原部队',
          {'influence':0,'units.count':1,'units.ready':'True','skills.chaosColor':'White'},True)
    click('JourneyInteraction/Btn_CloseInteraction','关闭交涉')
    click('Btn_EndTurn','第四正式回合结束',
          {'journey.completedTurns':4,'skills.chaosPending':'False','hand':2,'journey.discardCount':4})
    click('Btn_BeginTurn','同玩家第五回合')
    click('Btn_JourneyEncounter_0','实际原032，攻击4护甲3')
    for _ in range(3):click('Inspector/Btn_Next','正式英雄承伤，农民不代受')
    click('Btn_Skills','唯一自身混乱技能页')
    click('Skills/Btn_Skill_0','本人混乱，无跨技能页假设')
    click('Inspector/Btn_SkillChoice_0','White转Red攻击2')
    click('Inspector/Btn_UseSkill','真实红色攻击消费者',
          {'skills.chaosColor':'Red','melee':2,'wounds':2})
    click('Btn_Skills','返回真实原牌')
    combat('basic_card_021','attack',{'melee':4})
    click('Enemy_0/Btn_Target','同一原032')
    click('Inspector/Btn_Resolve','真实击杀后保留Red',{'kills':1,'fame':37,'wounds':2})
    click('Inspector/Btn_Next','结束第一战')
    click('Inspector/Btn_EndTurn','正式第五回合结束，不空转',
          {'journey.completedTurns':5,'skills.chaosColor':'Red','skills.chaosPending':'False'})
    click('Btn_BeginTurn','同一Krang第六正式回合')
    click('Btn_JourneyEncounter_4','原狼骑兵护甲4，远程提前击杀免新增伤')
    click('Btn_Skills','唯一原技能页')
    click('Skills/Btn_Skill_0','当前Red，下次使用转Gold')
    click('Inspector/Btn_SkillChoice_2','金色明确选择黑奖励冰火远程1',capture=True)
    click('Inspector/Btn_UseSkill','先转Gold并保留Gold，非转到Black',
          {'skills.chaosColor':'Gold','power.attacks':'Ranged:ColdFire:1','ranged':1,'skills.used':'True'},True)
    click('Btn_Skills','返回保留原牌')
    combat('advanced_card_000','strong',
           {'red':0,'power.attacks':'Ranged:ColdFire:1,Ranged:Fire:3','ranged':4})
    click('Enemy_0/Btn_Target','实际原狼骑兵')
    click('Inspector/Btn_Resolve','冰火1加火焰3真实远程击杀原甲4',
          {'kills':1,'fame':40,'ranged':0,'wounds':2},True)
    for _ in range(4):click('Inspector/Btn_Next','正式剩余战斗阶段，无新增伤')
    click('Inspector/Btn_EndTurn','同玩家六回合两战，轮盘与实际伤牌/单位保留',
          {'journey.completedTurns':6,'journey.completedBattles':2,'journey.samePlayer':'True',
           'hero.id':'Krang','hero.skills':1,'skills.chaosColor':'Gold','skills.chaosPending':'False',
           'fame':40,'wounds':2,'hero.deckTotal':8,'hand':2,'deck.count':0,'journey.discardCount':6,
           'units.count':1,'units.ready':'True','units.wounds':'0','red':0,'blue':1},True)
    return steps


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--write',action='store_true')
    p.add_argument('--from',dest='start',type=int,default=0)
    p.add_argument('--output',default='AutomationConfigs/pve_battle.json')
    a=p.parse_args();steps=build()
    if not 0 <= a.start <= len(steps):p.error('Invalid --from')
    for s in steps[:a.start]:s['after']=[];s['skipScreenshot']=True
    if a.write:Path(a.output).write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',
        captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,
        maxRunSeconds=360,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
    print(len(steps),'operations;',sum(len(s['after']) for s in steps),'assertions;',
          sum(not s['skipScreenshot'] for s in steps),'screenshots;', 'written' if a.write else 'plan only')


if __name__=='__main__':main()
