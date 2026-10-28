"""One actual player, seven original cards and two encounters; target/fee/consumer acceptance."""
import json,sys
from pathlib import Path
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原卡费用与实际敌人身份；单体全体互斥、减攻次数与后续真实结算') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def card(id):click('Hand/Btn_Card_'+id+'_0','选择实际原卡 '+id)
def choice(id):click('Inspector/Btn_Choice_'+id,'选择原效果 '+id)
def target(i):click(f'Enemy_{i}/Btn_Target','选择本场敌人 #'+str(i+1))
def play(id,mode,expected=None,enemy=None,capture=False):
    card(id);choice(mode)
    if enemy is not None:target(enemy)
    click('Inspector/Btn_Play','按原费用实际出牌',expected,capture)
def advance():click('Inspector/Btn_Next','推进正式战斗阶段')

picker='CampShade/DeedPicker/'
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','原卡连续构筑'));click('CampShade/Camp/Btn_Loadout','选择既有资源起点')
click('CampShade/Picker/Btn_Select_1','原先发制人：红1蓝2白1')
click('CampShade/Camp/Btn_ChooseCards','选择七张实际原卡');click(picker+'Btn_ClearDeeds','清除旧手牌选择')
click(picker+'Btn_ChooseDeed_basic_card_002','选择凝结原卡');click(picker+'Btn_ChooseDeed_basic_card_010','选择迅捷反应原卡')
click(picker+'Btn_DeedNext','基础原卡第二页');click(picker+'Btn_ChooseDeed_basic_card_015','选择魔力结晶')
click(picker+'Btn_DeedKind_1','高级行动原图目录')
for _ in range(3):click(picker+'Btn_DeedNext','下一页原高级行动')
click(picker+'Btn_ChooseDeed_advanced_card_036','选择迂回躲避');click(picker+'Btn_ChooseDeed_advanced_card_043','选择冷峻目光')
click(picker+'Btn_DeedKind_2','原法术目录');click(picker+'Btn_ChooseDeed_magic_003','选择地脉震颤')
click(picker+'Btn_DeedNext','第二页原法术');click(picker+'Btn_ChooseDeed_magic_019','选择暴露')
click(picker+'Btn_DoneDeeds','七张原牌构筑完成');click('CampShade/Camp/Btn_Journey','开始同一玩家连续旅程')
click('Tactic_day_1/Btn_Tactic_day_1','取得原捷足先登');click('Btn_BeginTurn','第一回合')
click('Btn_JourneyEncounter_2','两名实际骷髅战士',{'hand':7,'blue':2,'white':1,'enemies.armor':'3,3'})
card('magic_003');choice('base');click('Inspector/Btn_Play','缺少目标拒绝，不猜敌人0',{'rejections':1,'hand':7,'played':0})
play('basic_card_015','strong_Green',{'blue':1,'green':1})
play('magic_003','base_all',{'green':0,'enemies.armor':'1,1'})
card('magic_019');choice('base');target(1)
click('Inspector/Btn_Play','白费暴露只作用于第二实际敌人',{'white':0,'ranged':2,'enemies.resistancesRemoved':'1'},True)
target(1);click('Inspector/Btn_Resolve','实际远程2击败减甲后目标',{'kills':1,'fame':2,'ranged':0})
play('advanced_card_043','strong_enemy_blue',{'blue':0,'tokens.blue':0,'enemies.coldStareStopped':'0'},0,True)
play('basic_card_010','ranged',{'ranged':1})
target(0);click('Inspector/Btn_Resolve','远程1实际消费，另一原敌击败',{'kills':2,'fame':4,'ranged':0})
advance();advance();advance();advance()
click('Inspector/Btn_EndTurn','正式结束第一回合，保留另外两张原牌',{'journey.completedTurns':1,'hand':2,'wounds':0})
click('Btn_BeginTurn','同一玩家下一回合');click('Btn_JourneyEncounter_0','新的实际哨卫',{'enemies.armor':'3','enemies.attacks':'4'})
play('basic_card_002','white',{'tokens.white':1});advance()
play('advanced_card_036','strong',{'tokens.white':0,'reduction.remaining':1,'melee':0},capture=True)
target(0);click('Inspector/Btn_ReduceAttack','实际消费一次减攻4，原攻击4降至0',{'reduction.remaining':0,'enemies.attacks':'0','reduction.pending':'0:0=4'},True)
advance();click('Inspector/Btn_Next','正式承伤完成才获得无伤攻击奖励',{'wounds':0,'melee':2})
target(0);click('Inspector/Btn_Resolve','攻击2不足原护甲3仍消费，不能伪造击杀',{'kills':0,'fame':4,'melee':0},True)
advance();click('Inspector/Btn_EndTurn','第二场退却后同一牌库守恒',{'journey.completedTurns':2,'journey.completedBattles':2,'hand':0,'hero.deckTotal':7,'wounds':0})
if '--from' in sys.argv:
    boundary=int(sys.argv[sys.argv.index('--from')+1])
    for step in steps[:boundary]:step['after']=[];step['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=240,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
