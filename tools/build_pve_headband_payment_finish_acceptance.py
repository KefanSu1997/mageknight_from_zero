"""Replay established payment prefix, then prove real exhaustion of fee consumers."""
import sys,json
from pathlib import Path
args=sys.argv[1:]
base=Path('tools/build_pve_headband_payment_acceptance.py');source=base.read_text(encoding='utf-8')
payment_function='def payment'+source.split('def payment',1)[1].split("if mode=='Invocation':",1)[0]
sys.argv=[str(base),'Invocation']+[a for a in args if a in ('--prepare','--restore')]
exec(compile(source.split('open_headband(4)',1)[0],str(base),'exec'))
exec(compile(payment_function,str(base),'exec'))
for step in steps:step['after']=[];step['skipScreenshot']=True
hand_play(1,'heal',{'healing':1})
hand_open();click('JourneyHand/Btn_HealHero','真实先治疗一张战伤',{'wounds':1,'healing':0});hand_close()
hand_play(2,'base',{'movement':2})
open_headband(2)
payment(0,'strong','Green',material=0,expect={'movement':6,'headband.remaining':1,'hand':1,'hand.ids':'w','hero.skills':1})
click('Btn_HeadbandPaymentStatus','真实只剩伤牌，无原费用消费者才结束',{'headband.remaining':0,'movement':6,'wounds':1,'hand':1},True)
hand_close();click('Btn_JourneyMap','实际地图');click('JourneyMap/Btn_Hex_forest','原森林')
click('JourneyMap/Btn_Move','保留已付效果并实际消费森林3',{'movement':3,'journey.position':'1,0','wounds':1},True)
click('JourneyMap/Btn_CloseMap','同玩家返回')
click('Btn_EndTurn','原回合结束，保留真实未治疗伤牌',{'journey.completedTurns':2,'headband.remaining':0,'wounds':1,'hero.skills':1,'supply.skills':skill},True)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=360,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print('Finish',len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
