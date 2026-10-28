"""Replay a real PPO wound relocation witness, preserving rules while correcting cost."""
import os,json,hashlib,time,argparse
from pathlib import Path
from copy import deepcopy
os.environ['BATTLE_RL_ORIGINAL_OPENINGS']='1';os.environ.setdefault('BATTLE_RL_PROTOCOL','--serve-journey-artifacts')
from train_ppo import Rules,evaluate
from trajectory_audit import audit
parser=argparse.ArgumentParser();parser.add_argument('--source',default='AutomationOutputs/BattleRl/20261003_original_opening_kind_balanced_ppo/audit_2048_5.json');parser.add_argument('--out',default='AutomationOutputs/BattleRl/20261003_physical_wound_cost_replay.json');args=parser.parse_args()
source=Path(args.source);saved=json.loads(source.read_text(encoding='utf-8'));env=Rules();trace=[];checks=0

def total(state):return sum(c['wound'] for zone,cards in state['zones'].items() if zone!='removed' for c in cards)
def game(state):return {k:v for k,v in state.items() if k!='totalWounds'}
try:
 s=env.reset(saved['seed']);ids={a['Key']:i for i,a in enumerate(env.actions)}
 for ordinal,t in enumerate(saved['trace']):
  assert game(s['state'])==game(t['before']),('before',ordinal)
  index=ids[t['action']['Key']];assert index in s['legal'];n=env.step(index)
  assert game(n['state'])==game(t['after']),('after',ordinal)
  physical_delta=total(n['state'])-total(s['state']);hand_delta=n['state']['wounds']-s['state']['wounds']
  assert abs(n['reward']-(t['reward']-.02*(physical_delta-hand_delta)))<1e-10,('reward',ordinal)
  assert s['state']['totalWounds']==total(s['state']) and n['state']['totalWounds']==total(n['state']);checks+=1
  trace.append({'before':s['state'],'action':t['action'],'after':n['state'],'reward':n['reward']});s=n
 witness=39 if saved['seed']==9354 else next(i for i,t in enumerate(trace) if {c['instance'] for c in t['after']['zones']['removed'] if c['wound']}-{c['instance'] for c in t['before']['zones']['removed'] if c['wound']})
 if saved['seed']==9354:assert trace[witness]['reward']==-.01 and saved['trace'][witness]['reward']==.03
 else:assert {c['instance'] for c in trace[witness]['after']['zones']['removed'] if c['wound']}-{c['instance'] for c in trace[witness]['before']['zones']['removed'] if c['wound']}
 checks+=1
 report=audit(trace);assert not report['errors'],report
 bad=deepcopy(trace);bad[witness]['after']['totalWounds']=-123;assert audit(bad)['errors'];checks+=1
 # Metric accumulation over an actual completed game, without a second rollout.
 class FinishedObservation:
  def reset(self,seed):return s
 metrics,_=evaluate(FinishedObservation(),None,1)
 assert metrics['mean_total_active_wounds']==total(s['state']) and metrics['mean_hand_wounds']==s['state']['wounds'];checks+=1
 result={'scope':env.info['scope'],'source':str(source),'sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'seed':saved['seed'],'checks':checks,'operations':len(trace),'exact_game_states':True,'new_rewards_match_independent_physical_delta':True,'witness_step':witness,'witness_rewards':[saved['trace'][witness]['reward'],trace[witness]['reward']],'terminal_hand_wounds':metrics['mean_hand_wounds'],'terminal_total_wounds':metrics['mean_total_active_wounds'],'report':report,'trace':trace,'limits':'Actual relocation, physical damage in this trace and metric accumulation; every healing/poison/removal consumer and Unity replay still require broader evidence.'}
 Path(args.out).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in result.items() if k not in ('trace','report')}))
except Exception as error:
 Path(f'AutomationOutputs/BattleRl/20261003_physical_wound_cost_failure_{time.time_ns()}.json').write_text(json.dumps({'error':str(error),'trace':trace},ensure_ascii=False,indent=2),encoding='utf-8');raise
finally:env.close()
