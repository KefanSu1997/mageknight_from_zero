"""Exact semantic replay of a saved current-scope trajectory, including night shuffle."""
import os
os.environ.setdefault('BATTLE_RL_PROTOCOL','--serve-journey')
import argparse,hashlib,json
from pathlib import Path
from train_ppo import Rules
from trajectory_audit import audit

def main():
    parser=argparse.ArgumentParser();parser.add_argument('trace');parser.add_argument('--out',required=True);args=parser.parse_args()
    source=Path(args.trace);saved=json.loads(source.read_text(encoding='utf-8'));env=Rules();actual=[];mismatch=None
    try:
        s=env.reset(saved['seed']);ids={a['Key']:i for i,a in enumerate(env.actions)}
        for i,t in enumerate(saved['trace']):
            if s['state']!=t['before']:
                mismatch={'step':i,'side':'before','expected':t['before'],'actual':s['state']};break
            index=ids[t['action']['Key']]
            if index not in s['legal']:
                mismatch={'step':i,'side':'legal','action':t['action']};break
            n=env.step(index);actual.append({'before':s['state'],'action':env.actions[index],'after':n['state'],'reward':n['reward']});s=n
            if n['state']!=t['after'] or n['reward']!=t['reward']:
                mismatch={'step':i,'side':'after','expected':t['after'],'actual':n['state']};break
        result={'scope':env.info['scope'],'source':str(source),'sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'seed':saved['seed'],'operations':len(actual),'reached_night':any(t['after']['dayPart']=='Night' for t in actual),'ended':s['done'],'final_phase':s['state']['phase'],'exact_states_and_rewards':mismatch is None,'mismatch':mismatch,'report':audit(actual)}
        path=Path(args.out);path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
        print(json.dumps({k:v for k,v in result.items() if k not in ('mismatch','report')},ensure_ascii=False))
        assert mismatch is None and not result['report']['errors'],str(path)
    finally:env.close()

if __name__=='__main__':main()
