"""Actual original Box reveal/use/lifecycle; no injected card or supply order."""
import os,json,time
from pathlib import Path
os.environ.setdefault('BATTLE_RL_PROTOCOL','--serve-journey-artifacts')
from train_ppo import Rules
from trajectory_audit import audit,_action_colors,_spell_colors
from copy import deepcopy
env=Rules();traces=[];cases={};checks=0

def step(i):
 global s
 n=env.step(i);traces[-1].append({'before':s['state'],'action':env.actions[i],'after':n['state'],'reward':n['reward']});s=n

def action(kind,**kw):return next(i for i in s['legal'] if env.actions[i]['Kind']==kind and all(env.actions[i][k]==v for k,v in kw.items()))
try:
 for seed in range(1500):
  inv=env.call(op='inspectInventory',seed=seed)['state']
  if 'items_024' not in inv['hand']:continue
  s=env.reset(seed);traces.append([]);prior=s['state'];step(action('boxReveal'));box=s['state']['mysticBox'];host=box['physical'];revealed=box['revealed']
  assert s['state']['artifactSupply']==prior['artifactSupply']-1 and box['used']==False;checks+=1
  available=[i for i in s['legal'] if env.actions[i]['Kind']=='boxUse'];assert all(env.actions[i]['Card']==revealed for i in available);checks+=1
  if 'unused' not in cases:
   step(action('nextTurn'));assert s['state']['mysticBox'] is None and s['state']['artifactSupply']==prior['artifactSupply'];checks+=1
   assert any(c['instance']==host for c in s['state']['zones']['hand']);checks+=1;cases['unused']=seed
  elif revealed in ('items_004','items_005','items_006','items_007'):
   mode='normal' if 'ring_normal' not in cases else 'burn'
   if 'ring_'+mode in cases:traces.pop();continue
   before=s['state'];step(action('boxUse',Card=revealed,Choice=mode));assert s['state']['mysticBox']['used'];checks+=1
   assert not any(env.actions[i]['Kind']=='boxUse' for i in s['legal']);checks+=1
   assert s['state']['fame']==before['fame']+(2 if mode=='normal' else 1);checks+=1
   assert any(c['instance']==host for c in s['state']['zones']['removed' if mode=='burn' else 'played']);checks+=1
   assert s['state']['acquired']==before['acquired'];checks+=1
   # Consume the acquired permission/resource through an actual original strong card.
   color={'items_004':'Red','items_005':'Blue','items_006':'White','items_007':'Green'}[revealed]
   candidate=next(i for i in s['legal'] if env.actions[i]['Kind']=='play' and env.actions[i]['Choice'].startswith('strong') and (_action_colors|_spell_colors).get(env.actions[i]['Card'])==color)
   stock=s['state'];step(candidate)
   if mode=='normal':assert s['state']['tokens'].get(color,0)==stock['tokens'].get(color,0)-1 and s['state']['crystals']==stock['crystals']
   else:assert s['state']['tokens']==stock['tokens'] and s['state']['crystals']==stock['crystals']
   checks+=1
   step(action('nextTurn'));assert s['state']['mysticBox'] is None and s['state']['artifactSupply']==prior['artifactSupply'];checks+=1
   cases['ring_'+mode]=seed
  else:traces.pop();continue
  reports=[audit(t) for t in traces];assert not any(r['errors'] for r in reports),reports
  if len(cases)==3:break
 assert len(cases)==3,cases
 negative_checks=0
 for mutate in ('physical','revealed','used'):
  bad=deepcopy(traces[1]);row=next(t for t in bad if t['action']['Kind']=='boxUse')
  if mutate=='physical':row['after']['mysticBox']['physical']=-123
  elif mutate=='revealed':row['before']['mysticBox']['revealed']='items_024'
  else:row['after']['mysticBox']['used']=False
  assert audit(bad)['errors'],mutate;negative_checks+=1
 result={'negative_checks':negative_checks,'scope':env.info['scope'],'fixture_checks':checks,'cases':cases,'reports':reports,'traces':traces,'limits':'Original ring normal/burn plus optional unused recovery; weapons/materials/banners and supply bottom order need further targeted evidence; Headband alias unsupported'}
 Path('AutomationOutputs/BattleRl/20261003_mystic_box.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
 print(json.dumps({'cases':cases,'fixture_checks':checks,'transition_checks':sum(r['checks'] for r in reports)}))
except Exception as e:
 Path(f'AutomationOutputs/BattleRl/20261003_mystic_box_failure_{time.time_ns()}.json').write_text(json.dumps({'error':str(e),'cases':cases,'traces':traces},ensure_ascii=False,indent=2),encoding='utf-8');raise
finally:env.close()
