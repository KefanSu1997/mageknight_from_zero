"""Original artifacts outside battle -> actual upper/lower spells -> same turn cleanup."""
import json,sys
from pathlib import Path
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原神器实体/魔晶与标记分离、昼夜许可、正式原法术费用与结算、回合清理') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
picker='CampShade/DeedPicker/'
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','原神器与法术连续起点'));click('CampShade/Camp/Btn_Loadout','复用既有起点')
click('CampShade/Picker/Btn_Select_0','初始红蓝各1，无免费黑白魔力')
click('CampShade/Camp/Btn_ChooseCards','点选实际原卡');click(picker+'Btn_ClearDeeds','清除旧原牌选择')
click(picker+'Btn_DeedKind_3','原神器目录')
for id in ['items_004','items_005','items_006']:click(picker+'Btn_ChooseDeed_'+id,'选择原戒指 '+id)
click(picker+'Btn_DeedNext','下一页原神器');click(picker+'Btn_ChooseDeed_items_015','选择黑暗护符')
click(picker+'Btn_DeedKind_2','原法术目录');click(picker+'Btn_ChooseDeed_magic_007','选择拆毁/解离')
click(picker+'Btn_DeedNext','第二页法术');click(picker+'Btn_ChooseDeed_magic_013','选择冰冻/致命冰冻');click(picker+'Btn_ChooseDeed_magic_022','选择旋风/飓风')
click(picker+'Btn_DoneDeeds','确认七张原牌');click('CampShade/Camp/Btn_Journey','开始真实连续旅程')
click('Tactic_day_1/Btn_Tactic_day_1','原白昼战术');click('Btn_BeginTurn','实际第一回合')
click('Btn_JourneyHand','战斗外使用原神器',{'hand':7,'blue':1,'white':0})
click('JourneyHand/Btn_Hand_1','选蓝宝石戒指原图');click('JourneyHand/Btn_ArtifactNormal','正常效果')
click('JourneyHand/Btn_PlayOutside','蓝色标记与库存蓝晶分开增加',{'blue':2,'tokens.blue':1,'fame':1,'played':1},True)
click('JourneyHand/Btn_Hand_1','选白宝石戒指原图');click('JourneyHand/Btn_PlayOutside','取得白色标记和白晶',{'white':1,'tokens.white':1,'fame':2})
click('JourneyHand/Btn_Hand_1','选实际黑暗护符')
for _ in range(5):click('JourneyHand/Btn_ArtifactColor_0','选择这颗标记的实际颜色')
click('JourneyHand/Btn_PlayOutside','获得黑色并开放白昼下半法术',{'tokens.black':1,'mana.allowBlackAtDay':'True','played':3},True)
click('JourneyHand/Btn_Hand_0','选实际红宝石戒指');click('JourneyHand/Btn_ArtifactBurn','选择焚毁原神器')
click('JourneyHand/Btn_PlayOutside','永久移除红戒，获得本回合无尽红黑',{'journey.removedCount':1,'mana.infinite':'Red,Black','hand':3,'fame':2},True)
click('JourneyHand/Btn_CloseHand','回到实际回合');click('Btn_JourneyEncounter_2','同一手牌和魔力进入双原敌战斗')
def play(id,mode,target,expected=None,capture=False):
    click('Hand/Btn_Card_'+id+'_0','选择原法术 '+id);click('Inspector/Btn_Choice_'+mode,'选择印刷法术半面')
    click(f'Enemy_{target}/Btn_Target','选择实际原敌 #'+str(target+1));click('Inspector/Btn_Play','实际费用与法术结算',expected,capture)
def advance():click('Inspector/Btn_Next','推进正式战斗阶段')
play('magic_013','base',0,{'tokens.blue':0,'blue':2,'enemies.skipped':'0','hand':2},True)
advance();advance();click('Inspector/Btn_Next','仅另一实际敌人造成两张伤牌',{'wounds':2,'hand':4})
play('magic_007','strong',1,{'kills':1,'fame':5,'red':1,'tokens.black':1,'enemies.armor':'2,3'},True)
play('magic_022','strong',0,{'kills':2,'fame':7,'tokens.white':0,'white':1,'wounds':2})
advance();click('Inspector/Btn_EndTurn','正式结束回合：清掉许可与临时魔力，保留库存和原伤牌',{'journey.completedTurns':1,'journey.completedBattles':1,'hand':2,'played':0,'journey.removedCount':1,'journey.discardCount':6,'hero.deckTotal':8,'mana.infinite':'','mana.allowBlackAtDay':'False','tokens.black':0,'blue':2,'white':1,'wounds':2},True)
if '--from' in sys.argv:
    for step in steps[:int(sys.argv[sys.argv.index('--from')+1])]:step['after']=[];step['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=240,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
