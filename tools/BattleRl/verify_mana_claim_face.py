"""Replay an actual PPO selected-face mismatch, then prove exact original same-face behavior."""
import os,json,hashlib,time
from pathlib import Path
os.environ.setdefault('BATTLE_RL_PROTOCOL','--serve-journey-artifacts')
from train_ppo import Rules
from trajectory_audit import audit
from copy import deepcopy
source=Path('AutomationOutputs/BattleRl/20261003_coverage_offered_units_ppo/audit_7168_2.json')
saved=json.loads(source.read_text(encoding='utf-8'));assert saved['seed']==4403
old=saved['trace'][:6];env=Rules();traces=[];checks=0

def step(index):
 global s
 n=env.step(index);traces[-1].append({'before':s['state'],'action':env.actions[index],'after':n['state'],'reward':n['reward']});s=n

def choose(kind,**fields):return next(i for i in s['legal'] if env.actions[i]['Kind']==kind and all(env.actions[i][k]==v for k,v in fields.items()))
try:
 indices={a['Key']:i for i,a in enumerate(env.actions)}
 assert old[5]['action']['Key'] not in indices;checks+=1
 mismatch=audit(old);assert any('same basic-color face' in e['rule'] for e in mismatch['errors']);checks+=1
 for choice in ('strong','strong_deferred'):
  s=env.reset(saved['seed']);traces.append([])
  for t in old[:5]:step(indices[t['action']['Key']])
  before=s['state'];step(choose('play',Card='magic_012',Choice=choice,Input='dice=Green;colors=Green'))
  after=s['state'];assert after['roundClaimedDice']==['Green'] and after['claimedDice']==before['claimedDice'];checks+=1
  assert before['source'].count('Green')==after['source'].count('Green')+1 and before['source'].count('Blue')==after['source'].count('Blue');checks+=1
  assert after['tokens'].get('Gold',0)==0 and after['tokens'].get('Black',0)==0 and after['crystals']==before['crystals'];checks+=1
  # Actual Dark Talisman authorizes black in daytime; printed blue fee uses held gold.
  assert after['dayPart']=='Day';checks+=1
  if choice=='strong':assert after['tokens']['Green']==before['tokens']['Green']+3 and after['tokensPerTurn']==before['tokensPerTurn'];checks+=1
  else:
   assert after['tokens']['Green']==before['tokens']['Green'] and after['tokensPerTurn']['Green']==1;checks+=1
   step(choose('nextTurn'));assert s['state']['tokens'].get('Green',0)==1 and s['state']['roundClaimedDice']==['Green'];checks+=1
 # Reuse the audited original Tome fixture; only the affected Claim branch is replayed.
 tome=json.loads(Path('AutomationOutputs/BattleRl/20261003_spell_tome_inputs.json').read_text(encoding='utf-8'))
 seed,slot,face=tome['cases']['deferred'];s=env.reset(seed);traces.append([]);before=s['state']
 step(choose('artifact',Card='items_023',Choice='normal',Input=f'materialSlot={slot};spellId=magic_012;spellChoice=base_deferred;dice={face};colors={face}'))
 assert s['state']['roundClaimedDice']==[face] and s['state']['tokensPerTurn'][face]==1;checks+=1
 assert s['state']['tokens']==before['tokens'] and s['state']['crystals']==before['crystals'];checks+=1
 step(choose('nextTurn'));assert s['state']['tokens'][face]==1 and s['state']['roundClaimedDice']==[face];checks+=1
 reports=[audit(t) for t in traces];assert not any(r['errors'] for r in reports),reports
 bad=deepcopy(traces[0]);bad[-1]['after']['roundClaimedDice']=['Blue'];assert audit(bad)['errors'];checks+=1
 result={'scope':env.info['scope'],'source':str(source),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'seed':saved['seed'],'fixture_checks':checks,'reports':reports,'traces':traces,'retained_old_mismatch_errors':mismatch['errors'],'limits':'Original same-face round reservation, immediate/deferred income and printed blue/black payment; multiplayer curse consumer not covered'}
 Path('AutomationOutputs/BattleRl/20261003_mana_claim_same_face.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
 print(json.dumps({'fixture_checks':checks,'transition_checks':sum(r['checks'] for r in reports),'old_mismatch_errors':mismatch['errors']}))
except Exception as error:
 Path(f'AutomationOutputs/BattleRl/20261003_mana_claim_failure_{time.time_ns()}.json').write_text(json.dumps({'error':str(error),'traces':traces},ensure_ascii=False,indent=2),encoding='utf-8');raise
finally:env.close()
