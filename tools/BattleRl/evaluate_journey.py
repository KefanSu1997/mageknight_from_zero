"""Evaluate every hero/enemy pair once with fresh shuffles; not a held-out deck test."""
import os
os.environ.setdefault('BATTLE_RL_PROTOCOL','--serve-journey')
import argparse,hashlib,json,time
from collections import Counter
from pathlib import Path
import numpy as np
from train_ppo import Rules,Policy,inputs
from trajectory_audit import audit

def main():
    p=argparse.ArgumentParser();p.add_argument('--model',required=True);p.add_argument('--out',required=True);p.add_argument('--random',action='store_true');p.add_argument('--prefix-summary');a=p.parse_args()
    out=Path(a.out);out.mkdir(parents=True,exist_ok=True);env=Rules();start=time.perf_counter();rows=[];trace_saved=False;reviews=[]
    prefix=json.loads(Path(a.prefix_summary).read_text(encoding='utf-8')) if a.prefix_summary else {'evaluated':0,'winning_episodes':0,'cleared_battles':0,'started_battles':0,'seconds':0}
    assert not (a.random and a.prefix_summary),'A random baseline cannot reuse a greedy prefix'
    offset=prefix['evaluated'];assert 0<=offset<483
    manifest={'model':a.model,'model_sha256':hashlib.sha256(Path(a.model).read_bytes()).hexdigest(),'rules_dll':os.environ.get('BATTLE_RL_DLL'),'prefix_summary':a.prefix_summary,'reused_prefix_episodes':offset,'detail_scope':'new suffix only' if offset else 'full evaluation'}
    if manifest['rules_dll']:manifest['rules_sha256']=hashlib.sha256(Path(manifest['rules_dll']).read_bytes()).hexdigest()
    (out/'evaluation_manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    try:
        s=env.reset(100464);net=Policy(len(s['features']),len(env.actions));net.p=dict(np.load(a.model));rng=np.random.default_rng(20261001)
        for i in range(offset,483):
            seed=100464+i;s=env.reset(seed);hero=s['state']['hero'];enemy=s['state']['enemy'];trace=[];cleared_battles=0;started_battles=0
            while not s['done']:
                x,m=inputs(s,len(env.actions));_,prob,_=net.forward(x[None],m[None]);action=int(rng.choice(s['legal'])) if a.random else net.greedy(prob[0]);nxt=env.step(action)
                trace.append({'before':s['state'],'action':env.actions[action],'after':nxt['state'],'reward':nxt['reward']});s=nxt
                transition=trace[-1]
                cleared_battles+=int(transition['after']['won'] and not transition['before']['won'])
                started_battles+=int(transition['action']['Kind']=='fight')
            review=audit(trace);reviews.append({'seed':seed,**review})
            if review['errors']:
                (out/f'pair_rule_failure_{seed}.json').write_text(json.dumps({'seed':seed,'report':review,'trace':trace},ensure_ascii=False,indent=2),encoding='utf-8')
                raise RuntimeError(f'Trajectory rule audit failed on seed {seed}; retained reproduction')
            row={'seed':seed,'hero':hero,'initial_enemy':enemy,'won_at_least_one':cleared_battles>0,'killed_at_least_one':s['state']['kills']>0,'cleared_battles':cleared_battles,'started_battles':started_battles,**s['state']};rows.append(row)
            # Persist each passed result immediately; a later failure must not erase
            # earlier verified cases. Full traces remain limited to useful evidence.
            with (out/'verified_rows.jsonl').open('a',encoding='utf-8') as f:f.write(json.dumps({'row':row,'audit':review},ensure_ascii=False)+'\n')
            if cleared_battles and not trace_saved:
                (out/'winning_trace.json').write_text(json.dumps({'scope':env.info['scope'],'seed':seed,'trace':trace},ensure_ascii=False,indent=2),encoding='utf-8');trace_saved=True
            if (i+1)%69==0:
                progress={'evaluated':i+1,'winning_episodes':prefix['winning_episodes']+sum(r['won_at_least_one'] for r in rows),'cleared_battles':prefix['cleared_battles']+sum(r['cleared_battles'] for r in rows),'started_battles':prefix['started_battles']+sum(r['started_battles'] for r in rows),'seconds':prefix['seconds']+time.perf_counter()-start}
                (out/'evaluation_progress.json').write_text(json.dumps(progress,indent=2),encoding='utf-8');print(json.dumps(progress),flush=True)
        heroes={h:{'episodes':sum(r['hero']==h for r in rows),'wins':sum(r['hero']==h and r['won_at_least_one'] for r in rows)} for h in sorted({r['hero'] for r in rows})}
        result={'scope':env.info['scope'],'policy':'masked random' if a.random else 'reloaded PPO greedy','evaluation_kind':'483 hero/enemy pairs with unseen shuffle seeds; no held-out deck generalization claim','episodes':len(rows),'wins':sum(r['won_at_least_one'] for r in rows),'win_rate':sum(r['won_at_least_one'] for r in rows)/len(rows),'mean_kills':sum(r['kills'] for r in rows)/len(rows),'heroes':heroes,'formal_end_turn_denied':sum(r['stopReason'].startswith('formal_') for r in rows),'stop_reasons':dict(Counter(r['stopReason'] or 'completed' for r in rows)),'seconds':time.perf_counter()-start,'rows':rows}
        result.update(win_definition='episode cleared at least one entire enemy group',killing_episodes=sum(r['killed_at_least_one'] for r in rows),cleared_battles=sum(r['cleared_battles'] for r in rows),started_battles=sum(r['started_battles'] for r in rows))
        result.update(episodes=offset+len(rows),wins=prefix['winning_episodes']+result['wins'],cleared_battles=prefix['cleared_battles']+result['cleared_battles'],started_battles=prefix['started_battles']+result['started_battles'],reused_prefix_summary=prefix,detail_scope=manifest['detail_scope'])
        result['win_rate']=result['wins']/result['episodes']
        result['battle_clear_rate']=result['cleared_battles']/max(1,result['started_battles'])
        (out/'pair_evaluation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in result.items() if k!='rows'},indent=2),flush=True)
        summary={'trajectories':len(reviews),'checks':sum(r['checks'] for r in reviews),'errors':sum(len(r['errors']) for r in reviews),'gap_trajectories':sum(bool(r['adapter_gaps']) for r in reviews),'no_battle_episodes':sum(r['completedBattles']==0 for r in rows),'completed_battles':sum(r['completedBattles'] for r in rows),'reached_night':sum(r['rounds']==2 for r in rows),'finished_two_rounds':sum(r['rounds']==2 and not r['stopReason'] for r in rows),'reviews':reviews}
        (out/'pair_trajectory_audit.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in summary.items() if k!='reviews'},indent=2),flush=True)
    finally:env.close()

if __name__=='__main__':main()
