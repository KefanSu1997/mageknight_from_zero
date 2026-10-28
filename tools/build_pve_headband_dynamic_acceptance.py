"""Actual level reward -> public skill -> two independent headband inputs -> consumer."""
import json,sys
from pathlib import Path
variant=next((x for x in sys.argv[1:] if x in ('Wolfhawk','Tovak','Goldyx','TovakRepel')),'Wolfhawk')
hero='Tovak' if variant=='TovakRepel' else variant
repel=variant=='TovakRepel'
seed=4 if repel else {'Wolfhawk':0,'Tovak':1,'Goldyx':4}[hero]
fixture=Path('Assets/GameData/BattleDemo/HornGold.asset')
backup=Path('tmp/headband_dynamic_HornGold.original')
if '--restore' in sys.argv:
    original=backup.read_bytes()
    assert fixture.read_bytes()==original.replace(b'effectRandomSeed: 0',f'effectRandomSeed: {seed}'.encode())
    fixture.write_bytes(original)
    print('Original seed-0 asset restored')
    raise SystemExit
if '--prepare' in sys.argv:
    original=fixture.read_bytes()
    assert b'effectRandomSeed: 0' in original
    if backup.exists():assert backup.read_bytes()==original
    else:backup.write_bytes(original)
    fixture.write_bytes(original.replace(b'effectRandomSeed: 0',f'effectRandomSeed: {seed}'.encode()))
steps=[]
def click(path,label,expect=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
      after=[dict(key=k,expected=str(v),rule='实际升级公共技能；头环普通独立借用两次，真实原牌/贡献/费用；不取得技能或消耗自身次数，原行动实际消费') for k,v in {'ui.truncated':'',**(expect or {})}.items()]))
def play(card,choice):
    click('Hand/Btn_Card_basic_card_'+card+'_0','真实原手牌');click('Inspector/Btn_Choice_'+choice,'原效果');click('Inspector/Btn_Play','实际出牌')
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','配置原英雄连续旅程'));click('CampShade/Camp/Btn_Loadout','既有可重放原起点')
for _ in range(2):click('CampShade/Picker/Btn_NextPage','下一页')
click('CampShade/Picker/Btn_Select_39','记录种子的红蓝各一晶配置')
p='CampShade/DeedPicker/'
click('CampShade/Camp/Btn_ChooseCards','选择原卡');click(p+'Btn_ClearDeeds','清空配置')
click(p+'Btn_ChooseDeed_basic_card_000','原行进');click(p+'Btn_DeedNext','基础第二页')
for c in ['014','016','021']:click(p+'Btn_ChooseDeed_basic_card_'+c,'既有原牌')
click(p+'Btn_DeedKind_3','原神器');click(p+'Btn_DeedNext','神器第二页');click(p+'Btn_ChooseDeed_items_022','原启迪头环');click(p+'Btn_DoneDeeds','确认五张原牌')
click('CampShade/Camp/Btn_OriginalHero','原英雄');click('CampShade/HeroStart/Btn_Hero_'+hero,'所选原英雄')
click('CampShade/HeroStart/Btn_ConfiguredHero','等级1无技能原卡构筑',{'hero.id':hero,'hero.level':1,'hero.skills':0,'hand.ids':'basic_card_000,basic_card_014,basic_card_016,basic_card_021,items_022'})
click('Tactic_day_1/Btn_Tactic_day_1','捷足先登');click('Btn_BeginTurn','正式回合');click('Btn_JourneyEncounter_4','原狼骑兵')
for _ in range(3):click('Inspector/Btn_Next','原战斗阶段')
play('021','attack');play('016','attack');click('Enemy_0/Btn_Target','实际狼骑兵');click('Inspector/Btn_Resolve','攻击2加2实际击杀',{'kills':1,'fame':3,'wounds':2,'red':1,'blue':1})
click('Inspector/Btn_Next','战后');click('Inspector/Btn_EndTurn','正式回合末升级',{'upgrade.choices':2})
click('LevelReward/Btn_RewardSkill_'+('1' if hero=='Tovak' and not repel else '0'),'选实际另一技能');click('LevelReward/Btn_RewardAction_0','真实高级行动奖励')
skill='hero_skill_pool_007:07' if repel else {'Wolfhawk':'hero_skill_pool_004:04','Tovak':'hero_skill_pool_007:06','Goldyx':'hero_skill_pool_005:07'}[hero]
click('LevelReward/Btn_ConfirmReward','未选原技能进入公共区',{'hero.level':2,'hero.skills':1,'supply.skills':skill},True)
click('Btn_BeginTurn','同一玩家第二回合')
if hero=='Wolfhawk':
    click('Btn_JourneyEncounter_0','真实兽人')
    for _ in range(3):click('Inspector/Btn_Next','原战斗阶段')
    for card in ['000','014']:
        click('Hand/Btn_Card_basic_card_'+card+'_0','原牌作为真实攻击来源');click('Inspector/Btn_Sideways','实际横置攻击1')
    click('Hand/Btn_Card_items_022_0','实际原头环');click('Inspector/Btn_OpenSpecialArtifact','展开公共技能')
