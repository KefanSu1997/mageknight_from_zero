"""Original opening: real movement, actual rampaging token, persistent turn and healing."""
import json,sys
from pathlib import Path
steps=[]
def click(path,label,expect=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule='原A/1/2七格与原图对应，原牌移动消费、实际肆虐标记击杀和声誉、同玩家跨回合伤牌恢复') for k,v in {'ui.truncated':'',**(expect or {})}.items()]))
def hand():click('Btn_JourneyHand','实际手牌')
def closehand():click('JourneyHand/Btn_CloseHand','返回同一旅程')
def play(index,choice,expected):
    click('JourneyHand/Btn_Hand_'+str(index),'实际原卡');click('JourneyHand/Btn_OutsideChoice_'+choice,'印刷效果');click('JourneyHand/Btn_PlayOutside','真实费用和产出',expected)
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','配置原牌连续路线'))
click('CampShade/Camp/Btn_Loadout','已有可重放原起点')
for _ in range(2):click('CampShade/Picker/Btn_NextPage','下一页')
click('CampShade/Picker/Btn_Select_39','真实红蓝各一晶')
click('CampShade/Camp/Btn_MapMode','选择原起始地图')
click('CampShade/Camp/Btn_ChooseCards','七张原卡')
p='CampShade/DeedPicker/'
click(p+'Btn_ClearDeeds','清空旧构筑')
for c in ['000','001','007','008']:click(p+'Btn_ChooseDeed_basic_card_'+c,'原基本行动')
click(p+'Btn_DeedNext','第二页')
for c in ['014','016','021']:click(p+'Btn_ChooseDeed_basic_card_'+c,'原基本行动')
click(p+'Btn_DoneDeeds','确认原牌')
click('CampShade/Camp/Btn_OriginalHero','原英雄身份')
click('CampShade/HeroStart/Btn_Hero_Tovak','托瓦克')
click('CampShade/HeroStart/Btn_ConfiguredHero','合法等级1自定原牌起点',{'map.originalTiles':3,'map.originalCells':21,'map.landCells':18,'journey.position':'0,0'})
click('Tactic_day_1/Btn_Tactic_day_1','原战术');click('Btn_BeginTurn','正式第一回合')
click('Btn_JourneyMap','查看原起始地图',{'map.rampaging':'1:-4=monster_032,3:-3=monster_007'},True)
click('JourneyMap/Btn_Cell_0_1','原海洋格不可占据',capture=True)
click('JourneyMap/Btn_CloseMap','保留传送门位置',{'journey.position':'0,0'})
hand();play(0,'base',{'movement':2});play(1,'base',{'movement':4});play(2,'strong',{'movement':8,'blue':0});closehand()
click('Btn_JourneyMap','真实原地形移动')
for q,r,left in [(0,-1,6),(0,-2,4),(0,-3,1)]:
    click('JourneyMap/Btn_Cell_'+str(q)+'_'+str(r),'原地图实际相邻格');click('JourneyMap/Btn_Move','支付平原2或白昼森林3',{'journey.position':str(q)+','+str(r),'movement':left},left==1)
click('JourneyMap/Btn_Cell_1_-4','同一原兽人标记')
click('JourneyMap/Btn_MapGuards','公开原敌图与数值',capture=True)
click('MapGuards/Btn_CloseGuards','回到实际相邻位置')
click('JourneyMap/Btn_Challenge','挑战实际兽人并保留玩家',{'movement':0,'journey.samePlayer':'True'},True)
for _ in range(3):click('Inspector/Btn_Next','正式战斗阶段，英雄实际承伤')
click('Hand/Btn_Card_basic_card_021_0','原狂怒')
click('Inspector/Btn_Choice_strong','原红费攻击4');click('Inspector/Btn_Play','实际付红晶',{'red':0,'wounds':2})
click('Enemy_0/Btn_Target','同一原敌人');click('Inspector/Btn_Resolve','原攻击4击杀护甲3',{'kills':1,'fame':2},True)
click('Inspector/Btn_Next','结算原标记与声誉',{'reputation':1,'map.rampaging':'3:-3=monster_007'})
click('Inspector/Btn_EndTurn','正式回合结束',{'journey.completedTurns':1,'journey.completedBattles':1,'wounds':2})
click('Btn_BeginTurn','同一玩家第二回合')
click('Btn_JourneyMap','地图保留真实击杀结果',{'map.rampaging':'3:-3=monster_007','journey.position':'0,-3','map.originalCells':21},True)
click('JourneyMap/Btn_Cell_1_-4','已清空实际原兽人格')
click('JourneyMap/Btn_CloseMap','继续真实战后恢复')
hand();play(0,'heal',{'healing':1,'wounds':2})
click('JourneyHand/Btn_HealHero','原宁静治疗真实战伤',{'healing':0,'wounds':1},True);closehand()
click('Btn_EndTurn','同一玩家保留伤牌和地图战果',{'journey.completedTurns':2,'journey.samePlayer':'True','wounds':1,'reputation':1},True)
if '--from' in sys.argv:
    for s in steps[:int(sys.argv[sys.argv.index('--from')+1])]:s['after']=[];s['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=360,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'assertions')
