"""One original Insight element-removal consumer, plan only unless --write."""
import argparse
import json
from pathlib import Path


def build():
    steps=[]
    rule='Wolfhawk本人洞悉每轮一次：原004冰攻2真实槽去色Physical，原决心物理格挡2由低效1到足额2；抗性保留，火3加物2有效4击杀；同玩家次回合实际移动'
    def click(path,label,expected=None,capture=False):
        steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,
            skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule=rule)
            for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
    def play(card,choice,expected=None):
        click('Hand/Btn_Card_'+card+'_0','实际原牌')
        click('Inspector/Btn_Choice_'+choice,'原印刷效果')
        click('Inspector/Btn_Play','原费用与真实贡献',expected)
    (click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','Wolfhawk本人原技能有限去色'))
    click('CampShade/Camp/Btn_Loadout','既有种子0资源')
    for _ in range(2):click('CampShade/Picker/Btn_NextPage','下一页')
    click('CampShade/Picker/Btn_Select_39','红蓝各1库存晶，无部队')
    click('CampShade/Camp/Btn_ChooseCards','四张既有原牌')
    p='CampShade/DeedPicker/'
    click(p+'Btn_ClearDeeds','清空旧选择')
    click(p+'Btn_ChooseDeed_basic_card_000','原行进，留作正式次回合消费者')
    click(p+'Btn_DeedNext','原基础第二页')
    for c in ['016','021']:click(p+'Btn_ChooseDeed_basic_card_'+c,'原决心/狂怒')
    click(p+'Btn_DeedKind_1','原高级行动')
    click(p+'Btn_ChooseDeed_advanced_card_000','原火焰箭矢')
    click(p+'Btn_DoneDeeds','四原牌确认')
    click('CampShade/Camp/Btn_SmallDeckHeroBuild','合法本人构筑')
    click('CampShade/HeroBuild/Btn_Hero_Wolfhawk','原英雄Wolfhawk')
    click('CampShade/HeroBuild/Btn_Level_6','原等级6护甲3，仅一个技能')
    click('CampShade/HeroBuild/Btn_Skill_5','本人原洞悉猎物')
    click('CampShade/HeroBuild/Btn_Begin','同玩家合法演练起点',
          {'hero.id':'Wolfhawk','hero.skills':1,'hero.level':6,'hero.armor':3,
           'hero.deckTotal':4,'hand':4,'deck.count':0,'red':1,'blue':1})
    click('Tactic_day_1/Btn_Tactic_day_1','正式昼战术')
    click('Btn_BeginTurn','第一正式回合')
    click('Btn_JourneyEncounter_7','原寒冰巫妖004，冰攻2及原物/冰抗')
    click('Inspector/Btn_Next','格挡阶段')
    play('basic_card_016','block',{'block':2})
    click('Btn_Skills','唯一自身技能页')
    click('Skills/Btn_Skill_0','本人洞悉')
    click('Enemy_0/Btn_Target','真实原巫妖唯一攻击槽')
    click('Inspector/Btn_SkillChoice_200','移除冰元素变物理，非移除抗性',capture=True)
    click('Inspector/Btn_UseSkill','原攻击2去色，保留原防御能力',
          {'enemies.attacks':'2','enemies.armor':'4','skills.used':'True','block':2},True)
    click('Btn_Skills','返回原贡献')
    click('Enemy_0/Btn_Target','同一实际去色攻击')
    click('Inspector/Btn_Resolve','真实物理格挡2足额成立',{'block':0})
    click('Inspector/Btn_Next','正式伤害分配')
    click('Inspector/Btn_Next','实际防御消费，无新增伤',
          {'wounds':0,'attacks.successfullyBlocked':'0:0'},True)
    play('advanced_card_000','strong',{'red':0,'power.attacks':'Ranged:Fire:3'})
    play('basic_card_021','attack',{'power.attacks':'Ranged:Fire:3,Melee:Physical:2'})
    click('Enemy_0/Btn_Target','原物理抗性仍在，同敌实际甲4')
    click('Inspector/Btn_Resolve','火3加物理2经抗性有效1，合4击杀',
          {'kills':1,'fame':40,'wounds':0},True)
    click('Inspector/Btn_Next','正式战斗结束')
    click('Inspector/Btn_EndTurn','原三牌入弃，保留原行进',
          {'journey.completedTurns':1,'journey.completedBattles':1,'hand':1,'journey.discardCount':3})
    click('Btn_BeginTurn','同玩家正式次回合，洞悉每轮次数未恢复')
    click('Btn_JourneyHand','原手牌保留')
    click('JourneyHand/@card=basic_card_000','保留的真实行进')
    click('JourneyHand/Btn_OutsideChoice_base','原基础移动2')
    click('JourneyHand/Btn_PlayOutside','真实出牌移动2',{'movement':2})
    click('JourneyHand/Btn_CloseHand','返回地图')
    click('Btn_JourneyMap','现有演练真实地图')
    click('JourneyMap/Btn_Hex_plains','真实村庄位置')
    click('JourneyMap/Btn_Move','原地形实际费用2',{'movement':0,'journey.position':'0,1'})
    click('JourneyMap/Btn_CloseMap','保留实际位置')
    click('Btn_EndTurn','两正式回合保留本人原牌与无伤结果',
          {'journey.samePlayer':'True','journey.completedTurns':2,'journey.completedBattles':1,
           'hero.id':'Wolfhawk','hero.deckTotal':4,'hand':0,'deck.count':0,'journey.discardCount':4,
           'skills.used':'True','wounds':0,'fame':40,'red':0,'blue':1},True)
    return steps


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--write',action='store_true')
    p.add_argument('--display-only',action='store_true',help='Replay to29; only verify corrected choice panel')
    p.add_argument('--from',dest='start',type=int,default=0)
    p.add_argument('--output',default='AutomationConfigs/pve_battle.json')
    a=p.parse_args();steps=build()
    if not 0 <= a.start <= len(steps):p.error('Invalid --from')
    for s in steps[:a.start]:s['after']=[];s['skipScreenshot']=True
    if a.display_only:
        steps=steps[:30]
        for s in steps:s['after']=[];s['skipScreenshot']=True
        steps[29]['after']=[dict(key='ui.truncated',expected='',rule='三项洞悉选项与目标文字无重叠；原数值消费者沿用有效57/100')]
        steps[29]['skipScreenshot']=False
    if a.write:Path(a.output).write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',
        captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,
        maxRunSeconds=300,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
    print(len(steps),'operations;',sum(len(s['after']) for s in steps),'assertions;',
          sum(not s['skipScreenshot'] for s in steps),'screenshots;', 'written' if a.write else 'plan only')


if __name__=='__main__':main()
