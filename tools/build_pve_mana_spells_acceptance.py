"""Original source reservation, actual spell costs and persistent two-turn combat."""
import json,sys
from pathlib import Path
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule='宣夺实际源骰至轮末、延期下回合开始；原费先支付、辐射按旧库存受伤再得晶、真实原卡战斗及治疗消费') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def play(index,choice='base'):
    click('JourneyHand/Btn_Hand_'+str(index),'实际手牌实例');click('JourneyHand/Btn_OutsideChoice_'+choice,'原效果 '+choice);click('JourneyHand/Btn_PlayOutside','正式原出牌入口')
def color(c):click('DeedInputs/Btn_InputColor_0_'+c,'选择原基本色 '+c)
def confirm(label,expected=None,capture=False):click('DeedInputs/Btn_ConfirmInputs',label,expected,capture)
p='CampShade/DeedPicker/'
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','原法术跨回合连续构筑'));click('CampShade/Camp/Btn_Loadout','显式原骰种子');click('CampShade/Picker/Btn_NextPage','第二页');click('CampShade/Picker/Btn_NextPage','第三页');click('CampShade/Picker/Btn_Select_39','复用种子0，红蓝各1')
click('CampShade/Camp/Btn_ChooseCards','选择七原牌');click(p+'Btn_ClearDeeds','清除待选');click(p+'Btn_ChooseDeed_basic_card_001','原宁静');click(p+'Btn_DeedNext','第二页');click(p+'Btn_ChooseDeed_basic_card_021','原狂怒')
click(p+'Btn_DeedKind_2','原法术');click(p+'Btn_ChooseDeed_magic_006','原熔毁辐射');click(p+'Btn_DeedNext','法术第二页')
for id in ['012','015','018']:click(p+'Btn_ChooseDeed_magic_'+id,'原法术 '+id)
click(p+'Btn_DeedKind_3','原神器');click(p+'Btn_DeedNext','神器第二页');click(p+'Btn_ChooseDeed_items_015','原黑暗护符');click(p+'Btn_DoneDeeds','七原牌');click('CampShade/Camp/Btn_Journey','实际旅程');click('Tactic_day_1/Btn_Tactic_day_1','原战术');click('Btn_BeginTurn','第一回合',{'source.available':'White,Blue,White'})
click('Btn_JourneyHand','原手牌');play(3,'base_deferred');click('DeedInputs/Btn_ClaimColor_Blue','选择实际蓝色源骰',capture=True);confirm('付蓝晶，实际源骰保留至轮末，本回合尚无收益',{'blue':0,'tokens.blue':0,'source.roundClaims':'Blue','source.available':'White,White','source.perTurn':'Blue:1','source.ordinaryUsed':'False'},True)
click('JourneyHand/Btn_CloseHand','保留宣夺，普通取骰仍可用');click('Btn_JourneyTools','真实公共魔力源');click('JourneyTools/Btn_Source_0','实际白骰');click('JourneyTools/Btn_UseSource','普通白骰正常取得',{'tokens.white':1,'source.ordinaryUsed':'True','source.roundClaims':'Blue'},True);click('JourneyTools/Btn_CloseTools','回到原手牌');click('Btn_JourneyHand','使用新白色魔力')
play(4);color('Red');confirm('思维读取白费换红晶；当前单人无其他目标',{'tokens.white':0,'red':2,'hand':5},True)
click('JourneyHand/Btn_Hand_4','实际黑暗护符');click('JourneyHand/Btn_ArtifactNormal','护符普通原效果')
for _ in range(5):click('JourneyHand/Btn_ArtifactColor_0','原选择改为黑色')
click('JourneyHand/Btn_PlayOutside','获得黑标并按原护符允许白昼强化',{'tokens.black':1,'mana.allowBlackAtDay':'True'})
play(2,'strong');color('Red');confirm('辐射先付红黑，旧红库存1受伤，再获两红晶',{'tokens.black':0,'red':3,'wounds':1,'hand':4},True)
click('JourneyHand/Btn_CloseHand','保留三原牌和新伤');click('Btn_EndTurn','实际第一回合结束，普通骰归还，宣夺骰仍保留',{'journey.completedTurns':1,'source.roundClaims':'Blue','journey.source.count':2,'tokens.blue':0,'mana.allowBlackAtDay':'False','hand':4})
click('Btn_BeginTurn','正式下一回合首次得到延期蓝魔力',{'tokens.blue':1,'source.perTurn':'Blue:1','red':3,'wounds':1},True)
click('Btn_JourneyHand','原宁静实际恢复');play(0,'heal');click('JourneyHand/Btn_HealHero','消费原治疗清除辐射新伤',{'wounds':0,'healing':0,'hand':2})
click('JourneyHand/Btn_CloseHand','同一真实玩家进入战斗');click('Btn_JourneyEncounter_2','两个原敌人')
click('Hand/Btn_Card_magic_015_0','寒冰原法术');click('Inspector/Btn_Choice_base','原寒冰远程');click('Inspector/Btn_Play','延期蓝标实际支付原法术',{'tokens.blue':0,'ranged':5},True);click('Enemy_0/Btn_Target','第一实际敌人');click('Inspector/Btn_Resolve','寒冰远程实际击杀',{'kills':1})
click('Inspector/Btn_Next','正式格挡阶段');click('Inspector/Btn_Next','正式承伤阶段');click('Inspector/Btn_Next','幸存敌人实际造成新伤')
click('Hand/Btn_Card_basic_card_021_0','原狂怒');click('Inspector/Btn_Choice_strong','实际红晶强化');click('Inspector/Btn_Play','辐射所得红晶支付原狂怒',{'red':2,'melee':4},True);click('Enemy_1/Btn_Target','第二实际敌人');click('Inspector/Btn_Resolve','原攻击消费并击杀',{'kills':2,'melee':0});click('Inspector/Btn_Next','正式战斗结束');click('Inspector/Btn_EndTurn','第二回合结束，原伤牌与库存保留',{'journey.completedTurns':2,'journey.completedBattles':1,'source.roundClaims':'Blue','tokens.blue':0})
click('Btn_Announce','牌库空，正式声明轮末');click('Btn_EndRound','正式轮末归还宣夺原骰并清理延期收益',{'source.roundClaims':'','source.perTurn':'','journey.source.count':3,'journey.dayPart':'Night','red':2},True)
if '--from' in sys.argv:
    for s in steps[:int(sys.argv[sys.argv.index('--from')+1])]:s['after']=[];s['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
