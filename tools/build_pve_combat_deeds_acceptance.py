"""Seven original cards, two actual fights against the same surviving enemy instance."""
import json,sys
from pathlib import Path
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule='实际原材料永久去除、印刷费、伏击首次格挡、真实格挡减甲和反戈；下一回合同一幸存敌人、原仪式攻击') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def card(id):click('Hand/Btn_Card_'+id+'_0','选择实际原牌 '+id,capture=id=='advanced_card_029')
def choose(mode):click('Inspector/Btn_Choice_'+mode,'原分支 '+mode)
def target(i):click('Enemy_'+str(i)+'/Btn_Target','选择实际敌人 '+str(i))
def advance():click('Inspector/Btn_Next','正式阶段推进')
p='CampShade/DeedPicker/'
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','原材料与战斗连续构筑'));click('CampShade/Camp/Btn_Loadout','显式种子资源起点');click('CampShade/Picker/Btn_NextPage','第二页');click('CampShade/Picker/Btn_NextPage','第三页');click('CampShade/Picker/Btn_Select_39','红蓝各一原资源')
click('CampShade/Camp/Btn_ChooseCards','原卡构筑');click(p+'Btn_ClearDeeds','清空选择');click(p+'Btn_DeedNext','基础第二页')
for id in ['basic_card_014','basic_card_021']:click(p+'Btn_ChooseDeed_'+id,'实际原材料 '+id)
click(p+'Btn_DeedKind_1','高级');click(p+'Btn_ChooseDeed_advanced_card_005','寒冰之盾');click(p+'Btn_DeedNext','高级第二页')
for id in ['advanced_card_020','advanced_card_023']:click(p+'Btn_ChooseDeed_'+id,'原牌 '+id)
click(p+'Btn_DeedNext','高级第三页')
for id in ['advanced_card_028','advanced_card_029']:click(p+'Btn_ChooseDeed_'+id,'原牌 '+id)
click(p+'Btn_DoneDeeds','七张原牌');click('CampShade/Camp/Btn_Journey','同一玩家正式旅程');click('Tactic_day_1/Btn_Tactic_day_1','原捷足先登');click('Btn_BeginTurn','第一回合')
click('Btn_JourneyHand','原实际手牌');click('JourneyHand/Btn_Hand_3','分解');click('JourneyHand/Btn_PlayOutside','原分解材料选择');click('DeedInputs/Btn_InputMaterial_1','永久去除实际红色狂怒',capture=True)
click('DeedInputs/Btn_ConfirmInputs','同色两红晶与永久去除区',{'red':3,'removed.ids':'basic_card_021','hand':5},True)
click('JourneyHand/Btn_Hand_2','原伏击');click('JourneyHand/Btn_PlayOutside','移动2及首次攻防许可',{'movement':2});click('JourneyHand/Btn_CloseHand','返回真实旅程')
click('Btn_JourneyMap','实际地图');click('JourneyMap/Btn_Hex_plains','平原村庄');click('JourneyMap/Btn_Move','消费原移动2',{'movement':0,'journey.position':'0,1'});click('JourneyMap/Btn_CloseMap','返回')
click('Btn_JourneyEncounter_2','两名实际哨卫',{'source.enemies':'monster_032,monster_032','journey.samePlayer':'True'});advance();card('advanced_card_005');choose('strong')
click('Inspector/Btn_Play','蓝晶付费，寒冰格挡3加伏击2',{'block':5,'blue':0},True);target(0);click('Inspector/Btn_Resolve','实际格挡第一敌攻击4',{'block':0});advance()
click('Inspector/Btn_Next','第一敌成功格挡减甲，第二敌造成真实两伤',{'wounds':2,'enemies.armor':'1,3'},True)
card('advanced_card_028');choose('base');click('Inspector/Btn_Play','本回合实际格挡一敌，反戈攻击2加2',{'melee':4});target(0);click('Inspector/Btn_Resolve','仅击败已格挡的真实敌人',{'kills':1,'fame':2});advance()
click('Inspector/Btn_EndTurn','留下实际幸存者，保留四张手牌含两伤',{'hand':4,'wounds':2,'red':3,'removed.ids':'basic_card_021','journey.completedTurns':1},True)
click('Btn_BeginTurn','同一玩家第二回合');click('Btn_JourneyEncounter_2','再次交战同一未败原敌',{'source.enemies':'monster_032','journey.samePlayer':'True'});advance();advance();click('Inspector/Btn_Next','实际新增两伤，旧伤保持',{'wounds':4})
card('advanced_card_029');choose('strong');click('Inspector/Btn_Play','原仪式攻击输入');click('DeedInputs/Btn_InputMaterial_0','选择真实蓝色体力材料',capture=True)
click('DeedInputs/Btn_ConfirmInputs','支付分解所得红晶，去除蓝材料得到冰火攻击4',{'red':2,'melee':4,'removed.ids':'basic_card_021,basic_card_014','hand':4},True)
target(0);click('Inspector/Btn_Resolve','真实冰火攻击击败原幸存者',{'kills':1,'fame':4});advance();click('Inspector/Btn_EndTurn','两个真实回合、永久材料不回弃牌或手牌',{'journey.completedTurns':2,'journey.completedBattles':2,'hand':4,'wounds':4,'removed.ids':'basic_card_021,basic_card_014','red':2,'blue':0})
if '--from' in sys.argv:
    for s in steps[:int(sys.argv[sys.argv.index('--from')+1])]:s['after']=[];s['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=270,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
