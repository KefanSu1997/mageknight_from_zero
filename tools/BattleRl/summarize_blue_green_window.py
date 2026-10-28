import json,statistics as st,collections
from pathlib import Path
r=Path('H:/MageKnight_RL_Runs/autodl_20261013/diagnostic_20261014');out={}
for arm in ['blue','green']:
 d=json.loads((r/f'{arm}_window.json').read_text());out[arm]=[]
 for lo,hi in [(30,49),(50,69),(70,89)]:
  rr=[x for x in d['rows'] if lo<=x['update']<=hi];n=len(rr);ac=collections.Counter();mo=collections.Counter()
  for x in rr:ac.update(x['actions']);mo.update(x['modes'])
  val={'range':[lo,hi],'n':n,'wins':sum(x['won'] for x in rr),'means':{k:st.mean(x[k] for x in rr) for k in ['enemies','armor','units','steps','kills','wounds','unit_wounds','reward','remaining_hand','ready_units']},'actions_per_game':{k:v/n for k,v in ac.items()},'modes_per_game':{k:v/n for k,v in mo.items()},'timeouts':sum(bool(x['timeout']) for x in rr),'errors':sum(x['errors'] for x in rr),'strata':{str(c):{'n':sum(x['enemies']==c for x in rr),'wins':sum(x['won'] for x in rr if x['enemies']==c)} for c in sorted(set(x['enemies'] for x in rr))},'no_attack_losses':sum(not x['won'] and not x['actions'].get('attack',0) for x in rr)}
  out[arm].append(val)
(r/'blue_green_analysis.json').write_text(json.dumps(out,indent=2));print(json.dumps(out,indent=2))
