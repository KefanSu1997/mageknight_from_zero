"""Re-score saved real transitions without claiming a new policy rollout."""
import json
from pathlib import Path
from single_battle_reward import SingleBattleReward

root=Path('AutomationOutputs/BattleRl/20261011_fixed5000_experiment/run_recovery2')
outcomes=[]
steps=kills=positive=0
for p in list(root.glob('episode_*.json'))[:100]:
    doc=json.loads(p.read_text(encoding='utf-8'))
    trace=doc.get('trace',[])
    r=SingleBattleReward()
    for i,t in enumerate(trace):
        reward=r.step(t['before'],t['after'],i==len(trace)-1)
        steps+=1
        if t['after']['kills']>t['before']['kills']:
            kills+=1;positive+=reward>0
    if trace:outcomes.append(r.metrics)
result=dict(episodes=len(outcomes),steps=steps,kill_actions=kills,
    positive_kill_actions=positive,wins=sum(r['won'] for r in outcomes),
    scope='rescored historical actual transitions, not new training or policy improvement')
Path('AutomationOutputs/BackupInventory_20261012/reward_v2_rescore.json').write_text(json.dumps(result,indent=2))
print(result)
