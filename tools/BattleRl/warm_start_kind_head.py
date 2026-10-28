"""Initialize only the new kind head from an old PPO's real-rule decisions.

This is supervised initialization, not PPO training or held-out evaluation.
No rule actions are fabricated; teacher decisions execute in the real DLL.
"""
import argparse,hashlib,json
from pathlib import Path
import numpy as np
from train_ppo import Rules,Policy,inputs
from trajectory_audit import audit

def main():
    p=argparse.ArgumentParser();p.add_argument('--teacher',required=True);p.add_argument('--out',required=True)
    p.add_argument('--seed-start',type=int,required=True);p.add_argument('--episodes',type=int,default=7)
    p.add_argument('--epochs',type=int,default=100);args=p.parse_args()
    out=Path(args.out);out.mkdir(parents=True,exist_ok=True)
    teacher_path=Path(args.teacher);old=dict(np.load(teacher_path))
    assert 'wk' not in old,'Expected the previous joint-action PPO teacher'
    old_info=json.loads((teacher_path.parent/'environment.json').read_text(encoding='utf-8'))
    env=Rules();rows=[];reviews=[];teacher_wins=0
    try:
        s=env.reset(args.seed_start);d=len(s['features']);n=len(env.actions)
        assert env.info['featureNames'][:len(old_info['featureNames'])]==old_info['featureNames']
        student=Policy(d,n);student.p['w1'][:old['w1'].shape[0]]=old['w1'];student.p['w1'][old['w1'].shape[0]:]=0
        for k in ('b1','wv','bv'):student.p[k]=old[k].copy()
        original={a['Key']:i for i,a in enumerate(old_info['catalog'])};known=np.zeros(n,dtype=bool)
        for i,a in enumerate(env.actions):
            if a['Key'] in original:
                known[i]=True;student.p['wp'][:,i]=old['wp'][:,original[a['Key']]];student.p['bp'][i]=old['bp'][original[a['Key']]]
        kinds=sorted({a['Kind'] for a in env.actions});groups=np.array([kinds.index(a['Kind']) for a in env.actions],dtype=np.int32)
        teacher=Policy(d,n);teacher.p={k:v.copy() for k,v in student.p.items()}
        if 'action_kind_groups' in old:teacher.p['action_kind_groups']=groups.copy()
        student.enable_kind_factor(groups)
        for seed in range(args.seed_start,args.seed_start+args.episodes):
            s=env.reset(seed);trace=[];wins=0
            while not s['done']:
                x,mask=inputs(s,n);teacher_mask=mask&known
                assert teacher_mask.any(),'Teacher has no original legal action; do not auto-advance'
                h,prob,_=teacher.forward(x[None],teacher_mask[None]);action=teacher.greedy(prob[0])
                available=np.array([mask[groups==g].any() for g in range(len(kinds))])
                rows.append((h[0].copy(),available,int(groups[action])))
                nxt=env.step(action);trace.append({'before':s['state'],'action':env.actions[action],'after':nxt['state'],'reward':nxt['reward']})
                wins+=bool(nxt['state']['won']) and not s['state']['won'];s=nxt
            report=audit(trace);reviews.append({'seed':seed,'operations':len(trace),'whole_wins':wins,**report});teacher_wins+=wins
            if report['errors']:
                (out/f'teacher_rule_failure_{seed}.json').write_text(json.dumps({'seed':seed,'trace':trace,'report':report},ensure_ascii=False),encoding='utf-8')
                raise RuntimeError('Teacher real trajectory failed; retain failure')
            print(json.dumps({'teacher_seed':seed,'operations':len(trace),'whole_wins':wins,'errors':0}),flush=True)
        h=np.array([r[0] for r in rows]);available=np.array([r[1] for r in rows]);target=np.array([r[2] for r in rows]);rng=np.random.default_rng(7)
        for epoch in range(args.epochs):
            for ids in np.array_split(rng.permutation(len(rows)),max(1,(len(rows)+127)//128)):
                z=np.where(available[ids],h[ids]@student.p['wk']+student.p['bk'],-1e9);z-=z.max(axis=1,keepdims=True)
                q=np.exp(z);q/=q.sum(axis=1,keepdims=True);q[np.arange(len(ids)),target[ids]]-=1;q/=len(ids)
                student.update({'wk':h[ids].T@q,'bk':q.sum(axis=0)},lr=.01)
        z=np.where(available,h@student.p['wk']+student.p['bk'],-1e9)
        agreement=float((z.argmax(axis=1)==target).mean())
        np.savez(out/'policy_ppo.npz',**student.p)
        (out/'environment.json').write_text(json.dumps(env.info,ensure_ascii=False),encoding='utf-8')
        report={'scope':'supervised kind-head initialization from real old PPO; final optimizer remains PPO','teacher_sha256':hashlib.sha256(teacher_path.read_bytes()).hexdigest(),'teacher_model':str(teacher_path),'rules_sha256':hashlib.sha256(env.dll.read_bytes()).hexdigest(),'source_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'seed_start':args.seed_start,'next_seed':args.seed_start+args.episodes,'rows':len(rows),'teacher_whole_wins':teacher_wins,'epochs':args.epochs,'training_kind_agreement':agreement,'reviews':reviews,'limits':'teacher training seeds only; agreement does not prove student decisions, wins, generalization or Unity acceptance; new legal tuple weights remain trainable'}
        (out/'warm_start_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
        print(json.dumps({k:v for k,v in report.items() if k!='reviews'}),flush=True)
    finally:env.close()

if __name__=='__main__':main()
