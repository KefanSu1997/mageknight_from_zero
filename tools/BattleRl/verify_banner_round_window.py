"""Physical banner kept/returned through real end-round and used again."""
import os,json
from pathlib import Path
os.environ.setdefault('BATTLE_RL_PROTOCOL','--serve-journey-artifacts')
from train_ppo import Rules
from trajectory_audit import audit
env=Rules();traces=[];checks=0
def choose(kind,**fields):return next(i for i in s['legal'] if env.actions[i]['Kind']==kind and all(env.actions[i][k]==v for k,v in fields.items()))
def step(index):
    global s
    n=env.step(index);traces[-1].append({'before':s['state'],'action':env.actions[index],'after':n['state'],'reward':n['reward']});s=n
try:
    for take in (False,True):
        s=env.reset(19);traces.append([])
        original=next(c for c in s['state']['zones']['hand'] if c['id']=='items_003')
        step(choose('artifact',Card='items_003',Choice='normal',Targets=0))
        for _ in range(12):
            assert not s['done']
            step(choose('nextTurn'))
            if s['state']['roundWindow']:break
        assert s['state']['roundWindow'] and s['state']['rounds']==1 and s['state']['dayPart']=='Day';checks+=1
        assert all(env.actions[i]['Kind'] in ('roundReturn','roundFinish') for i in s['legal']);checks+=1
        assert original in s['state']['zones']['banners'];checks+=1
        if take:step(choose('roundReturn',Targets=0,Option=1))
        before=s['state'];step(choose('roundFinish'))
        assert s['state']['rounds']==2 and s['state']['dayPart']=='Night' and s['state']['turns']==before['turns']+1;checks+=1
        assert not s['state']['roundWindow'];checks+=1
        if take:
            assert s['state']['units'][0]['banner'] is None and original in sum([s['state']['zones'][name] for name in ('hand','draw','discard')],[]);checks+=1
            for _ in range(12):
                if original in s['state']['zones']['hand']:break
                step(choose('nextTurn'))
            assert original in s['state']['zones']['hand'];step(choose('artifact',Card='items_003',Choice='normal',Targets=0))
            assert original in s['state']['zones']['banners'];checks+=1
        else:assert original in s['state']['zones']['banners'] and not s['state']['units'][0]['bannerSpent'];checks+=1
    reports=[audit(t) for t in traces];assert not any(r['errors'] for r in reports),reports
    result={'scope':env.info['scope'],'fixture_checks':checks,'reports':reports,'traces':traces,'limits':'Single-player original banner return lifecycle; command-banner unit removal and Unity replay not verified here'}
    Path('AutomationOutputs/BattleRl/20261003_banner_round_window.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'fixture_checks':checks,'transition_checks':sum(r['checks'] for r in reports)}))
except Exception as error:
    import time
    Path(f'AutomationOutputs/BattleRl/20261003_banner_round_window_failure_{time.time_ns()}.json').write_text(json.dumps({'error':str(error),'traces':traces},ensure_ascii=False,indent=2),encoding='utf-8');raise
finally:env.close()
