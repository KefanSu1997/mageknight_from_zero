"""Keep a real defeated token, carry it through formal turns, consume printed power."""
import argparse,json,os,time
from pathlib import Path
from copy import deepcopy
import numpy as np
from train_ppo import Rules,Policy,inputs
from trajectory_audit import audit

parser=argparse.ArgumentParser();parser.add_argument('--mode',type=int,choices=(1,2));args=parser.parse_args()
env=Rules();traces=[];fixtures=[];skill='hero_skill_pool_003:06'
old_path=Path('AutomationOutputs/BattleRl/20261004_native_fork_ppo')
old=dict(np.load(old_path/'policy_ppo.npz'));info=json.loads((old_path/'environment.json').read_text(encoding='utf-8'))
known={a['Key']:i for i,a in enumerate(info['catalog'])}
mapping={a['Key']:i for i,a in enumerate(env.actions)}

def step(i):
    global state
    n=env.step(i);traces[-1].append({'before':state['state'],'action':env.actions[i],'after':n['state'],'reward':n['reward']});state=n

def teacher():
    legal=[i for i in state['legal'] if env.actions[i]['Key'] in known]
    mask=np.zeros(len(info['catalog']),dtype=bool)
    for i in legal:mask[known[env.actions[i]['Key']]]=True
    assert mask.any(),'No original teacher action; do not synthesize a turn'
    _,prob,_=net.forward(np.array(state['features'][:old['w1'].shape[0]])[None],mask[None])
    return mapping[info['catalog'][net.greedy(prob[0])]['Key']]

try:
    net=Policy(old['w1'].shape[0],len(info['catalog']));net.p=old
    seeds=[]
    for seed in range(5,2100,7):
        s=env.call(op='inspectInventory',seed=seed)['state']
        if skill in s['skills']:seeds.append(seed)
        if len(seeds)>=24:break
    assert seeds
    for mode in ((args.mode,) if args.mode else (1,2)):
        for seed in seeds:
            state=env.reset(seed);traces.append([]);kept=None;used=False;consumed=False
            for _ in range(256):
                if state['done']:break
                options=[i for i in state['legal'] if env.actions[i]['Kind']=='puppetKeep']
                if kept is None and options:
                    selected=next(e for e in state['state']['puppetCandidates'] if e['id']==env.actions[options[0]]['Choice'])
                    step(options[0]);kept=selected
                    assert selected in state['state']['puppetStored']
                    assert not any(env.actions[i]['Kind'] in ('puppetKeep','puppetUse') for i in state['legal'])
                    before=deepcopy(state);observed=env.call(op='state');assert observed['state']==before['state'],'Legality preview mutated token storage'
                    continue
                uses=[i for i in state['legal'] if env.actions[i]['Kind']=='puppetUse' and env.actions[i]['Option']==mode]
                if kept and not used and uses:
                    assert kept in state['state']['puppetStored'];step(uses[0]);used=True
                    assert kept not in state['state']['puppetStored']
                    assert not any(env.actions[i]['Kind']=='puppetUse' for i in state['legal'])
                    continue
                if used:
                    target_kind='attack' if mode==1 else 'block'
                    power='attacks' if mode==1 else 'blocks'
                    candidates=[i for i in state['legal'] if env.actions[i]['Kind']==target_kind and any(p.get('skill')==skill for p in state['state'][power])]
                    if mode==1:
                        candidates=[i for i in candidates if any(p.get('skill')==skill and env.actions[i]['Parts']&(1<<j) for j,p in enumerate(state['state']['attacks']))]
                    if candidates:
                        step(candidates[0]);consumed=True;break
                    # Add only actual legal original sideways cards, never fake power.
                    if state['state']['phase']==('Melee' if mode==1 else 'Block') and any(p.get('skill')==skill for p in state['state'][power]):
                        extra=[i for i in state['legal'] if env.actions[i]['Kind']=='sideways']
                        if extra:step(extra[0]);continue
                step(teacher())
            if used and (consumed or mode==2):
                report=audit(traces[-1]);assert not report['errors'],report
                fixtures.append({'seed':seed,'mode':'attack' if mode==1 else 'block','physical_token':kept,'actual_power_consumer':consumed,'report':report})
                break
            traces.pop()
        else:raise AssertionError(('No real native puppet fixture',mode))
    if args.mode!=2:
        wrong=deepcopy(traces[0]);event=next(t for t in wrong if t['action']['Kind']=='puppetUse');event['after']['attacks'][-1]['value']+=1
        assert any(e['rule']=='puppet melee preserves original attack elements and rounds halves upward' for e in audit(wrong)['errors'])
    result={'scope':env.info['scope'],'fixtures':fixtures,'trace':traces,'negative_checks':['same_turn_second_keep_or_consume','legality_preview_preserves_storage','printed_attack_half_mutation'],'limits':'two native branches with actual tokens and formal turn continuity; not every enemy/resistance, borrowed skill, all consumers, cross-night persistence, PPO coverage or Unity acceptance'}
    Path('AutomationOutputs/BattleRl/20261004_native_puppets'+('' if args.mode is None else '_mode'+str(args.mode))+'.json').write_text(json.dumps(result,ensure_ascii=False),encoding='utf-8')
    print(json.dumps({'fixtures':fixtures,'checks':sum(f['report']['checks'] for f in fixtures)},ensure_ascii=False))
except Exception as error:
    Path(f'AutomationOutputs/BattleRl/20261004_native_puppets_failure_{time.time_ns()}.json').write_text(json.dumps({'error':str(error),'trace':traces},ensure_ascii=False),encoding='utf-8');raise
finally:env.close()
