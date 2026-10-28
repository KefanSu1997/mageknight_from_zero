"""Same seven original cards: real supplied freeze, borrowed attack and resumable recovery."""
import json,sys
from pathlib import Path
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule='原材料实例弃牌/永久去除；供应许可正常付费且牌留供应；原血脉红费借强效并伤入手；过载逐次选择正式效果与实际治疗、回合牌区守恒') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def play(index,choice='base'):
    click('JourneyHand/Btn_Hand_'+str(index),'当前真实手牌');click('JourneyHand/Btn_OutsideChoice_'+choice,'原模式 '+choice);click('JourneyHand/Btn_PlayOutside','原出牌入口')
def nextcopy():click('PendingCopy/Btn_ContinueOriginal','按当前状态选择下一次原效果')
p='CampShade/DeedPicker/'
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','复制与供应连续构筑'));click('CampShade/Camp/Btn_Loadout','显式可重放供应');click('CampShade/Picker/Btn_NextPage','第二页');click('CampShade/Picker/Btn_NextPage','第三页');click('CampShade/Picker/Btn_Select_39','原显式种子0与红蓝各1')
click('CampShade/Camp/Btn_ChooseCards','七原牌');click(p+'Btn_ClearDeeds','清空待选');click(p+'Btn_ChooseDeed_basic_card_001','原宁静');click(p+'Btn_DeedNext','基础第二页')
for id in ['014','021']:click(p+'Btn_ChooseDeed_basic_card_'+id,'原基础 '+id)
click(p+'Btn_DeedKind_1','高级行动');click(p+'Btn_DeedNext','第二页');click(p+'Btn_DeedNext','第三页')
for id in ['024','025','030','033']:click(p+'Btn_ChooseDeed_advanced_card_'+id,'原高级 '+id)
click(p+'Btn_DoneDeeds','保留七个原实例');click('CampShade/Camp/Btn_Journey','正式旅程');click('Tactic_day_1/Btn_Tactic_day_1','原战术');click('Btn_BeginTurn','第一回合',{'supply.advanced':'advanced_card_021,advanced_card_028,advanced_card_009','supply.spells':'magic_013,magic_000,magic_023'})
click('Btn_JourneyHand','真实手牌');click('JourneyHand/Btn_NextHand','第七张法术熔炉');play(6);click('DeedInputs/Btn_InputOffer_0','实际供应蓝色冰冻');click('DeedInputs/Btn_ConfirmInputs','熔炉得原蓝晶，供应不动',{'blue':2,'hand':6})
play(4);click('DeedInputs/Btn_InputMaterial_1','实际蓝色耐力作为天赋材料',capture=True);click('DeedInputs/Btn_ConfirmInputs','原材料弃牌，生成一次蓝色施法许可',{'spell.permissions':'Blue','discard.ids':'basic_card_014','hand':4},True);click('JourneyHand/Btn_CloseHand','进入实际战斗');click('Btn_JourneyEncounter_0','原兽人标记')
click('Btn_OfferedSpells','使用实际供应施法许可');click('OfferedSpells/Btn_Spell_0','原冰冻');click('DeedInputs/Btn_InputEnemy_0','指定实际存活敌人',capture=True);click('DeedInputs/Btn_ConfirmInputs','支付蓝晶施放原冰冻，供体不入手不离供应',{'blue':1,'spell.permissions':'','supply.spells':'magic_013,magic_000,magic_023','hand':4},True)
click('Inspector/Btn_Next','格挡阶段');click('Inspector/Btn_Next','分配伤害');click('Inspector/Btn_Next','原冰冻实际停止攻击，英雄无新伤',{'wounds':0});click('Hand/Btn_Card_advanced_card_030_0','原远古血脉');click('Inspector/Btn_Choice_strong','红费借实际供应强效');click('Inspector/Btn_Play','进入供体选择');click('CopyPicker/Btn_CopyDonor_1','供应反戈一击');click('CopyPicker/Btn_CopyEffect','所借原强效');click('DeedInputs/Btn_ConfirmInputs','真实红晶付款、伤入手、借攻击4且供体保留',{'red':0,'melee':4,'wounds':1,'supply.advanced':'advanced_card_021,advanced_card_028,advanced_card_009'},True)
click('Enemy_0/Btn_Target','选择原护甲3');click('Inspector/Btn_Resolve','借来的原攻击实际击杀',{'kills':1,'fame':2,'melee':0});click('Inspector/Btn_Next','正式战斗结束');click('Inspector/Btn_EndTurn','第一回合保留三原牌和真实伤牌',{'journey.completedTurns':1,'journey.completedBattles':1,'wounds':1,'hand':4,'journey.discardCount':4})
click('Btn_BeginTurn','同一玩家第二回合');click('Btn_JourneyHand','实际保留手牌');play(2);click('CopyPicker/Btn_CopyDonor_0','实际宁静作为永久去除供体');click('CopyPicker/Btn_CopyEffect','选择第一次原治疗');click('DeedInputs/Btn_ConfirmInputs','仅先执行一次，供体永久移除且本体只打出一次',{'copy.remaining':2,'copy.source':'basic_card_001','removed.ids':'basic_card_001','healing':1,'played':1},True)
nextcopy();click('DeedInputs/Btn_ConfirmInputs','第二次独立确认治疗',{'copy.remaining':1,'healing':2});nextcopy();click('DeedInputs/Btn_ConfirmInputs','第三次正式完成，释放后续操作',{'copy.remaining':0,'healing':3,'played':1},True)
click('JourneyHand/Btn_HealHero','原治疗点实际移除血脉伤牌',{'wounds':0,'healing':2,'hand':1},True);click('JourneyHand/Btn_CloseHand','返回同一旅程');click('Btn_EndTurn','正式结束第二回合，永久材料不回弃牌',{'journey.completedTurns':2,'journey.completedBattles':1,'removed.ids':'basic_card_001','hand.ids':'basic_card_021','journey.discardCount':5,'healing':0},True)
if '--from' in sys.argv:
    for s in steps[:int(sys.argv[sys.argv.index('--from')+1])]:s['after']=[];s['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
