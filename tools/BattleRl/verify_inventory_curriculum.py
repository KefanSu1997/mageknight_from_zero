"""Check all seven heroes across original/21-card/26-card inventory families."""
import os
os.environ['BATTLE_RL_PROTOCOL']='--serve-journey-builds'
import json
from collections import Counter
from pathlib import Path
from train_ppo import Rules

catalog=json.loads(Path('tmp/rl_v11_catalog.json').read_text(encoding='utf-8'))
heroes={h['Id']:h for h in catalog['heroes']}
original_ids={c['id'] for c in catalog['cards']}
env=Rules();rows=[];checks=0
try:
    for seed in range(21):
        state=env.reset(seed);s=state['state']
        cards=[c for zone in s['zones'].values() for c in zone if not c['wound']]
        ids=[c['id'] for c in cards];basic=Counter(c for c in ids if c.startswith('basic_card_'))
        extras=[c for c in ids if not c.startswith('basic_card_')]
        tests=[len(ids)==(16 if seed%3==0 else 21 if seed%3==1 else 26),
               basic==Counter(heroes[s['hero']]['StartingCards']),set(ids)<=original_ids,
               len(extras)==len(set(extras)),all(0<=v<=3 for v in s['crystals'].values()),
               not any(s['tokens'].values()),s['level']==1,
               len(s['skills'])<=s['level']//2,
               not set(ids)&set(s['advancedOffer']),not set(ids)&set(s['spellOffer']),
               all(0<=i<len(env.actions) for i in state['legal'])]
        checks+=len(tests);assert all(tests),(seed,s['hero'],tests)
        rows.append({'seed':seed,'hero':s['hero'],'setup':s['setup'],'cards':len(ids),'extra_original_ids':extras,'checks':len(tests)})
    assert len({(r['hero'],r['cards']) for r in rows})==21
    out=Path('AutomationOutputs/BattleRl/20261003_inventory_curriculum_checks.json')
    out.write_text(json.dumps({'scope':env.info['scope'],'checks':checks+1,'rows':rows,'status':'inventory_validation_only_not_effect_coverage'},ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'inventories':len(rows),'checks':checks+1,'evidence':str(out)}))
finally:env.close()
