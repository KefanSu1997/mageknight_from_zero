"""Compare candidate public rendering with saved real rule states, no model run."""
import sys,json,argparse,hashlib,collections
from pathlib import Path
sys.path.insert(0,'tools/BattleRl')
from critical_public_state_text import document,decode_critical
from resource_public_state_text import document as resource_document,decode as decode_resources
p=argparse.ArgumentParser();p.add_argument('--root',required=True);p.add_argument('--out',required=True);a=p.parse_args();root=Path(a.root)
wanted={};sources=[]
for line in (root/'episode_batches.jsonl').read_text().splitlines():
 b=json.loads(line)
 if b['update']!=50:continue
 for e in b['episodes']:
  path=root/'episodes'/f"worker_{e['worker']}_episode_{e['episode']}.json";raw=path.read_bytes();d=json.loads(raw);sources.append(dict(path=str(path),sha256=hashlib.sha256(raw).hexdigest()))
  for t in d['trace']:wanted[int(t['support_id'])]=t['before']
assert len(sources)==128
phases=('Ranged','Block','AssignDamage','Melee','End');checked=0;counts=collections.Counter();examples=[]
with (root/'contexts/contexts.jsonl').open() as f:
 for ctx,line in enumerate(f,1):
  d=json.loads(line);state=wanted.get(int(d['support_id']))
  if state is None:continue
  phase,enemies=decode_critical(d['state']);assert phases[phase]==state['phase']
  assert enemies==[(i,bool(e['defeated'])) for i,e in enumerate(state['enemies'])]
  resources=decode_resources(d['state']);hand=collections.Counter(state['hand'])
  assert resources['cards']=={k:hand[k] for k in resources['cards']}
  assert sum(resources['cards'].values())==sum(v for k,v in hand.items() if k!='w'),hand
  assert abs(resources['wounds']-state['wounds'])<1e-4
  assert abs(resources['block']-state['block'])<1e-4
  for key in ('crystals','tokens'):
   assert all(abs(v-state[key].get(color,0))<1e-4 for color,v in zip(('Red','Blue','Green','White','Gold','Black'),resources[key]))
  new_resources=resource_document(d);assert new_resources['state'].endswith(d['state']) and new_resources['operations']==d['operations']
  new=document(d);assert new['state'].endswith(d['state']);assert new['operations']==d['operations'];assert new['rules']==d['rules']
  checked+=1;counts[state['phase']]+=1
  if len(examples)<10:examples.append(dict(context=ctx,prefix=new['state'].splitlines()[0]))
assert checked==len(wanted)
r=dict(passed=True,episodes=128,states=checked,phases=dict(counts),examples=examples,sources=sources,scope='Candidate input mapping only. No encoder/gradient/speed/learning gate; not installed in running training. All original text and legal operations preserved.')
Path(a.out).write_text(json.dumps(r,ensure_ascii=False,indent=2));print(json.dumps({k:v for k,v in r.items() if k not in ['sources','examples']}))
