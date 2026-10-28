"""Actual normal/extra source claims, storm wild black die, and previous-turn discard recovery."""
import json,sys
from pathlib import Path
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='实际普通/额外源骰额度、风暴重掷与黑骰通用许可；上回合实际弃牌经冥想回牌库再正式补牌') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def outside(index):click('JourneyHand/Btn_Hand_'+str(index),'选择实际原牌');click('JourneyHand/Btn_PlayOutside','原效果入口')
def confirm(label,e=None,c=False):click('DeedInputs/Btn_ConfirmInputs',label,e,c)
p='CampShade/DeedPicker/'
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','原魔力与回收构筑'));click('CampShade/Camp/Btn_Loadout','固定重放');click('CampShade/Picker/Btn_NextPage','第二页');click('CampShade/Picker/Btn_NextPage','第三页');click('CampShade/Picker/Btn_Select_39','实际种子0')
click('CampShade/Camp/Btn_ChooseCards','原牌');click(p+'Btn_ClearDeeds','清空待选')
for id in ['basic_card_000','basic_card_009']:click(p+'Btn_ChooseDeed_'+id,'原牌 '+id)
click(p+'Btn_DeedNext','基础第二页')
for id in ['basic_card_014','basic_card_023']:click(p+'Btn_ChooseDeed_'+id,'原牌 '+id)
click(p+'Btn_DeedKind_1','高级');click(p+'Btn_DeedNext','高级第二页')
for id in ['advanced_card_012','advanced_card_022']:click(p+'Btn_ChooseDeed_'+id,'原牌 '+id)
click(p+'Btn_DeedKind_2','原法术');click(p+'Btn_ChooseDeed_magic_002','原冥想神游');click(p+'Btn_DoneDeeds','七张原牌')
click('CampShade/Camp/Btn_Journey','连续旅程');click('Tactic_day_1/Btn_Tactic_day_1','捷足先登');click('Btn_BeginTurn','第一回合',{'source.faces':'White,Blue,White'})
click('Btn_JourneyHand','实际原牌');outside(0);outside(1);outside(0);confirm('原魔力汲取增加一颗额外骰额度',{'source.extra':1,'hand':4});click('JourneyHand/Btn_CloseHand','实际资源保持')
click('Btn_JourneyTools','正式源骰入口');click('JourneyTools/Btn_Source_0','实际白骰');click('JourneyTools/Btn_UseSource','普通额度获得白标',{'tokens.white':1,'source.claims':1,'source.extra':1})
click('JourneyTools/Btn_Source_0','实际蓝骰');click('JourneyTools/Btn_UseExtraSource','额外额度获得蓝标，普通位置不覆盖',{'tokens.blue':1,'source.claims':2,'source.extra':0},True);click('JourneyTools/Btn_CloseTools','回原手牌')
click('Btn_JourneyHand','手牌');outside(2);click('DeedInputs/Btn_InputChoice_strong','风暴强化原文',capture=True)
confirm('付白标，真实重掷剩余骰为黑，额外3颗',{'tokens.white':0,'source.faces':'Black','source.extra':3});click('JourneyHand/Btn_CloseHand','回资源')
click('Btn_JourneyTools','原魔力源');click('JourneyTools/Btn_Source_0','风暴后的原黑骰');click('JourneyTools/Btn_SourceColor_Green','通用黑骰选择绿色');click('JourneyTools/Btn_UseExtraSource','白昼原黑骰按许可提供绿标',{'tokens.green':1,'source.extra':2,'source.claims':3,'journey.source.count':0},True);click('JourneyTools/Btn_CloseTools','返回')
click('Btn_EndTurn','四实际原牌弃牌，临时魔力清空，全部原骰归还',{'journey.completedTurns':1,'journey.discardCount':4,'source.claims':0,'journey.source.count':3,'hand':3,'tokens.green':0})
click('Btn_BeginTurn','下一回合不继承额外骰额度',{'source.extra':0});click('Btn_JourneyHand','真实剩余三牌');outside(1);click('DeedInputs/Btn_InputColor_0_Green','仪式本回合重新选绿');confirm('原鲜血仪式新伤、红晶、绿标',{'wounds':1,'red':2,'tokens.green':1})
outside(1);click('DeedInputs/Btn_InputChoice_bottom','原冥想选择实际牌库底',capture=True);confirm('实际两张上回合弃牌回库并登记下一次补牌加2',{'tokens.green':0,'deck.count':2,'journey.discardCount':2,'draw.nextBonus':2})
click('JourneyHand/Btn_CloseHand','保留同一实际玩家');click('Btn_EndTurn','正式补牌回手，实际两张回收牌可继续使用',{'journey.completedTurns':2,'hand':4,'deck.count':0,'journey.discardCount':4,'draw.nextBonus':0,'wounds':1})
click('Btn_BeginTurn','第三回合保留回收原牌');click('Btn_JourneyHand','查看本局实际回收牌',{'hand':4},True)
if '--from' in sys.argv:
    for s in steps[:int(sys.argv[sys.argv.index('--from')+1])]:s['after']=[];s['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=250,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
