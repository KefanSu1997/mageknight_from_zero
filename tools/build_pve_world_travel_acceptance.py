"""Original tunnel and wings movement, then actual battle and next-turn healing."""
import json,sys
from pathlib import Path
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule='原地道不花移动及飞翼逐格扣移动，真实原晶标费用，后续战斗承伤及同一玩家次回合治疗') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def play(i,choice='base'):
    click('JourneyHand/Btn_Hand_'+str(i),'实际手牌实例');click('JourneyHand/Btn_OutsideChoice_'+choice,'原半面 '+choice);click('JourneyHand/Btn_PlayOutside','正式出牌')
def confirm(label,expected=None,capture=False):click('DeedInputs/Btn_ConfirmInputs',label,expected,capture)
p='CampShade/DeedPicker/'
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','连续移动与战斗'));click('CampShade/Camp/Btn_Loadout','合法起点');click('CampShade/Picker/Btn_NextPage','第二页');click('CampShade/Picker/Btn_NextPage','第三页');click('CampShade/Picker/Btn_Select_39','种子0红蓝各1')
click('CampShade/Camp/Btn_ChooseCards','原卡构筑');click(p+'Btn_ClearDeeds','清空选择');click(p+'Btn_ChooseDeed_basic_card_000','原行进');click(p+'Btn_ChooseDeed_basic_card_001','原宁静');click(p+'Btn_DeedNext','基本第二页');click(p+'Btn_ChooseDeed_basic_card_021','原狂怒')
click(p+'Btn_DeedKind_2','原法术');click(p+'Btn_ChooseDeed_magic_004','地道');click(p+'Btn_DeedNext','第二页');click(p+'Btn_ChooseDeed_magic_023','清风之翼');click(p+'Btn_DeedKind_3','原神器');click(p+'Btn_ChooseDeed_items_006','钻石戒指');click(p+'Btn_ChooseDeed_items_007','翡翠戒指');click(p+'Btn_DoneDeeds','七原实例');click('CampShade/Camp/Btn_Journey','实际旅程');click('Tactic_day_1/Btn_Tactic_day_1','原战术');click('Btn_BeginTurn','正式第一回合')
click('Btn_JourneyHand','实际手牌');click('JourneyHand/Btn_Hand_5','钻石戒指');click('JourneyHand/Btn_ArtifactNormal','正常');click('JourneyHand/Btn_PlayOutside','白晶与白标分别产生',{'tokens.white':1,'white':1});click('JourneyHand/Btn_Hand_5','翡翠戒指');click('JourneyHand/Btn_PlayOutside','绿晶与绿标分别产生',{'tokens.green':1,'green':1})
play(3);click('DeedInputs/Btn_Path_mountain','穿过已揭示山脉');click('DeedInputs/Btn_Path_desert','终点安全沙漠',capture=True);confirm('地道绿标费用，不产生也不扣移动',{'journey.position':'1,-1','movement':0,'green':1,'tokens.green':0,'world.tunnel':'False'},True)
play(0,'strong');click('JourneyHand/Btn_CloseHand','强化行进实际支付绿晶并产生4移动',{'movement':4,'green':0});click('Btn_JourneyHand','原飞翼')
play(2);click('DeedInputs/Btn_Path_mountain','第一格山脉');click('DeedInputs/Btn_Path_lake','第二格湖泊');click('DeedInputs/Btn_Path_camp','第三格平原');click('DeedInputs/Btn_Path_plains','第四格安全平原',capture=True);confirm('飞翼白标加4移动，越山湖仍逐格付费',{'journey.position':'0,1','movement':0,'tokens.white':0,'white':1,'hand':2},True)
click('JourneyHand/Btn_CloseHand','同一真实玩家继续战斗');click('Btn_JourneyEncounter_0','原兽人');click('Inspector/Btn_Next','格挡');click('Inspector/Btn_Next','承伤');click('Inspector/Btn_Next','兽人实际承伤',{'wounds':2});click('Hand/Btn_Card_basic_card_021_0','原狂怒');click('Inspector/Btn_Choice_strong','原红费强化');click('Inspector/Btn_Play','实际扣红晶产生攻击4',{'red':0,'melee':4});click('Enemy_0/Btn_Target','同一兽人');click('Inspector/Btn_Resolve','实际击杀并消费',{'kills':1,'melee':0});click('Inspector/Btn_Next','战斗结束');click('Inspector/Btn_EndTurn','正式回合结束保留位置与真实伤牌',{'journey.completedTurns':1,'journey.position':'0,1','wounds':2,'movement':0},True)
click('Btn_BeginTurn','同玩家下一回合');click('Btn_JourneyHand','保留原宁静和新伤');play(0,'heal');click('JourneyHand/Btn_HealHero','原宁静消费一点治疗，仅移除一张伤',{'wounds':1,'healing':0,'hand':1},True);click('JourneyHand/Btn_CloseHand','返回同旅程');click('Btn_EndTurn','第二回合保留另一真实伤牌',{'journey.completedTurns':2,'journey.completedBattles':1,'wounds':1,'journey.position':'0,1'},True)
if '--from' in sys.argv:
    for s in steps[:int(sys.argv[sys.argv.index('--from')+1])]:s['after']=[];s['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
