"""Actual Arythea public Pain Power consumes two distinct battle wounds.

After --prepare, refresh Unity assets before Run Acceptance so its seed is imported.
Use --restore after the run; --from retains the successful replay prefix.
"""
import json,runpy,sys
from pathlib import Path
args=sys.argv[1:]
sys.argv=['tools/build_pve_headband_target_acceptance.py','Tovak']+[x for x in args if x in ('--prepare','--restore')]
runpy.run_path(sys.argv[0],run_name='__main__')
p=Path('AutomationConfigs/pve_battle.json');config=json.loads(p.read_text(encoding='utf-8'))
steps=config['steps'][:46]
for s in steps:
    s['buttonPath']=s['buttonPath'].replace('Btn_Hero_Tovak','Btn_Hero_Arythea')
    for a in s['after']:
        a['expected']=a['expected'].replace('Tovak','Arythea').replace('hero_skill_pool_007:05','hero_skill_pool_000:05')
    s['runtimeRule']='原阿瑞希亚痛苦之力：两次各选真实伤牌横置2，头环及伤牌正式已打出与弃牌，消费实际移动'
def click(path,label,checks=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
      after=[dict(key=k,expected=str(v),rule='头环独立借用痛苦之力，每张实际伤牌横置值2并正常弃牌，不改个人许可或次数') for k,v in {'ui.truncated':'',**(checks or {})}.items()]))
for i in range(2):
    click('SidewaysSkill/Btn_Card_0','本次实时实际伤牌')
    click('SidewaysSkill/Btn_Confirm','真实伤牌横置移动2',{'headband.remaining':1-i,'wounds':1-i,'movement':2*(i+1),'hand':5-i,'deck.count':1,'skills.used':'False'},True)
click('JourneyHand/Btn_CloseHand','实际伤牌均在已打出区');click('Btn_JourneyMap','实际地形');click('JourneyMap/Btn_Hex_forest','白昼森林')
click('JourneyMap/Btn_Move','两张伤牌移动4支付森林3',{'journey.position':'1,0','movement':1,'wounds':0},True)
click('JourneyMap/Btn_CloseMap','同一玩家继续');click('Btn_EndTurn','头环和两张真实伤牌正常弃牌',{'journey.completedTurns':2,'headband.remaining':0,'hero.skills':1,'supply.skills':'hero_skill_pool_000:05','hand':5,'deck.count':0},True)
if '--from' in args:
    for s in steps[:int(args[args.index('--from')+1])]:s['after']=[];s['skipScreenshot']=True
config['steps']=steps;p.write_text(json.dumps(config,ensure_ascii=False,indent=2),encoding='utf-8')
print('Pain',len(steps),'steps',sum(len(s['after']) for s in steps),'assertions')
