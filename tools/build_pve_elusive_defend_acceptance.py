"""Successful original Ice Bats / two distinct Sentries combat, then formal round.
Default plan only. --write is explicit; --from replays without old evidence.
"""
import argparse
import json
from pathlib import Path


def build():
    steps=[]
    rule='原057 Elusive双甲1/2、Defend1真实分配给两同名032之一；原远程7付红蓝各1击杀两032，两冰1全部格挡后原近战击杀蝠，正式跨轮保留原六牌与资源'
    def c(path,label,expected=None,shot=False):
        steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not shot,
            after=[dict(key=k,expected=str(v),rule=rule) for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
    def play(card,choice,expected=None):
        c('Hand/Btn_Card_'+card+'_0','实际原牌 '+card)
        c('Inspector/Btn_Choice_'+choice,'原印刷模式 '+choice)
        c('Inspector/Btn_Play','正式原费与原贡献',expected)
    (c('Btn_Menu', '打开战斗菜单'), c('Btn_Camp','可复用原敌遭遇连续构筑'))
    c('CampShade/Camp/Btn_Loadout','既有HornGold起点')
    for _ in range(2):c('CampShade/Picker/Btn_NextPage','既有构筑目录下一页')
    c('CampShade/Picker/Btn_Select_39','红蓝晶各1，无单位，无免费新资源')
    c('CampShade/Camp/Btn_ChooseCards','六原牌合法自定构筑，非标准16牌开局')
    p='CampShade/DeedPicker/'
    c(p+'Btn_ClearDeeds','清除待选构筑')
    for id in ['010','011']:c(p+'Btn_ChooseDeed_basic_card_'+id,'原迅捷反应/利斧投掷')
    c(p+'Btn_DeedNext','基本第二页')
    for id in ['016','021']:c(p+'Btn_ChooseDeed_basic_card_'+id,'原决心/狂怒')
    c(p+'Btn_DeedKind_1','原高级行动第一页')
    c(p+'Btn_ChooseDeed_advanced_card_000','原火焰箭矢')
    for _ in range(2):c(p+'Btn_DeedNext','高级下一页')
    c(p+'Btn_ChooseDeed_advanced_card_034','原骑士精神')
    c(p+'Btn_DoneDeeds','六张原牌')
    c('CampShade/Camp/Btn_SmallDeckHeroBuild','合法原英雄等级')
    c('CampShade/HeroBuild/Btn_Hero_Wolfhawk','原Wolfhawk，迅捷反应所属英雄')
    c('CampShade/HeroBuild/Btn_Level_6','实际等级6名望35手限6')
    c('CampShade/HeroBuild/Btn_Begin','同一实际玩家开局',{'hero.id':'Wolfhawk','hero.level':6,'hero.skills':0,'units.count':0,'fame':35,'hand':6,'red':1,'blue':1})
    c('Tactic_day_1/Btn_Tactic_day_1','正式白昼战术')
    c('Btn_BeginTurn','正式第一回合')
    c('Btn_JourneyEncounter_13','原057与两个独立032，可复用战斗遭遇非原图探索',{'enemies.armor':'2,3,3','enemies.defendApplied':'','kills':0,'wounds':0},True)
    play('basic_card_010','ranged',{'ranged':1,'red':1,'blue':1})
    play('basic_card_011','strong',{'ranged':4,'red':1,'blue':0})
    play('advanced_card_000','strong',{'ranged':7,'red':0,'blue':0})
    c('Enemy_1/Btn_Target','第一实际032')
    c('Enemy_2/Btn_Target','第二独立同名032')
    c('Inspector/Btn_Resolve','同组攻击需要确定唯一防护对象',{'ranged':7,'kills':0,'enemies.defendApplied':''})
    c('EnemyDefend/Btn_Target_1','057来源为第一032提供防护，不混淆同名实体',{'enemies.defendSelected':'0>1:1','enemies.defendApplied':'','enemies.armor':'2,3,3','ranged':7},True)
    c('EnemyDefend/Btn_Confirm','正式远程7消费击杀甲4+3两敌',{'enemies.defendApplied':'0>1:1','enemies.armor':'2,4,3','ranged':0,'kills':2,'fame':40,'red':0,'blue':0})
    c('Inspector/Btn_Next','实际格挡阶段，蝠两次冰攻击尚存')
    play('basic_card_016','block',{'block':2})
    c('Enemy_0/Btn_Target','蝠第一个冰1攻击')
    c('Inspector/Btn_Resolve','物理格挡2折半为1挡第一个冰1',{'block':0,'enemies.armor':'2,4,3'})
    play('basic_card_021','block',{'block':2})
    c('Enemy_0/Btn_Target','重新选蝠，攻击序号重置为0')
    c('Inspector/Btn_AttackSlot','实际选择第二个冰1攻击')
    c('Inspector/Btn_Resolve','物理格挡2折半为1挡第二个冰1',{'block':0})
    c('Inspector/Btn_Next','正式分配伤害阶段')
    c('Inspector/Btn_Next','两次全挡，无伤进入近战，原低甲1',{'attacks.successfullyBlocked':'0:0,0:1','enemies.armor':'1,4,3','wounds':0,'kills':2,'enemies.defendApplied':'0>1:1'},True)
    play('advanced_card_034','base',{'melee':3,'red':0,'blue':0})
    c('Enemy_0/Btn_Target','同一原蝠，防护此前已给另一敌人')
    c('Inspector/Btn_Resolve','原攻击3实际击杀低甲1蝠',{'kills':3,'fame':42,'wounds':0})
    c('Inspector/Btn_Next','完成实际战斗')
    c('Inspector/Btn_EndTurn','六牌正式弃牌，同一玩家战果',{'journey.completedTurns':1,'journey.completedBattles':1,'journey.discardCount':6,'hand':0,'deck.count':0,'red':0,'blue':0,'wounds':0,'journey.samePlayer':'True'})
    c('Btn_Announce','真实空库声明轮末，不能直接再次开回合')
    c('Btn_EndRound','正式六原牌回收洗回',{'deck.count':6,'journey.dayPart':'Night'})
    c('Btn_PrepareRound','正式抽回同六牌，不重建构筑',{'hand':6,'red':0,'blue':0,'journey.samePlayer':'True'})
    c('Tactic_night_1/Btn_Tactic_night_1','正式黑夜战术')
    c('Btn_BeginTurn','同玩家下一轮开始且已花晶不恢复',{'journey.completedTurns':1,'journey.completedBattles':1,'journey.samePlayer':'True','hand':6,'red':0,'blue':0,'fame':42,'wounds':0},True)
    return steps


def main():
    p=argparse.ArgumentParser();p.add_argument('--write',action='store_true');p.add_argument('--from',dest='start',type=int,default=0);p.add_argument('--output',default='AutomationConfigs/pve_battle.json');a=p.parse_args()
    steps=build()
    if not 0<=a.start<=len(steps):p.error('invalid --from')
    for s in steps[:a.start]:s['after']=[];s['skipScreenshot']=True
    if a.write:Path(a.output).write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=480,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
    print(len(steps),'operations;',sum(len(s['after']) for s in steps),'assertions;',sum(not s['skipScreenshot'] for s in steps),'captures;', 'written' if a.write else 'plan only')
if __name__=='__main__':main()
