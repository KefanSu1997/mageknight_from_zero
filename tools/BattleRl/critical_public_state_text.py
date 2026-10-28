"""Candidate renderer only. Explicit public phase and enemy occupancy/status.

No policy, reward, support or original text is changed by importing this module.
Coordinates are the frozen 6314 schema; not hidden simulator dictionaries.
"""
import re

PHASES=('远程攻击','格挡','承受伤害','近战攻击','战斗收尾')

def decode_critical(state):
    pairs=re.findall(r'state\.(\d+)=([-+\d.eE]+)',state)
    if not pairs:raise ValueError('Missing public state coordinates')
    features={int(k):float(v) for k,v in pairs}
    if any(not 0<=k<6314 for k in features):raise ValueError('Unsupported feature schema')
    active=[i for i in range(5) if features.get(7+i,0)!=0]
    if len(active)!=1:raise ValueError('Exactly one combat phase is required')
    enemies=[]
    for slot in range(6):
        base=487+110*slot
        present=features.get(base,0);defeated=features.get(base+1,0)
        if present not in (0,1) or defeated not in (0,1):raise ValueError('Nonbinary enemy flags')
        if present:enemies.append((slot,bool(defeated)))
    return active[0],enemies

def document(original):
    phase,enemies=decode_critical(original['state'])
    summary='公开战斗事实：当前阶段='+PHASES[phase]+'；'
    summary+='；'.join(f'敌人槽{slot}='+('已击败' if defeated else '仍存活') for slot,defeated in enemies)
    if not enemies:summary+='没有公开敌人槽'
    result=dict(original)
    result['state']=summary+'。\n以下是完整原始公开观察：\n'+original['state']
    assert all(result[k]==v for k,v in original.items() if k!='state')
    return result
