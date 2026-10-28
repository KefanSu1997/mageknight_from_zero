"""Real level reward -> headband social borrowing -> village consumers."""
import json,sys
from pathlib import Path
args=sys.argv[1:];mode=next((a for a in args if a in ('Lone','Disguise')),'Lone')
hero,seed,skill=('Wolfhawk',6,'hero_skill_pool_004:03') if mode=='Lone' else ('Krang',2,'hero_skill_pool_003:05')
base=Path('tools/build_pve_headband_target_acceptance.py');source=base.read_text(encoding='utf-8').split("if hero != 'Norowas':",1)[0]
a,b=source.index('VARIANTS = {'),source.index('\nhero = ')
source=source[:a]+'VARIANTS = '+repr({hero:(seed,skill,0)})+source[b:]
source=source.replace('tmp/headband_target_HornGold.original','tmp/headband_social_'+mode.lower()+'_HornGold.original')
sys.argv=[str(base),hero]+[a for a in args if a in ('--prepare','--restore')]
exec(compile(source,str(base),'exec'))
rule='真实原英雄升级公共技能，头环逐次独立原交涉效果，不改个人次数；实际影响力消费、原伤牌/原单位与正式回合对应'
hand_play(0,expect={'movement':2})
click('Btn_JourneyMap','实际地图');click('JourneyMap/Btn_Hex_plains','真实村庄');click('JourneyMap/Btn_Move','原行进支付2',{'movement':0,'journey.position':'0,1'});click('JourneyMap/Btn_CloseMap','保留真实位置')
open_headband(3)
option=1 if mode=='Lone' else 0
for n in (1,2):
 click('HeadbandSocial/Btn_Option_'+str(option),'本次原交涉选择')
 click('HeadbandSocial/Btn_Confirm','实际第'+str(n)+'次影响力',{'influence':n*(3 if mode=='Lone' else 2),'headband.remaining':2-n,'hero.skills':1,'supply.skills':skill},True)
hand_close()
click('Btn_JourneyInteraction','真实村庄交涉');click('JourneyInteraction/Btn_BeginInteraction','正式交涉')
click('JourneyInteraction/Btn_Offer_army_2_006','实际原供应农民')
if mode=='Lone':
 click('JourneyInteraction/Btn_Recruit','原承诺拒绝招募且不消费',{'rejections':1,'units.count':0,'influence':6,'skills.forbidInfluenceRecruit':'True'},True)
 for _ in range(2):click('JourneyInteraction/Btn_BuyHealing','原村庄每次支付3影响买治疗')
 click('JourneyInteraction/Btn_CloseInteraction','保留实际治疗点',{'healing':2,'influence':0})
 hand_open()
 for i in (1,0):click('JourneyHand/Btn_HealHero','实际治疗狼骑兵伤牌',{'wounds':i,'healing':i},True)
 hand_close()
else:
 click('JourneyInteraction/Btn_Recruit','原影响4真实取得供应农民',{'units.count':1,'influence':0,'skills.influenceRecruited':'True','skills.flipped':''},True)
 click('JourneyInteraction/Btn_CloseInteraction','保留实际部队')
click('Btn_EndTurn','正式第二回合同玩家弃牌',{'journey.completedTurns':2,'journey.completedBattles':1,'hero.skills':1,'supply.skills':skill,'skills.forbidInfluenceRecruit':'False','skills.influenceRecruited':'False'},True)
if '--from' in args:
 for s in steps[:int(args[args.index('--from')+1])]:s['after']=[];s['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=360,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(mode,len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
