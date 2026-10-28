"""Actual public skill, Command Banner recruitment, then mixed unit/card fees in second battle."""
import json,sys
from pathlib import Path
args=sys.argv[1:]
base=Path('tools/build_pve_headband_target_acceptance.py');source=base.read_text(encoding='utf-8').split("if hero != 'Norowas':",1)[0]
a,b=source.index('VARIANTS = {'),source.index('\nhero = ')
source=source[:a]+"VARIANTS = {'Arythea': (55, 'hero_skill_pool_000:07', 0)}"+source[b:]
source=source.replace('tmp/headband_target_HornGold.original','tmp/headband_unit_payment_HornGold.original')
source=source.replace("first_page = ['000', '001', '008' if hero == 'Norowas' else '007']","first_page = ['000', '007']")
source=source.replace("click(p + 'Btn_ChooseDeed_items_022', '原启迪头环')","click(p + 'Btn_ChooseDeed_items_017', '原指挥旌旗')\nclick(p + 'Btn_ChooseDeed_items_022', '原启迪头环')")
source=source.replace("+ ',items_022'","+ ',items_017,items_022'")
sys.argv=[str(base),'Arythea']+[a for a in args if a in ('--prepare','--restore')]
exec(compile(source,str(base),'exec'))
rule='实际升级公共两极分化与原指挥旗招募北地僧侣，同玩家第二战头环两次费用由单位与原手牌实际消费，原费用/源骰/魔晶/耗竭/元素攻击/击杀一致'
hand_open();click('JourneyHand/Btn_Hand_3','实际指挥旌旗');click('JourneyHand/Btn_PlayOutside','原供应免费招募')
click('SpecialArtifact/Btn_SpecialBurn','原指挥旗焚毁');click('SpecialArtifact/Btn_FreeOffer_1','实际供应北地僧侣')
click('SpecialArtifact/Btn_ConfirmSpecial','实际取得原单位及占用指挥槽',{'units.count':1,'journey.offer':'army_2_001,army_2_006','units.ready':'True','removed.ids':'items_017','command.ordinaryFree':0},True)
hand_close();click('Btn_EndTurn','正式第二回合保留部队和头环',{'journey.completedTurns':2,'units.ready':'True','hand':6})
click('Btn_BeginTurn','同一玩家第三回合');click('Btn_JourneyEncounter_0','原哨卫第二战')
for _ in range(3):click('Inspector/Btn_Next','原阶段推进，英雄承伤保留单位就绪')
click('Hand/Btn_Card_items_022_0','实际保留头环');click('Inspector/Btn_OpenSpecialArtifact','原头环公共技能')
click('SpecialArtifact/Btn_PublicSkill_0','原两极分化');click('SpecialArtifact/Btn_ConfirmSpecial','独立实际付款输入')
click('Btn_Roster','实际自有部队');click('Units/Btn_Unit_0','原北地僧侣')
click('Inspector/Btn_ManaPayment','本次原蓝费用');click('ManaPayment/Btn_Required_Blue','僧侣印刷蓝费')
click('ManaPayment/Btn_Die_2','实际白源骰转换蓝',{'source.faces':'Blue,Red,White'},True)
click('ManaPayment/Btn_Select','确认付款输入尚未消费')
click('Inspector/Btn_UnitAbility_1','原寒冰攻击4，真实付款与耗竭',{'melee':4,'source.units':'army_2_005','units.ready':'False','headband.remaining':1,'source.faces':'Blue,Red','blue':1,'hero.skills':1},True)
click('Btn_Roster','同一手牌消费者');click('Hand/Btn_Card_basic_card_007_0','实际敏捷')
click('Inspector/Btn_Choice_strong','原白费强化远程攻击')
click('Inspector/Btn_ManaPayment','第二次实际付款');click('ManaPayment/Btn_Required_White','敏捷原白费');click('ManaPayment/Btn_Source_Blue','实际蓝库存晶体')
click('ManaPayment/Btn_Select','第二次独立选择')
click('Inspector/Btn_Play','蓝晶真实转白，远程攻击3',{'melee':4,'ranged':3,'blue':0,'headband.remaining':0,'units.ready':'False'},True)
click('Enemy_0/Btn_Target','原实际哨卫');click('Inspector/Btn_Resolve','原单位寒冰和原卡远程实际击杀',{'kills':1},True)
click('Inspector/Btn_Next','保留原战果');click('Inspector/Btn_EndTurn','正式第二战结算，不重建单位',{'journey.completedTurns':3,'journey.completedBattles':2,'units.ready':'False','removed.ids':'items_017','hero.skills':1},True)
if '--from' in args:
 for step in steps[:int(args[args.index('--from')+1])]:step['after']=[];step['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=360,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print('UnitPayment',len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
