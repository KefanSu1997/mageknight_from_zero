"""Exercise actual original starting hands, material discard and source quotas."""
import os
os.environ['BATTLE_RL_PROTOCOL']='--serve-journey'
import json
from pathlib import Path
from train_ppo import Rules
from trajectory_audit import audit

env=Rules();cases=[]
originals={c['id']:c for c in json.loads(Path('resources/text_json/basic_card.json').read_text(encoding='utf-8'))}
color_indices={'红':0,'蓝':1,'绿':2,'白':3}
try:
    def perform(index,trace):
        global s
        n=env.step(index)
        trace.append({'before':s['state'],'action':env.actions[index],'after':n['state'],'reward':n['reward']})
        s=n
    def find(test):
        return next((i for i in s['legal'] if test(env.actions[i])),None)
    for case in ('material','extra_quota','convert_one','convert_two'):
        found=False
        for seed in range(70):
            s=env.reset(seed);trace=[]
            if case=='material':
                index=find(lambda a:a['Kind']=='play' and a['Card']=='basic_card_023' and 'material=' in a['Input'])
                if index is None:continue
                perform(index,trace)
            elif case=='extra_quota':
                base=find(lambda a:a['Kind']=='play' and a['Card'] in ('basic_card_009','basic_card_013') and a['Choice']=='base' and not a['Input'])
                ordinary=find(lambda a:a['Kind']=='source')
                if base is None or ordinary is None:continue
                perform(ordinary,trace);perform(base,trace)
                extra=find(lambda a:a['Kind']=='extraSource' and a['Targets']==a['Option'])
                if extra is None:continue
                perform(extra,trace)
            else:
                host='basic_card_009' if case=='convert_one' else 'basic_card_013'
                if host not in s['state']['hand']:continue
                printed_fee=color_indices[originals[host]['required_crystals']]
                ordinary=find(lambda a:a['Kind']=='source' and a['Option'] in (printed_fee,4))
                if ordinary is None:continue
                perform(ordinary,trace)
                index=find(lambda a:a['Kind']=='play' and a['Card']==host and a['Choice']=='strong' and 'dice=' in a['Input'])
                if index is None:continue
                perform(index,trace)
            report=audit(trace);assert not report['errors'],(case,seed,report)
            cases.append({'case':case,'seed':seed,'report':report,'trace':trace});found=True;break
        assert found,'No actual starting-hand fixture found for '+case
    out=Path('AutomationOutputs/BattleRl/20261003_actual_deed_input_checks.json')
    out.write_text(json.dumps({'scope':env.info['scope'],'cases':cases},ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'cases':len(cases),'operations':sum(len(c['trace']) for c in cases),'checks':sum(c['report']['checks'] for c in cases),'evidence':str(out)}))
except Exception as error:
    Path('AutomationOutputs/BattleRl/20261003_actual_deed_input_failure.json').write_text(json.dumps({'error':str(error),'cases':cases,'active_case':case,'seed':seed,'trace':trace},ensure_ascii=False,indent=2),encoding='utf-8')
    raise
finally:env.close()
