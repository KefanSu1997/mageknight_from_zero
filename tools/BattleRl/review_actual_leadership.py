"""Replay new actual PPO prefixes and pair native Leadership with ordinary activation."""
import os,json,sys,hashlib,time
from pathlib import Path
from copy import deepcopy
os.environ.setdefault('BATTLE_RL_PROTOCOL','--serve-journey-artifacts');os.environ['BATTLE_RL_ORIGINAL_OPENINGS']='1'
from train_ppo import Rules
from trajectory_audit import audit,classify_attack_sources
source=Path(sys.argv[1]);out=Path(sys.argv[2]);saved=json.loads(source.read_text(encoding='utf-8'));sha=hashlib.sha256(source.read_bytes()).hexdigest()
def digest(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
review_inputs={'source_sha256':sha,'reviewer_sha256':digest(__file__),'audit_sha256':digest(Path(__file__).with_name('trajectory_audit.py')),'dll_sha256':digest(os.environ.get('BATTLE_RL_DLL','tools/BattleRl/bin/Debug/net8.0/BattleRl.dll')),'rules_data_hashes':{p.name:digest(p) for p in sorted(Path('resources/text_json').glob('*.json'))}}
if out.exists() and all(json.loads(out.read_text(encoding='utf-8')).get(k)==v for k,v in review_inputs.items()):
 print(json.dumps({'unchanged_source_already_reviewed':True,'source':str(source)}));sys.exit(0)
env=Rules();pairs=[];trace=saved['trace']
def compare(before,ordinary,actual,action):
 key='blocks' if len(ordinary['blocks'])>len(before['blocks']) else 'attacks'
 normal=ordinary[key][len(before[key]):];boosted=actual[key][len(before[key]):]
 assert len(normal)==len(boosted)==1,('actual_single_unit_contribution',action)
 bonus=3 if key=='blocks' else 1 if normal[0]['type']=='Ranged' else 2
 assert boosted[0]['value']==normal[0]['value']+bonus,('printed_leadership_bonus',action)
 same=lambda part:{k:v for k,v in part.items() if k not in ('value','effectiveValue')}
 assert same(normal[0])==same(boosted[0]),'native bonus retains actual contribution source/element/type'
 for field in ('tokens','crystals','source','held','units','zones'):assert ordinary[field]==actual[field],('actual_same_unit_fee_and_exhaustion',field)
 assert 'hero_skill_pool_002:06' in actual['usedSkillsTurn'] and 'hero_skill_pool_002:06' not in ordinary['usedSkillsTurn']
 return {'unit':action['Card'],'key':key,'base_value':normal[0]['value'],'actual_value':boosted[0]['value'],'printed_bonus':bonus}
try:
 catalog={a['Key']:i for i,a in enumerate(env.actions)}
 for ordinal,t in enumerate(trace):
  if t['action']['Kind']!='unitLeadership':continue
  state=env.reset(saved['seed'])
  for previous in trace[:ordinal]:
   assert state['state']==previous['before'];i=catalog[previous['action']['Key']];assert i in state['legal'];state=env.step(i)
   assert state['state']==previous['after'] and state['reward']==previous['reward']
  assert state['state']==t['before']
  key='unit'+t['action']['Key'][len('unitLeadership'):];index=catalog[key];assert index in state['legal']
  ordinary=env.step(index)['state'];pair=compare(t['before'],ordinary,t['after'],t['action']);pairs.append(dict(pair,step=ordinal))
  bad=deepcopy(t['after']);bad[pair['key']][-1]['value']-=1
  try:compare(t['before'],ordinary,bad,t['action'])
  except AssertionError:pass
  else:raise AssertionError('Wrong native bonus must be rejected')
 assert pairs,'No actual native Leadership use in this saved trace'
 report=audit(trace);assert not report['errors'],report
 result={**review_inputs,'scope':env.info['scope'],'source':str(source),'seed':saved['seed'],'paired_actual_operations':pairs,'actual_attack_consumers':[r for r in classify_attack_sources(trace) if r['mode'].startswith('unit_leadership:')],'report':report,'limits':'New actual PPO native Leadership activation versus the same formal unit activation, with printed additive bonus; underlying original unit effects, assault site fees and all units require independent original-rule evidence.'}
 out.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in result.items() if k not in ('limits','source_sha256','reviewer_sha256','audit_sha256','dll_sha256','rules_data_hashes')},ensure_ascii=False))
except Exception as error:
 out.with_name(out.stem+'_failure_'+str(time.time_ns())+'.json').write_text(json.dumps({'error':str(error),'source':str(source),'seed':saved['seed'],'pairs':pairs},ensure_ascii=False,indent=2),encoding='utf-8');raise
finally:env.close()