else:
    click('Btn_JourneyHand','真实手牌');click('JourneyHand/Btn_Hand_2','实际原头环');click('JourneyHand/Btn_PlayOutside','展开公共技能')
click('SpecialArtifact/Btn_PublicSkill_0','原公共技能');click('SpecialArtifact/Btn_ConfirmSpecial','选择本次真实输入',capture=True)
if hero=='Wolfhawk':
    for i in range(2):
        click('ContributionSkill/Btn_Part_'+str(i),'选择独立原牌攻击')
        click('ContributionSkill/Btn_Confirm','真实借用第'+str(i+1)+'次',{'headband.remaining':1-i,'power.attacks':'Melee:Physical:3,Melee:Physical:'+('1' if i==0 else '3'),'hero.skills':1},True)
    click('Enemy_0/Btn_Target','原兽人');click('Inspector/Btn_Resolve','两笔实际攻击3合计6击杀',{'kills':1,'headband.remaining':0},True);click('Inspector/Btn_Next','实际战后');click('Inspector/Btn_EndTurn','头环正常弃牌',{'journey.completedTurns':2,'journey.completedBattles':2},True)
else:
    for i in range(2):
        # The next list is rebuilt from actual remaining hand, excluding the headband.
        click('SidewaysSkill/Btn_Card_0','选择本次真实原牌')
        if hero=='Goldyx':click('SidewaysSkill/Btn_Color_'+str(0 if i==0 else 1),'分别支付红色和蓝色实际魔力')
        if repel:click('SidewaysSkill/Btn_Value'+str(2+i),'第一次保留源骰，第二次承诺本回合不取源骰')
        value=(2 if i==0 else 5) if repel else (3 if i==0 else 7) if hero=='Goldyx' else (2 if i==0 else 4)
        checks={'source.forgone':str(i==1),'source.usedThisTurn':'False','skills.used':'False'} if repel else {}
        click('SidewaysSkill/Btn_Confirm','真实借用第'+str(i+1)+'次',{'headband.remaining':1-i,'movement':value,'hero.skills':1,**checks},True)
    click('JourneyHand/Btn_CloseHand','返回连续旅程');click('Btn_JourneyMap','实际地形');click('JourneyMap/Btn_Hex_forest','白昼森林')
    click('JourneyMap/Btn_Move','原森林费用3消费借用移动',{'journey.position':'1,0','movement':2 if repel else 4 if hero=='Goldyx' else 1},True)
    click('JourneyMap/Btn_CloseMap','继续本回合');click('Btn_EndTurn','正式回合末头环与材料弃牌',{'journey.completedTurns':2,'headband.remaining':0,'hero.skills':1,'supply.skills':skill},True)
    if repel:
        click('Btn_BeginTurn','正式新回合解除借用承诺',{'source.forgone':'False','source.usedThisTurn':'False'},True)
        click('Btn_JourneyTools','新回合实际魔力源');click('JourneyTools/Btn_Source_0','选择真实源骰')
        click('JourneyTools/Btn_UseSource','正式取骰证明限制已清理',{'source.usedThisTurn':'True','source.forgone':'False'},True)
        click('JourneyTools/Btn_CloseTools','返回连续旅程')
if '--from' in sys.argv:
    for s in steps[:int(sys.argv[sys.argv.index('--from')+1])]:s['after']=[];s['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(variant,len(steps),'steps',sum(len(s['after']) for s in steps),'assertions')
