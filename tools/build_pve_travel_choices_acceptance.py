"""Same seven original cards, real harvest payment, lake crossing, battle and safe portal."""
import json,sys
from pathlib import Path
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule='原收获随成功移动弃真实牌、魔晶实际强化；迷雾与空间原费用/地形/距离；同玩家战斗与时光传送唯一行动') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def play(index,choice,expected=None,capture=False):
    click('JourneyHand/Btn_Hand_'+str(index),'选择当前真实原牌');click('JourneyHand/Btn_OutsideChoice_'+choice,'原选择 '+choice);click('JourneyHand/Btn_PlayOutside','实际出牌 '+choice,expected,capture)
def hand():click('Btn_JourneyHand','实际手牌')
def closehand():click('JourneyHand/Btn_CloseHand','返回旅程')
def map():click('Btn_JourneyMap','真实地图')
def closemap():click('JourneyMap/Btn_CloseMap','返回旅程')
def select(id):click('JourneyMap/Btn_Hex_'+id,'实际地图目标 '+id)
def move(label,expected=None,capture=False):click('JourneyMap/Btn_Move',label,expected,capture)
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','构筑'));click('CampShade/Camp/Btn_Loadout','既有资源');click('CampShade/Picker/Btn_Select_1','红1蓝2白1绿0')
click('CampShade/Camp/Btn_ChooseCards','选择七原牌');p='CampShade/DeedPicker/';click(p+'Btn_ClearDeeds','清空选择')
for id in ['000','001','003']:click(p+'Btn_ChooseDeed_basic_card_'+id,'原基础 '+id)
click(p+'Btn_DeedNext','基础第二页');click(p+'Btn_ChooseDeed_basic_card_021','原狂怒')
click(p+'Btn_DeedKind_1','高级行动');click(p+'Btn_DeedNext','第二页');click(p+'Btn_DeedNext','第三页');click(p+'Btn_ChooseDeed_advanced_card_032','原时光传送')
click(p+'Btn_DeedKind_2','原法术');click(p+'Btn_DeedNext','法术第二页')
for id in ['014','016']:click(p+'Btn_ChooseDeed_magic_'+id,'原法术 '+id)
click(p+'Btn_DoneDeeds','确认七原牌');click('CampShade/Camp/Btn_Journey','同一玩家旅程');click('Tactic_day_1/Btn_Tactic_day_1','原战术');click('Btn_BeginTurn','正式第一回合')
hand();play(2,'base',{'movement':2,'travel.harvest':1});closehand();map();select('plains')
click('JourneyMap/Btn_Harvest','选择随移动材料');click('HarvestInputs/Btn_Material_1','原绿色宁静作为材料',capture=True);click('HarvestInputs/Btn_Done','保留材料返回地图');move('实际移动弃同一宁静得绿晶',{'movement':0,'green':1,'travel.harvest':0,'journey.position':'0,1','journey.discardCount':1},True);closemap()
hand();play(0,'strong',{'green':0,'movement':4});play(3,'base',{'blue':1,'travel.space':'True'});play(2,'base',{'blue':0,'movement':8});closehand();map();select('mountain');move('原迷雾不允许进入高山',{'rejections':1,'movement':8,'journey.position':'0,1'})
select('lake');move('空间跨两格到真实湖泊，迷雾仍付2',{'movement':6,'journey.position':'-1,0','journey.terrain':'Lake'},True)
select('forest');move('再次跨两格，真实森林也付2',{'movement':4,'journey.position':'1,0','journey.terrain':'Forest'},True);closemap()
click('Btn_JourneyEncounter_0','原兽人战斗');click('Inspector/Btn_Next','格挡');click('Inspector/Btn_Next','分配伤害');click('Inspector/Btn_Next','实际承伤',{'wounds':2});click('Hand/Btn_Card_basic_card_021_0','原狂怒');click('Inspector/Btn_Choice_strong','原红费强化');click('Inspector/Btn_Play','实际支付红晶',{'red':0,'melee':4});click('Enemy_0/Btn_Target','真实敌人');click('Inspector/Btn_Resolve','原攻击击杀护甲3',{'kills':1,'fame':2});click('Inspector/Btn_Next','战斗结束');click('Inspector/Btn_EndTurn','结束第一回合保留两伤，清移动许可',{'journey.completedTurns':1,'travel.space':'False','travel.harvest':0,'movement':0,'wounds':2},True)
click('Btn_BeginTurn','正式第二回合同玩家');hand();play(0,'base',{'travel.portal':'True','travel.range':1,'travel.drawBonus':1},True);select('lake');move('超过一格且湖泊不安全，保留传送选择',{'rejections':2,'journey.position':'1,0','travel.portal':'True'});select('plains');move('传送到已揭示安全村庄且不花移动',{'journey.position':'0,1','travel.portal':'False','movement':0},True);closemap()
click('Btn_JourneyEncounter_1','传送占本回合行动不能再战',{'rejections':3,'journey.completedBattles':1});click('Btn_EndTurn','正式结束传送回合，空库无虚构补牌',{'journey.completedTurns':2,'travel.drawBonus':0,'wounds':2,'hand':2},True)
if '--from' in sys.argv:
    for s in steps[:int(sys.argv[sys.argv.index('--from')+1])]:s['after']=[];s['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
