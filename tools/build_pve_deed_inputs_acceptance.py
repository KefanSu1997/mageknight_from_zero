"""One persistent mana-source, printed fees, combat, crystal refund and actual discard consumer."""
import json,sys
from pathlib import Path
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原卡实际骰、独立选色、印刷费用、材料与战斗/移动消费；同玩家跨回合不重建') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def panel(choice):click('DeedInputs/Btn_InputChoice_'+choice,'选择原分支 '+choice)
def color(n,c):click('DeedInputs/Btn_InputColor_'+str(n)+'_'+c,'第'+str(n+1)+'枚选'+c)
def outside(index):click('JourneyHand/Btn_Hand_'+str(index),'选择实际原手牌');click('JourneyHand/Btn_PlayOutside','展开原效果选择')
def confirm(label,e=None,c=False):click('DeedInputs/Btn_ConfirmInputs',label,e,c)
p='CampShade/DeedPicker/'
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','同一七牌连续资源构筑'));click('CampShade/Camp/Btn_Loadout','显式种子重放');click('CampShade/Picker/Btn_NextPage','第二页');click('CampShade/Picker/Btn_NextPage','第三页');click('CampShade/Picker/Btn_Select_39','种子0原骰与供应')
click('CampShade/Camp/Btn_ChooseCards','选原卡');click(p+'Btn_ClearDeeds','清除原选择');click(p+'Btn_ChooseDeed_basic_card_009','原魔力汲取');click(p+'Btn_DeedNext','基础第二页')
for id in ['basic_card_013','basic_card_023']:click(p+'Btn_ChooseDeed_'+id,'原牌 '+id)
click(p+'Btn_DeedKind_1','高级行动');click(p+'Btn_DeedNext','高级第二页')
for id in ['advanced_card_012','advanced_card_013','advanced_card_021','advanced_card_022']:click(p+'Btn_ChooseDeed_'+id,'原牌 '+id)
click(p+'Btn_DoneDeeds','确认七张实际原牌');click('CampShade/Camp/Btn_Journey','同一玩家旅程');click('Tactic_day_1/Btn_Tactic_day_1','捷足先登');click('Btn_BeginTurn','第一回合')
click('Btn_JourneyHand','战外手牌');outside(0);panel('strong');color(0,'Blue');click('DeedInputs/Btn_InputDie_0','选择实际骰，转蓝得两标',capture=True)
confirm('支付蓝晶，实际骰保留到回合末',{'blue':0,'tokens.blue':2,'source.claims':1,'journey.source.count':2})
outside(4);panel('strong');confirm('蓝标记支付魔晶精通，记录本回合返还',{'tokens.blue':1,'crystals.return':'True'})
outside(2);color(0,'White');confirm('鲜血仪式真实新伤、红晶与白标',{'wounds':1,'red':2,'tokens.white':1},True)
outside(0);panel('strong');color(0,'Red');color(1,'Green');click('DeedInputs/Btn_InputDie_0','第一枚实际余骰');click('DeedInputs/Btn_InputDie_1','第二枚不同实际余骰')
confirm('白标支付征引，实际两骰转红绿',{'tokens.white':0,'tokens.red':1,'tokens.green':1,'source.claims':3,'journey.source.count':0},True)
click('JourneyHand/Btn_CloseHand','保留资源进入战斗');click('Btn_JourneyEncounter_0','真实遗迹哨卫');click('Inspector/Btn_Next','格挡');click('Inspector/Btn_Next','承伤');click('Inspector/Btn_Next','原护甲实际两伤',{'wounds':3})
click('Hand/Btn_Card_advanced_card_013_0','原纯净魔法');click('Inspector/Btn_Choice_strong_attack','原强化另付红攻击');click('Inspector/Btn_Play','原输入面板')
confirm('蓝印刷费加红额外费原子支付，攻击7',{'tokens.blue':0,'tokens.red':0,'melee':7,'red':2},True)
click('Enemy_0/Btn_Target','原敌人');click('Inspector/Btn_Resolve','消费原攻击实际击杀',{'kills':1,'fame':2});click('Inspector/Btn_Next','结束战斗');click('Inspector/Btn_EndTurn','本回合真蓝晶返还、三骰原实例归还',{'blue':1,'red':2,'source.claims':0,'journey.source.count':3,'source.faces':'Blue,Red,Green','hand':5,'journey.completedTurns':1},True)
click('Btn_BeginTurn','同一玩家下一回合');click('Btn_JourneyHand','实际剩余手牌');outside(0);panel('strong_move');click('DeedInputs/Btn_InputMaterial_0','原魔力风暴作为实际非伤材料',capture=True)
confirm('红晶支付随机应变，弃实际材料换移动5',{'red':1,'movement':5,'hand':3,'journey.discardCount':6})
click('JourneyHand/Btn_CloseHand','返回');click('Btn_JourneyMap','实际地形');click('JourneyMap/Btn_Hex_plains','原平原村庄');click('JourneyMap/Btn_Move','真实移动消费2',{'journey.position':'0,1','movement':3},True);click('JourneyMap/Btn_CloseMap','返回')
click('Btn_EndTurn','新回合不继承旧返晶许可',{'red':1,'blue':1,'journey.completedTurns':2,'journey.discardCount':7,'wounds':3})
if '--from' in sys.argv:
    for s in steps[:int(sys.argv[sys.argv.index('--from')+1])]:s['after']=[];s['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=260,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
