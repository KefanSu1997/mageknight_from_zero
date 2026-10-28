"""Actual Box-to-Horn physical attack source, paid original Siege ally, and real kill."""
import os,json,time
from pathlib import Path
os.environ.setdefault('BATTLE_RL_PROTOCOL','--serve-journey-artifacts')
from train_ppo import Rules
from trajectory_audit import audit,classify_attack_sources
e=Rules();trace=[];checks=0
try:
 s=e.reset(49)
 def step(i):
  global s
  n=e.step(i);trace.append({'before':s['state'],'action':e.actions[i],'after':n['state'],'reward':n['reward']});s=n
 def choose(kind,**fields):return next(i for i in s['legal'] if e.actions[i]['Kind']==kind and all(e.actions[i][k]==v for k,v in fields.items()))
 step(choose('boxReveal'));host=s['state']['mysticBox']['physical'];assert s['state']['mysticBox']['revealed']=='items_009';checks+=1
 step(choose('fight'));before=s['state'];step(choose('boxUse',Card='items_009',Choice='normal'))
 part=s['state']['attacks'][0];assert part['value']==5 and part['type']=='Siege' and part['element']=='Physical' and part['card']=='items_024' and part['cardInstance']==host;checks+=1
 assert s['state']['fame']==before['fame']+1 and s['state']['tokens']==before['tokens'] and s['state']['crystals']==before['crystals'];checks+=1
 before=s['state'];step(choose('unit',Card='army_2_008',Choice='1'))
 assert s['state']['crystals'].get('Green',0)==before['crystals'].get('Green',0)-1 and not next(u for u in s['state']['units'] if u['id']=='army_2_008')['ready'];checks+=1
 part=s['state']['attacks'][1];assert part['value']==4 and part['type']=='Siege' and part['unit']=='army_2_008';checks+=1
 before=s['state'];step(choose('attack',Targets=2,Parts=3))
 assert s['state']['kills']==before['kills']+1 and s['state']['enemies'][1]['defeated'] and not s['state']['enemies'][0]['defeated'] and not s['state']['attacks'];checks+=1
 assert any(c['instance']==host for c in s['state']['zones']['played']);checks+=1
 sources=classify_attack_sources(trace);assert any(p['instance']==host and p['mode'].startswith('boxUse:items_009:normal') and p['targets']=='monster_021' for p in sources);checks+=1
 report=audit(trace);assert not report['errors'],report
 result={'scope':e.info['scope'],'seed':49,'fixture_checks':checks,'report':report,'sources':sources,'trace':trace,'limits':'Normal borrowed Horn with actual siege consumer; burned Horn dice/wounds, Box materials and attached banners remain unverified'}
 Path('AutomationOutputs/BattleRl/20261003_box_weapon_actual_consumer.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
 print(json.dumps({'fixture_checks':checks,'transition_checks':report['checks'],'sources':sources}))
except Exception as error:
 Path(f'AutomationOutputs/BattleRl/20261003_box_weapon_failure_{time.time_ns()}.json').write_text(json.dumps({'error':str(error),'trace':trace},ensure_ascii=False,indent=2),encoding='utf-8');raise
finally:e.close()
