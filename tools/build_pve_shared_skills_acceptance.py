"""Two legal heroes share an original central token in real alternating turns."""
import json,sys
from pathlib import Path
steps=[]
def click(path,label,expected=None,capture=False):
 steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule='不同原英雄合法技能；正式战术顺序与持续玩家、中央标记实际领取') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','双英雄构筑'));click('CampShade/Camp/Btn_Loadout','原牌构筑')
for _ in range(2):click('CampShade/Picker/Btn_NextPage','下一页')
click('CampShade/Picker/Btn_Select_39','零部队')
p='CampShade/DeedPicker/'
click('CampShade/Camp/Btn_ChooseCards','两英雄独立原牌');click(p+'Btn_ClearDeeds','原牌选择')
for card in ['000','001','007','008']:click(p+'Btn_ChooseDeed_basic_card_'+card,'原基础牌')
click(p+'Btn_DeedNext','下一页')
for card in ['014','021','022']:click(p+'Btn_ChooseDeed_basic_card_'+card,'原基础牌')
click(p+'Btn_DoneDeeds','确认');click('CampShade/Camp/Btn_SmallDeckHeroBuild','按原英雄技能构筑')
h='CampShade/HeroBuild/'
click(h+'Btn_Hero_Norowas','诺洛瓦斯');click(h+'Btn_Level_2','合法等级2');click(h+'Btn_Skill_9','原共享平静');click(h+'Btn_AddCompanion','保留第一位实际玩家')
click(h+'Btn_Hero_Arythea','阿瑞希亚');click(h+'Btn_Skill_9','原分享痛苦');click(h+'Btn_Begin','两位实际玩家',{'journey.players':2},True)
click('Tactic_day_1/Btn_Tactic_day_1','第一英雄战术');click('Tactic_day_4/Btn_Tactic_day_4','第二英雄战术');click('Btn_BeginTurn','第一位正式回合',{'hero.id':'Norowas','journey.playerId':0})
click('Btn_JourneyTools','当前回合工具');click('JourneyTools/Btn_SharedSkills','原共享区');click('SharedSkills/Btn_Own_0','本人原技能');click('SharedSkills/Btn_Confirm','发动共享平静',{'shared.count':1,'shared.ids':'hero_skill_pool_002:09'},True)
click('SharedSkills/Btn_Back','返回');click('JourneyTools/Btn_CloseTools','回到旅程');click('Btn_JourneyHand','实际手牌');click('JourneyHand/Btn_Hand_0','原牌');click('JourneyHand/Btn_OutsideChoice_base','原基础效果');click('JourneyHand/Btn_PlayOutside','正式出牌');click('JourneyHand/Btn_CloseHand','返回');click('Btn_EndTurn','正式结束首回合',{'journey.completedTurns':1,'shared.count':1})
click('Btn_BeginTurn','第二位实际回合',{'hero.id':'Arythea','journey.playerId':1});click('Btn_JourneyTools','第二玩家');click('JourneyTools/Btn_SharedSkills','同一中央区');click('SharedSkills/Btn_Token_0','真实第一英雄标记');click('SharedSkills/Btn_Confirm','第二位领取共享平静',{'shared.count':0},True)
click('SharedSkills/Btn_Back','返回实际手牌');click('JourneyTools/Btn_CloseTools','返回旅程');click('Btn_JourneyHand','第二英雄原手牌');click('JourneyHand/Btn_Hand_0','原步行');click('JourneyHand/Btn_OutsideChoice_base','原移动2');click('JourneyHand/Btn_PlayOutside','第二英雄产生实际移动',{'movement':2});click('JourneyHand/Btn_CloseHand','地图消费');click('Btn_JourneyMap','实际地图');click('JourneyMap/Btn_Hex_forest','原森林白昼费用3');click('JourneyMap/Btn_Move','中央平静实际减费1，支付2进入森林',{'movement':0,'journey.position':'1,0'},True)
if '--from' in sys.argv:
 for step in steps[:int(sys.argv[sys.argv.index('--from')+1])]:step['after']=[];step['skipScreenshot']=True
steps[42]['skipScreenshot']=False
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps))
