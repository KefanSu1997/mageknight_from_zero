"""Actual declared-task Unity admission, before any sampled cost is claimed."""
import argparse
import json
from pathlib import Path
import sqlite3
import time
import uuid
import zipfile
from structured_ui_ppo import ROOT, Demo, state, write_json


class CostDemo(Demo):
    def reset_task(self,definition):
        return self.native_call('reset_task','taskJson',definition)

    def prepare_prefix(self,moves,payment_consumer=None):
        return self.native_call('prepare_prefix','prefixJson',moves,
            paymentConsumerJson=json.dumps(payment_consumer,ensure_ascii=False) if payment_consumer else '')

    def native_call(self,operation,field,value,**extra):
        self.calls+=1;rid=f'{self.calls:04d}_{uuid.uuid4().hex[:8]}'
        request=dict(id=rid,operation=operation,fastCapture=getattr(self,'fast_capture',False),**{field:json.dumps(value,ensure_ascii=False)},**extra)
        write_json(self.directory/'request.json',request)
        began=time.monotonic()
        while time.monotonic()-began<60:
            p=self.directory/'responses'/f'{rid}.json'
            if p.exists():
                try:result=json.loads(p.read_text(encoding='utf-8'))
                except (OSError,json.JSONDecodeError):
                    time.sleep(.01 if getattr(self,'fast_capture',False) else .05)
                    continue
                write_json(self.directory/'exchanges'/f'{rid}.json',dict(request=request,response=result,role='native_fixture_reset_not_measured_click'))
                if not result['success']:raise RuntimeError(result['error'])
                return result['observation']
            time.sleep(.01 if getattr(self,'fast_capture',False) else .05)
        raise TimeoutError('Native task reset RPC timeout')


def outside_expected(s):
    values={'hero.id':s['hero'],'fame':s['fame'],'wounds':s['wounds'],'hand':len(s['hand']),
        'hand.ids':','.join(s['hand']),'deck.count':s['deck'],'units.count':len(s['units']),
        'units.ready':','.join(str(u['ready']) for u in s['units']),
        'units.wounds':','.join(str(u['wounds']) for u in s['units']),
        'source.available':','.join(s['auditSourceOrder']),'source.usedThisTurn':s['sourceUsedThisTurn'],
        'journey.dayPart':s['dayPart'],'played':len(s['zones']['played'])}
    for color in ('Red','Blue','Green','White'):values[color.lower()]=s['crystals'].get(color,0)
    for color in ('Red','Blue','Green','White','Gold','Black'):values['tokens.'+color.lower()]=s['tokens'].get(color,0)
    return {k:str(v) for k,v in values.items()}


def main():
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);args=p.parse_args()
    out=args.out.resolve();out.mkdir(parents=True,exist_ok=False)
    db=sqlite3.connect('file:'+str(ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite').replace('\\','/')+'?mode=ro',uri=True)
    # Independent, real archived roots with first trace Outside and no omitted
    # setup effects. Older roots are replay checked, not assumed compatible.
    chosen=[];heroes=set()
    rows=db.execute("SELECT id,path,volume,reset_json,prefix_json FROM episodes WHERE reset_kind='declared_task' ORDER BY id")
    for eid,path,volume,reset,prefix in rows:
        task=json.loads(reset)['task'];hero=task['HeroId']
        if hero in heroes or json.loads(prefix):continue
        with zipfile.ZipFile('H:/MageKnight_RL_Backups/20261012_before_reward_v2/'+volume) as z:doc=json.loads(z.read(path))
        if doc['trace'][0]['before']['phase']!='Outside':continue
        heroes.add(hero);chosen.append((eid,path,task,doc['trace'][0]['before']))
        if len(heroes)==7:break
    db.close();assert len(chosen)==7,'Seven independent formal root fixtures required'
    demo=CostDemo(out/'live');results=[]
    try:
        for eid,path,task,expected_state in chosen:
            observation=demo.reset_task(task);actual=state(observation);expected=outside_expected(expected_state)
            diffs=[dict(key=k,expected=v,actual=actual.get(k)) for k,v in expected.items() if actual.get(k)!=v]
            audit=json.loads(actual['audit.costFixture'])
            draw_by_id={x['instance']:x['id'] for x in expected_state['zones']['draw']}
            audit_expected=dict(draw=[draw_by_id[i] for i in expected_state['auditDrawOrder']],
                skills=expected_state['skills'],units=[u['id'] for u in expected_state['units']],
                sourceOrder=expected_state['auditSourceOrder'],unitOffer=expected_state['unitOffer'],
                movement=expected_state['movement'],influence=expected_state['influence'],phase='Outside',
                enemyRewardMode='PrintedFameOnly')  # authoritative JourneyTraining constructor default
            diffs += [dict(key='audit.'+k,expected=v,actual=audit.get(k)) for k,v in audit_expected.items() if audit.get(k)!=v]
            row=dict(episode=eid,path=path,task=task,checks=len(expected)+len(audit_expected),differences=diffs,
                     snapshot=actual,status='passed' if not diffs else 'failed')
            results.append(row);write_json(out/'results.json',dict(results=results,scope='actual UI task reset gate; no cost samples or actor training'))
            if diffs:raise AssertionError(diffs)
    finally:demo.close()
    print(json.dumps(dict(roots=len(results),checks=sum(r['checks'] for r in results),passed=True)))


if __name__=='__main__':main()
