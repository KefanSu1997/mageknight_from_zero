"""Formal legal-hero Shapeshift fee/material and contribution consumers."""
import copy
import json
import os
from pathlib import Path
import sys
os.environ.update(BATTLE_RL_SHARED_SKILLS='1',BATTLE_RL_SHAPESHIFT='1',BATTLE_RL_NATIVE_PAYMENTS='1',
                  BATTLE_RL_NATIVE_CONVERSIONS='1',BATTLE_RL_NATIVE_ENERGY_TARGETS='1',
                  BATTLE_RL_ORIGINAL_OPENINGS='1',BATTLE_RL_PROTOCOL='--serve-journey-artifacts')
from train_ppo import Rules

out=Path(sys.argv[1])
if out.exists():raise ValueError('Preserve actual evidence')
out.mkdir(parents=True)
pool=json.loads(Path('AutomationOutputs/BattleRl/20261008_shared_rule_pool_rebinding/pool.json').read_text())
rows=[r for r in pool['tasks']+pool['unknown_tasks'] if 'hero_skill_pool_001:06' in r['task']['definition'].get('Build',{}).get('SkillIds',[])]
env=Rules();saved=[];current=None;trace=[]
def step(index):
    global current
    following=env.step(index);trace.append(dict(before=current['state'],action=env.actions[index],after=following['state'],reward=following['reward']));current=following
try:
    cases=[('basic_card_023',False,2,3)] if len(sys.argv)>2 and sys.argv[2]=='base-consumer-only' else [('basic_card_000',True,2,4),('basic_card_023',False,2,3),('basic_card_023',True,2,5)]
    for card,strong,target,value in cases:
        found=False
        for row in rows:
            definition=copy.deepcopy(row['task']['definition']);definition['EnemyGroups']=[['monster_032']]
            # Bound genuine fixture; never injected hand, hero skill or custom card.
            for seed in range(definition['ShuffleSeed'],definition['ShuffleSeed']+16):
                definition['ShuffleSeed']=seed;current=env.reset_task(definition);trace=[]
                step(next(i for i in current['legal'] if env.actions[i]['Kind']=='fight'))
                step(next(i for i in current['legal'] if env.actions[i]['Kind']=='advance'))
                choices=[]
                for index in current['legal']:
                    action=env.actions[index]
                    if action['Kind']!='skillShapeshift' or action['Choice']!=('strong' if strong else 'base') or action['Targets']!=target:continue
                    physical=current['state']['handOrder'][action['Parts']]
                    host=next(c for c in current['state']['zones']['hand'] if c['instance']==physical)
                    if host['id']==card:choices.append((index,physical))
                if not choices:continue
                index,physical=choices[0];before=copy.deepcopy(current['state']);action=env.actions[index];step(index);after=current['state']
                assert after['blocks'][-1]['value']==value and after['blocks'][-1]['cardInstance']==physical
                assert any(c['instance']==physical for c in after['zones']['played'])
                fee='Green' if card=='basic_card_000' else 'Red'
                assert after['crystals'][fee]==before['crystals'][fee]-(1 if strong else 0)
                if card=='basic_card_023':
                    slot=int(action['Input'].split('=')[1]);material=before['handOrder'][slot]
                    assert material!=physical and any(c['instance']==material for c in after['zones']['discard'])
                    assert all(c['instance']!=material for c in after['zones']['played'])
                block=[i for i in current['legal'] if env.actions[i]['Kind']=='block' and env.actions[i]['Targets']==0]
                if value>=4:
                    assert block;step(block[0]);assert current['state']['enemies'][0]['attacks'][0]['blocked']
                else:
                    assert not block and not current['state']['enemies'][0]['attacks'][0]['blocked']
                    step(next(i for i in current['legal'] if env.actions[i]['Kind']=='sideways' and env.actions[i]['Card'].startswith('basic_card_')))
                    assert sum(c['value'] for c in current['state']['blocks'])==4
                    step(next(i for i in current['legal'] if env.actions[i]['Kind']=='block' and env.actions[i]['Targets']==0))
                    assert current['state']['enemies'][0]['attacks'][0]['blocked']
                saved.append(dict(definition=copy.deepcopy(definition),card=card,enhanced=strong,value=value,trace=trace));found=True;break
            if found:break
        assert found,(card,strong)
    (out/'actual_cases.json').write_text(json.dumps(saved,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(dict(status='original_legal_Braevalar_fee_material_and_block_consumers_checked',cases=len(saved),operations=sum(len(r['trace']) for r in saved),gaps=['element-preserving legal borrow','world target inputs','model migration','Unity acceptance'])))
except Exception as failure:
    (out/'failure.json').write_text(json.dumps(dict(reason=repr(failure),current=current,trace=trace,completed_cases=saved),ensure_ascii=False,indent=2),encoding='utf-8');raise
finally:env.close()
