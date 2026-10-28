"""Original public payment skill -> two actual fees -> healing and terrain consumers."""
import json,sys
from pathlib import Path
args=sys.argv[1:];mode=next((a for a in args if a in ('Invocation','Polarization')),'Invocation')
hero='Arythea';seed=26 if mode=='Invocation' else 55
skill='hero_skill_pool_000:'+('06' if mode=='Invocation' else '07')
base=Path('tools/build_pve_headband_target_acceptance.py');source=base.read_text(encoding='utf-8').split("if hero != 'Norowas':",1)[0]
a,b=source.index('VARIANTS = {'),source.index('\nhero = ')
source=source[:a]+'VARIANTS = '+repr({hero:(seed,skill,0)})+source[b:]
source=source.replace('tmp/headband_target_HornGold.original','tmp/headband_payment_'+mode.lower()+'_HornGold.original')
sys.argv=[str(base),hero]+[a for a in args if a in ('--prepare','--restore')]
exec(compile(source,str(base),'exec'))
rule='原头环借用实际升级公共技能，两次原费用独立支付；原材料/魔晶/公共骰与同玩家伤牌治疗、实际地形移动对应，不写个人使用次数'
open_headband(4)
def payment(index,choice,color,material=None,source=None,die=None,expect=None):
 click('JourneyHand/Btn_Hand_'+str(index),'选实际原手牌消费者')
 click('JourneyHand/Btn_OutsideChoice_'+choice,'选原强化效果')
 click('JourneyHand/Btn_ManaPayment','本次实际原费用')
 click('ManaPayment/Btn_Required_'+color,'所替代的一点印刷费用')
 if material is not None:click('ManaPayment/Btn_Material_'+str(material),'本次实时独立材料',capture=True)
 if source is not None:click('ManaPayment/Btn_Source_'+source,'实际库存源色',capture=True)
 if die is not None:click('ManaPayment/Btn_Die_'+str(die),'实际公共源骰',capture=True)
 click('ManaPayment/Btn_Select','保留输入，尚未扣费')
 click('JourneyHand/Btn_PlayOutside','原效果付款提交',expect,True)
if mode=='Invocation':
 # Actual hand: March, Tranquility, Swiftness, Stamina, Headband, two wounds.
 # Picker excludes consumer and physical headband. Swiftness is index 1.
 payment(0,'strong','Green',material=1,expect={'movement':4,'headband.remaining':1,'hand':4,'red':1,'blue':1})
 # Now Tranquility, Stamina, two wounds. Stamina is picker index 0.
 payment(0,'strong_heal','Green',material=0,expect={'healing':2,'movement':4,'headband.remaining':0,'hand':2,'red':1,'blue':1,'hero.skills':1,'supply.skills':skill})
else:
 # First red crystal pays original Tranquility green. Second actual white die pays Stamina blue.
 payment(1,'strong_heal','Green',source='Red',expect={'healing':2,'headband.remaining':1,'hand':5,'red':0,'blue':1,'source.faces':'Blue,Red,White'})
 payment(2,'strong','Blue',die=2,expect={'healing':2,'movement':4,'headband.remaining':0,'hand':4,'red':0,'blue':1,'source.faces':'Blue,Red','hero.skills':1,'supply.skills':skill})
for left in (1,0):click('JourneyHand/Btn_HealHero','消费原治疗点移除狼骑兵实际伤牌',{'wounds':left,'healing':left},True)
hand_close()
click('Btn_JourneyMap','同玩家实际地图')
click('JourneyMap/Btn_Hex_forest','白昼森林目标')
click('JourneyMap/Btn_Move','原森林费用3消费真实移动',{'movement':1,'journey.position':'1,0','wounds':0},True)
click('JourneyMap/Btn_CloseMap','保留真实位置与剩余移动')
click('Btn_EndTurn','正式第二回合结束，借用和临时资源清理',{'journey.completedTurns':2,'journey.completedBattles':1,'movement':0,'hero.skills':1,'supply.skills':skill,'headband.remaining':0},True)
if '--from' in args:
 for step in steps[:int(args[args.index('--from')+1])]:step['after']=[];step['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=360,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(mode,len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
