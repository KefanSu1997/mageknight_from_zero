"""Local masked PPO over the real .NET rule process. No simulated game rules.

NumPy implementation keeps the first experiment dependency-free and small.
The environment protocol also supports a later PyTorch/CUDA trainer unchanged.
"""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
os.environ['OMP_NUM_THREADS']='1'
os.environ['MKL_NUM_THREADS']='1'
import argparse,json,subprocess,time,hashlib
from pathlib import Path
import numpy as np
from trajectory_audit import audit,classify_attack_sources
from coverage_sampler import CoverageSampler
from training_reward import CompletionReward
from policy_diagnostics import summarize_policy_trace

def validate_ability_schema(old_info,new_info):
    if (old_info.get('abilitySchema') != new_info.get('abilitySchema') or
        old_info.get('featureSchema') != new_info.get('featureSchema')):
        raise ValueError('Original indexed feature blocks changed; prefix-copying old input weights is invalid. Use a new policy or an explicit semantic migration.')

class Rules:
    def __init__(self):
        self.dll=Path(os.environ.get('BATTLE_RL_DLL','tools/BattleRl/bin/Debug/net8.0/BattleRl.dll'))
        self.p=subprocess.Popen(['dotnet',str(self.dll),os.environ.get('BATTLE_RL_PROTOCOL','--serve')],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True,encoding='utf-8',bufsize=1)
        self.info=json.loads(self.p.stdout.readline())
        self.actions=self.info['catalog']
    def call(self,**request):
        self.p.stdin.write(json.dumps(request)+'\n');self.p.stdin.flush()
        line=self.p.stdout.readline()
        if not line:raise RuntimeError('Rules process exited')
        r=json.loads(line)
        if 'error' in r:raise RuntimeError(r['error'])
        return r
    def reset(self,seed):return self.call(op='reset',seed=seed)
    def reset_task(self,task):
        if self.info.get('taskResetSchema') not in ('independent_original_factors_v1','independent_original_factors_v2'):
            raise ValueError('Frozen rules do not support explicit tasks; do not fall back to seed reset')
        if task.get('Build') is not None and self.info['taskResetSchema']!='independent_original_factors_v2':
            raise ValueError('Frozen rules do not support explicit builds')
        return self.call(op='reset',task=task)
    def step(self,a):return self.call(op='step',action=int(a))
    def close(self):
        self.p.stdin.write('{"op":"quit"}\n');self.p.stdin.flush();self.p.wait(timeout=10)
        self.p.stdin.close();self.p.stdout.close()

class Policy:
    def __init__(self,d,n,seed=7,entropy_coefficient=.01,value_coefficient=.5):
        if not np.isfinite(entropy_coefficient) or entropy_coefficient<0:
            raise ValueError('Entropy coefficient must be finite and nonnegative')
        self.entropy_coefficient=float(entropy_coefficient)
        if not np.isfinite(value_coefficient) or value_coefficient<0:
            raise ValueError('Value coefficient must be finite and nonnegative')
        self.value_coefficient=float(value_coefficient)
        r=np.random.default_rng(seed)
        self.p={'w1':r.normal(0,np.sqrt(2/(d+64)),(d,64)), 'b1':np.zeros(64),
                'wp':r.normal(0,.01,(64,n)), 'bp':np.zeros(n),
                'wv':r.normal(0,.1,(64,1)), 'bv':np.zeros(1)}
        self.m={k:np.zeros_like(v) for k,v in self.p.items()};self.v={k:np.zeros_like(v) for k,v in self.p.items()};self.t=0
    def enable_kind_factor(self,groups):
        self.p['action_kind_groups']=np.asarray(groups,dtype=np.int32)
        count=int(self.p['action_kind_groups'].max())+1
        self.p['wk']=np.zeros((64,count));self.p['bk']=np.zeros(count)
        for key in ('wk','bk'):self.m[key]=np.zeros_like(self.p[key]);self.v[key]=np.zeros_like(self.p[key])
    def greedy(self,prob):
        if 'wk' not in self.p:return int(prob.argmax())
        groups=self.p['action_kind_groups'];mass=np.bincount(groups,weights=prob,minlength=self.p['bk'].size)
        kind=int(mass.argmax());return int(np.where(groups==kind,prob,-1).argmax())
    def bias_kind(self,actions,kind,bias):
        indices=[i for i,action in enumerate(actions) if action['Kind']==kind]
        if 'wk' in self.p:
            # A constant within a conditional kind cancels in its softmax.
            # Bias the kind gate itself so single-tuple kinds such as fight change mass.
            for group in set(int(self.p['action_kind_groups'][i]) for i in indices):
                self.p['bk'][group]+=bias
        else:
            self.p['bp'][indices]+=bias
    def forward(self,x,mask):
        h=np.tanh(x@self.p['w1']+self.p['b1']);logits=h@self.p['wp']+self.p['bp']
        groups=self.p.get('action_kind_groups')
        if 'wk' in self.p:
            conditional=np.zeros_like(logits);available=np.zeros((len(mask),len(self.p['bk'])),dtype=bool)
            for group in range(available.shape[1]):
                columns=groups==group;legal=mask[:,columns];available[:,group]=legal.any(axis=1)
                if not columns.any():continue
                z=np.where(legal,logits[:,columns],-1e9);z-=z.max(axis=1,keepdims=True)
                q=np.exp(z)*legal;q/=np.maximum(q.sum(axis=1,keepdims=True),1e-30);conditional[:,columns]=q
            kind_logits=np.where(available,h@self.p['wk']+self.p['bk'],-1e9);kind_logits-=kind_logits.max(axis=1,keepdims=True)
            kind_prob=np.exp(kind_logits)*available;kind_prob/=kind_prob.sum(axis=1,keepdims=True)
            prob=conditional*kind_prob[:,groups]
            self._conditional_probs=conditional;self._kind_probs=kind_prob
            return h,prob,(h@self.p['wv']+self.p['bv']).ravel()
        if groups is not None:
            # A state-dependent base measure: equal logits give equal total mass
            # to each legal kind, irrespective of its number of input tuples.
            # Counts depend only on the formal mask, so ordinary PPO logit
            # derivatives remain exact. Metadata is saved with the weights.
            counts=np.ones((len(mask),int(groups.max())+1))
            for group in range(counts.shape[1]):counts[:,group]=np.maximum(1,mask[:,groups==group].sum(axis=1))
            logits=logits-np.log(counts[:,groups])
        logits=np.where(mask,logits,-1e9);logits-=logits.max(axis=1,keepdims=True)
        prob=np.exp(logits);prob/=prob.sum(axis=1,keepdims=True)
        value=(h@self.p['wv']+self.p['bv']).ravel()
        return h,prob,value
    def action_forward(self,features,legal):
        """Single-state inference over formal legal columns; training stays dense."""
        columns=np.asarray(legal,dtype=np.int64)
        if columns.size==0:raise RuntimeError('No formal legal action')
        h=np.tanh(np.asarray(features)@self.p['w1']+self.p['b1'])
        logits=h@self.p['wp'][:,columns]+self.p['bp'][columns]
        groups=self.p.get('action_kind_groups')
        if 'wk' in self.p:
            legal_groups=groups[columns];available=np.unique(legal_groups)
            kind_logits=h@self.p['wk'][:,available]+self.p['bk'][available]
            kind_prob=np.exp(kind_logits-kind_logits.max());kind_prob/=kind_prob.sum()
            selected_prob=np.zeros(columns.size)
            for group,mass in zip(available,kind_prob):
                members=legal_groups==group;z=logits[members]
                q=np.exp(z-z.max());q/=q.sum();selected_prob[members]=mass*q
        else:
            if groups is not None:
                legal_groups=groups[columns]
                counts=np.bincount(legal_groups,minlength=int(groups.max())+1)
                logits-=np.log(counts[legal_groups])
            selected_prob=np.exp(logits-logits.max());selected_prob/=selected_prob.sum()
        prob=np.zeros(self.p['bp'].size);prob[columns]=selected_prob
        return prob,float((h@self.p['wv']+self.p['bv'])[0])
    def loss_grad(self,x,mask,a,oldlog,adv,returns,return_input_gradient=False):
        h,p,value=self.forward(x,mask);n=len(x);log=np.log(np.maximum(p,1e-30));lp=log[np.arange(n),a]
        ratio=np.exp(lp-oldlog);clipped=np.clip(ratio,.8,1.2)
        entropy=-(p*log).sum(axis=1)
        loss=-np.minimum(ratio*adv,clipped*adv).mean()+self.value_coefficient*((value-returns)**2).mean()-self.entropy_coefficient*entropy.mean()
        active=((adv>=0)&(ratio<=1.2))|((adv<0)&(ratio>=.8))
        dz=p.copy();dz[np.arange(n),a]-=1
        dz*=((active*adv*ratio)/n)[:,None]
        if 'wk' not in self.p:dz+=self.entropy_coefficient*p*(log+entropy[:,None])/n
        dz=np.where(mask,dz,0)
        dv=(2*self.value_coefficient*(value-returns)/n)[:,None]
        kind_dz=None
        if 'wk' in self.p:
            groups=self.p['action_kind_groups'];kind_dz=np.zeros((n,len(self.p['bk'])));conditional_dz=dz.copy()
            for group in range(kind_dz.shape[1]):
                columns=groups==group;subtotal=dz[:,columns].sum(axis=1);kind_dz[:,group]=subtotal
                conditional_dz[:,columns]-=self._conditional_probs[:,columns]*subtotal[:,None]
            kind_dz-=self._kind_probs*dz.sum(axis=1,keepdims=True)
            # Joint entropy rewards kinds with more legal parameter tuples.
            # Explore the kind gate independently of each conditional distribution.
            kind_prob=self._kind_probs;kind_log=np.log(np.maximum(kind_prob,1e-30))
            kind_entropy=-(kind_prob*kind_log).sum(axis=1)
            kind_dz+=self.entropy_coefficient*kind_prob*(kind_log+kind_entropy[:,None])/n
            available=np.stack([mask[:,groups==group].any(axis=1) for group in range(kind_dz.shape[1])],axis=1)
            kind_count=available.sum(axis=1)
            conditional_entropy=np.zeros(n)
            for group in range(kind_dz.shape[1]):
                columns=groups==group;q=self._conditional_probs[:,columns]
                qlog=np.log(np.maximum(q,1e-30));qe=-(q*qlog).sum(axis=1)
                conditional_entropy+=qe/kind_count
                conditional_dz[:,columns]+=self.entropy_coefficient*q*(qlog+qe[:,None])/(n*kind_count[:,None])
            factor_entropy=kind_entropy+conditional_entropy
            loss+=self.entropy_coefficient*(entropy-factor_entropy).mean()
            dz=conditional_dz
        g={'wp':h.T@dz,'bp':dz.sum(axis=0),'wv':h.T@dv,'bv':dv.sum(axis=0)}
        dh=dz@self.p['wp'].T+dv@self.p['wv'].T
        if kind_dz is not None:
            g['wk']=h.T@kind_dz;g['bk']=kind_dz.sum(axis=0);dh+=kind_dz@self.p['wk'].T
        dh*=1-h*h
        g['w1']=x.T@dh;g['b1']=dh.sum(axis=0)
        result=(loss,g,float((oldlog-lp).mean()))
        return (*result,dh@self.p['w1'].T) if return_input_gradient else result
    def update(self,g,lr=.0008):
        squared_norms={k:(v*v).sum() for k,v in g.items()}
        norm=np.sqrt(sum(squared_norms.values()));scale=min(1,.5/(norm+1e-12));self.t+=1
        for k in g:
            grad=g[k]*scale;self.m[k]=.9*self.m[k]+.1*grad;self.v[k]=.999*self.v[k]+.001*grad*grad
            self.p[k]-=lr*(self.m[k]/(1-.9**self.t))/(np.sqrt(self.v[k]/(1-.999**self.t))+1e-8)
        return {'gradient_norm':float(norm),'clip_scale':float(scale),
                'policy_head_norm':float(np.sqrt(sum(squared_norms.get(k,0) for k in ('wp','bp','wk','bk')))),
                'value_head_norm':float(np.sqrt(sum(squared_norms.get(k,0) for k in ('wv','bv')))),
                'shared_norm':float(np.sqrt(sum(squared_norms.get(k,0) for k in ('w1','b1'))))}
    def loss_grad_legal(self,x,mask,a,oldlog,adv,returns,return_input_gradient=False):
        """Project to the batch legal union, then restore full Adam gradients."""
        columns=np.flatnonzero(mask.any(axis=0))
        if columns.size==0:raise RuntimeError('No formal legal action')
        local_actions=np.searchsorted(columns,a)
        if np.any(local_actions>=columns.size) or np.any(columns[local_actions]!=a):
            raise ValueError('Selected action is absent from the batch legal union')
        original=self.p
        projected=dict(original)
        projected['wp']=original['wp'][:,columns]
        projected['bp']=original['bp'][columns]
        if 'action_kind_groups' in original:
            projected['action_kind_groups']=original['action_kind_groups'][columns]
        try:
            self.p=projected
            result=self.loss_grad(x,mask[:,columns],local_actions,oldlog,adv,returns,return_input_gradient)
            loss,grad,kl=result[:3]
        finally:self.p=original
        # Dense zero gradients preserve momentum decay and Adam updates on
        # currently inactive columns; skipping their updates would not be equal.
        full_wp=np.zeros_like(original['wp']);full_wp[:,columns]=grad['wp']
        full_bp=np.zeros_like(original['bp']);full_bp[columns]=grad['bp']
        grad['wp']=full_wp;grad['bp']=full_bp
        return (loss,grad,kl,result[3]) if return_input_gradient else (loss,grad,kl)

    def rollout_kl_legal(self,x,mask,a,oldlog):
        """Measure the updated policy over all frozen rollout samples.

        For log(q/p), exp(log(q/p))-1-log(q/p) is nonnegative;
        its expectation under the old policy is KL(p || q). Projecting
        to the legal union preserves each state's original distribution.
        """
        columns=np.flatnonzero(mask.any(axis=0))
        local=np.searchsorted(columns,a)
        if columns.size==0 or np.any(local>=columns.size) or np.any(columns[local]!=a):
            raise ValueError('Rollout selected action is absent from its legal union')
        if not np.all(mask[np.arange(len(a)),a]):
            raise ValueError('Rollout selected action must be legal in its own state')
        original=self.p;projected=dict(original)
        projected['wp']=original['wp'][:,columns];projected['bp']=original['bp'][columns]
        if 'action_kind_groups' in original:projected['action_kind_groups']=original['action_kind_groups'][columns]
        try:
            self.p=projected
            updated=np.empty(len(a))
            # All rollout states are measured, in bounded memory chunks.
            for begin in range(0,len(a),128):
                end=min(begin+128,len(a))
                _,prob,_=self.forward(x[begin:end],mask[begin:end][:,columns])
                updated[begin:end]=np.log(np.maximum(prob[np.arange(end-begin),local[begin:end]],1e-30))
        finally:self.p=original
        log_ratio=updated-oldlog
        return float(np.maximum(np.expm1(log_ratio)-log_ratio,0).mean())

def gradient_check(balanced=False,hierarchical=False,entropy_coefficient=.01,value_coefficient=.5):
    r=np.random.default_rng(9);net=Policy(4,3,entropy_coefficient=entropy_coefficient,value_coefficient=value_coefficient);x=r.normal(size=(5,4));mask=np.ones((5,3),dtype=bool);mask[0,1]=False
    if balanced:net.p['action_kind_groups']=np.array([0,0,1],dtype=np.int32)
    if hierarchical:
        net.enable_kind_factor([0,0,1]);net.p['wk']=r.normal(0,.1,(64,2));net.p['bk']=r.normal(0,.1,2);mask[3,2]=False
    a=np.array([0,1,2,0,2]);_,p,_=net.forward(x,mask);old=np.log(p[np.arange(5),a])+np.array([.1,-.4,.4,-.1,0])
    adv=np.array([1,-1,1,-1,.3]);ret=r.normal(size=5);loss,g,_=net.loss_grad(x,mask,a,old,adv,ret)
    errors=[]
    parameters=[('w1',(1,3)),('b1',(3,)),('wp',(2,1)),('bp',(0,)),('wv',(2,0)),('bv',(0,))]
    if hierarchical:parameters.extend([('wk',(2,1)),('bk',(0,))])
    for k,idx in parameters:
        v=net.p[k][idx];eps=1e-5;net.p[k][idx]=v+eps;l1=net.loss_grad(x,mask,a,old,adv,ret)[0]
        net.p[k][idx]=v-eps;l2=net.loss_grad(x,mask,a,old,adv,ret)[0];net.p[k][idx]=v
        errors.append(abs((l1-l2)/(2*eps)-g[k][idx]))
    assert max(errors)<1e-6,errors
    return max(errors)

def inputs(s,n):
    mask=np.zeros(n,dtype=bool);mask[s['legal']]=True
    if not mask.any():raise RuntimeError('No formal legal action')
    return np.array(s['features']),mask

def estimate_advantages(rewards,values,done,next_value,gamma=.97,gae_lambda=.95):
    """Bootstrap a rollout boundary, but never across a completed episode."""
    advantage=np.zeros(len(rewards));gae=0.;last=float(next_value)
    for i in range(len(rewards)-1,-1,-1):
        live=1-float(done[i])
        delta=rewards[i]+gamma*last*live-values[i]
        gae=delta+gamma*gae_lambda*live*gae
        advantage[i]=gae;last=values[i]
    return advantage

def evaluate(env,net,n,random=False,seed=100000,save_trace=False,trace_out=None):
    wins=steps=wounds=total_wounds=0;trace=[];rng=np.random.default_rng(seed);heroes={};battles=rounds=stops=0;killing_episodes=won_battles=0
    if trace_out is not None:Path(trace_out).mkdir(parents=True,exist_ok=True)
    for i in range(n):
        s=env.reset(seed+i);current=[];episode_wins=0
        while not s['done']:
            p,_=net.action_forward(s['features'],s['legal'])
            a=rng.choice(s['legal']) if random else net.greedy(p)
            nxt=env.step(a);steps+=1
            if 'completedBattles' in nxt['state']:
                episode_wins+=int(nxt['state'].get('won',False) and not s['state'].get('won',False))
            elif nxt['done'] and nxt['won']:
                episode_wins+=1
            if save_trace or trace_out is not None:current.append({'before':s['state'],'action':env.actions[a],'after':nxt['state'],'reward':nxt['reward']})
            s=nxt
        did_win=episode_wins>0;wins+=did_win;won_battles+=episode_wins;killing_episodes+=int(s['state'].get('kills',int(s['won']))>0);wounds+=int(s['state']['wounds']);total_wounds+=int(s['state'].get('totalWounds',sum(c['wound'] for zone,cards in s['state'].get('zones',{}).items() if zone!='removed' for c in cards)))
        battles+=s['state'].get('completedBattles',0);rounds+=s['state'].get('rounds',1);stops+=s['state'].get('stopReason','').startswith('formal_')
        hero=s['state'].get('hero',s['state'].get('hero.id','unknown'));h=heroes.setdefault(hero,{'episodes':0,'wins':0});h['episodes']+=1;h['wins']+=did_win
        if trace_out is not None:
            report={'seed':seed+i,'hero':hero,'won_battles':episode_wins,'stop_reason':s['state'].get('stopReason',''),'trace':current}
            (Path(trace_out)/f'seed_{seed+i}.json').write_text(json.dumps(report,ensure_ascii=False),encoding='utf-8')
        if save_trace:trace=current
    return {'episodes':n,'wins':wins,'win_rate':wins/n,'win_definition':'episode cleared at least one entire enemy group','killing_episodes':killing_episodes,'won_battles':won_battles,'mean_steps':steps/n,'mean_wounds':wounds/n,'mean_hand_wounds':wounds/n,'mean_total_active_wounds':total_wounds/n,'mean_completed_battles':battles/n,'mean_rounds':rounds/n,'formal_stop_count':stops,'heroes':heroes},trace

def prepare_training_start(env,state,enter_combat):
    """Apply a formal curriculum prefix, outside the on-policy PPO rollout."""
    if not enter_combat:return state,[]
    candidates=[int(i) for i in state['legal'] if env.actions[int(i)]['Kind']=='fight']
    if len(candidates)!=1:raise ValueError('Combat curriculum requires exactly one actual legal fight entry')
    action=candidates[0];nxt=env.step(action)
    if not nxt['state']['inBattle']:raise ValueError('Formal fight entry did not enter combat')
    prefix=[{'before':state['state'],'action':env.actions[action],'after':nxt['state'],
             'reward':nxt['reward'],'curriculum_initialization':True}]
    report=audit(prefix)
    if report['errors']:raise ValueError(('Combat curriculum prefix violates rules',report))
    return nxt,prefix

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--steps',type=int,default=32768);parser.add_argument('--out',default='AutomationOutputs/BattleRl/ppo_pilot');parser.add_argument('--eval-episodes',type=int,default=200);parser.add_argument('--resume');parser.add_argument('--audit-seconds',type=float,default=60);parser.add_argument('--fight-init-bias',type=float,default=0);parser.add_argument('--seed-start',type=int,default=0);parser.add_argument('--coverage-window',type=int,default=0);parser.add_argument('--coverage-prior');parser.add_argument('--balance-action-kinds',action='store_true');parser.add_argument('--factor-action-kinds',action='store_true');parser.add_argument('--include-original-openings',action='store_true');parser.add_argument('--learning-rate',type=float,default=.0008);parser.add_argument('--gamma',type=float,default=.97);parser.add_argument('--gae-lambda',type=float,default=.95);parser.add_argument('--entropy-coefficient',type=float,default=.01);parser.add_argument('--mixed-combat-start',action='store_true');parser.add_argument('--value-coefficient',type=float,default=.5);parser.add_argument('--completion-first-reward',action='store_true');parser.add_argument('--easy-combat-curriculum',action='store_true');args=parser.parse_args()
    if not np.isfinite(args.learning_rate) or args.learning_rate<=0:parser.error('--learning-rate must be finite and positive')
    if args.include_original_openings:os.environ['BATTLE_RL_ORIGINAL_OPENINGS']='1'
    if not np.isfinite(args.gamma) or not 0<args.gamma<=1:parser.error('--gamma must be finite in (0, 1]')
    if not np.isfinite(args.gae_lambda) or not 0<=args.gae_lambda<=1:parser.error('--gae-lambda must be finite in [0, 1]')
    if not np.isfinite(args.entropy_coefficient) or args.entropy_coefficient<0:parser.error('--entropy-coefficient must be finite and nonnegative')
    if not np.isfinite(args.value_coefficient) or args.value_coefficient<0:parser.error('--value-coefficient must be finite and nonnegative')
    out=Path(args.out);out.mkdir(parents=True,exist_ok=True);gradient_error=max(gradient_check(entropy_coefficient=args.entropy_coefficient,value_coefficient=args.value_coefficient),gradient_check(balanced=True,entropy_coefficient=args.entropy_coefficient,value_coefficient=args.value_coefficient),gradient_check(hierarchical=True,entropy_coefficient=args.entropy_coefficient,value_coefficient=args.value_coefficient));env=Rules();evaluation_env=Rules()
    try:
        start=time.perf_counter();s=env.reset(0);cold=time.perf_counter()-start;n=len(env.actions);d=len(s['features']);net=Policy(d,n,entropy_coefficient=args.entropy_coefficient,value_coefficient=args.value_coefficient)
        assert d==len(env.info['featureNames'])
        if args.resume:
            old=dict(np.load(args.resume));old_info=json.loads((Path(args.resume).parent/'environment.json').read_text(encoding='utf-8'))
            validate_ability_schema(old_info,env.info)
            assert env.info['featureNames'][:len(old_info['featureNames'])]==old_info['featureNames'],'Feature prefix incompatible'
            net.p['w1'][:old['w1'].shape[0]]=old['w1'];net.p['w1'][old['w1'].shape[0]:]=0
            for k in ('b1','wv','bv'):net.p[k]=old[k].copy()
            old_ids={a['Key']:i for i,a in enumerate(old_info['catalog'])}
            for i,action in enumerate(env.actions):
                if action['Key'] in old_ids:net.p['wp'][:,i]=old['wp'][:,old_ids[action['Key']]];net.p['bp'][i]=old['bp'][old_ids[action['Key']]]
        if args.balance_action_kinds or args.factor_action_kinds or args.resume and 'action_kind_groups' in old:
            kinds=sorted({a['Kind'] for a in env.actions})
            net.p['action_kind_groups']=np.array([kinds.index(a['Kind']) for a in env.actions],dtype=np.int32)
        if args.factor_action_kinds or args.resume and 'wk' in old:
            net.enable_kind_factor(net.p['action_kind_groups'])
            if args.resume and 'wk' in old:
                old_kinds=sorted({a['Kind'] for a in old_info['catalog']})
                for kind in kinds:
                    if kind in old_kinds:
                        net.p['wk'][:,kinds.index(kind)]=old['wk'][:,old_kinds.index(kind)];net.p['bk'][kinds.index(kind)]=old['bk'][old_kinds.index(kind)]
        net.bias_kind(env.actions,'fight',args.fight_init_bias)
        (out/'environment.json').write_text(json.dumps(env.info,ensure_ascii=False,indent=2),encoding='utf-8')
        manifest={'scope':env.info['scope'],'resume':args.resume,'fight_initial_bias':args.fight_init_bias,'audit_interval_seconds':args.audit_seconds,'optimizer_state_restored':False,'source_sha256':{str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in [Path('tools/BattleRl/JourneyTraining.cs'),Path('tools/BattleRl/train_ppo.py'),Path('tools/BattleRl/trajectory_audit.py'),Path('tools/BattleRl/policy_diagnostics.py'),env.dll]}}
        manifest['action_distribution']='learned kind gate then conditional legal tuple' if 'wk' in net.p else 'kind-balanced legal base measure' if 'action_kind_groups' in net.p else 'flat masked logits'
        manifest['inference_execution']='single-state formal legal columns only; dense PPO gradients unchanged'
        manifest['greedy_decoding']='kind then tuple' if 'wk' in net.p else 'joint action argmax'
        manifest['entropy_regularizer']='kind entropy plus mean conditional entropy over available kinds' if 'wk' in net.p else 'joint action entropy'
        manifest['entropy_coefficient']=args.entropy_coefficient
        manifest['value_coefficient']=args.value_coefficient
        manifest['kind_head_initialization']='restored by original kind labels' if args.resume and 'wk' in old else 'neutral new gate; old tuple logits retained' if 'wk' in net.p else 'not applicable'
        manifest['reward_definition']=env.info.get('rewardDefinition','legacy rules DLL; inspect frozen rules source')
        if args.completion_first_reward and not all(term in manifest['reward_definition'] for term in ('kill +10', 'formal whole-group win rising edge +20', 'no-kill terminal -5')):
            raise ValueError('Completion reward requires the native full-scope journey reward contract')
        manifest['completion_first_reward']=args.completion_first_reward
        manifest['training_reward_definition']='kill +2 instead of +10; replace no-kill terminal -5 with no-formal-whole-win terminal -10; all other native terms retained' if args.completion_first_reward else manifest['reward_definition']
        manifest['source_sha256']['tools/BattleRl/training_reward.py']=hashlib.sha256(Path('tools/BattleRl/training_reward.py').read_bytes()).hexdigest()
        manifest['include_original_openings']=os.environ.get('BATTLE_RL_ORIGINAL_OPENINGS')=='1'
        manifest['native_payment_inputs']=os.environ.get('BATTLE_RL_NATIVE_PAYMENTS')=='1'
        manifest['native_combat_conversions']=os.environ.get('BATTLE_RL_NATIVE_CONVERSIONS')=='1'
        manifest['native_energy_unit_targets']=os.environ.get('BATTLE_RL_NATIVE_ENERGY_TARGETS')=='1'
        manifest['source_sha256']['tools/BattleRl/JourneyTraining.CombatConversions.cs']=hashlib.sha256(Path('tools/BattleRl/JourneyTraining.CombatConversions.cs').read_bytes()).hexdigest()
        manifest['source_sha256']['tools/BattleRl/JourneyTraining.NativePayments.cs']=hashlib.sha256(Path('tools/BattleRl/JourneyTraining.NativePayments.cs').read_bytes()).hexdigest()
        manifest['ppo_update_execution']='batch legal-union equivalent gradients; complete Adam parameters and history retained'
        manifest['epoch_stop_measure']='updated policy nonnegative sampled KL over complete frozen rollout; threshold0.03'
        if args.easy_combat_curriculum and not args.coverage_window:raise ValueError('Easy combat curriculum requires coverage sampling')
        manifest['easy_combat_curriculum']=args.easy_combat_curriculum
        manifest['curriculum_selection']='Half balanced lower public enemy count/armor/attack; half balanced original novelty; no actor/evaluation overrides' if args.easy_combat_curriculum else 'balanced original novelty'
        manifest['coverage_window']=args.coverage_window
        manifest['mixed_combat_start']=args.mixed_combat_start
        manifest['curriculum_prefix_scope']='Every other training episode only: actual formal fight entry retained in trace, excluded from PPO samples/rewards and sampler activation counts. Evaluation has no forced prefix.'
        manifest['learning_rate']=args.learning_rate
        manifest['gamma']=args.gamma;manifest['gae_lambda']=args.gae_lambda
        manifest['source_sha256']['tools/BattleRl/coverage_sampler.py']=hashlib.sha256(Path('tools/BattleRl/coverage_sampler.py').read_bytes()).hexdigest()
        for original_source in ('resources/text_json/monster.json','AutomationConfigs/original_enemy_combat_oracle_20261004.json','Assets/GameData/BattleDemo/original_monsters.json'):
            source_path=Path(original_source)
            if source_path.exists():manifest['source_sha256'][original_source]=hashlib.sha256(source_path.read_bytes()).hexdigest()
        if args.resume:manifest['resume_model_sha256']=hashlib.sha256(Path(args.resume).read_bytes()).hexdigest()
        (out/'run_manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
        baseline,_=evaluate(env,net,args.eval_episodes,random=True);untrained,_=evaluate(env,net,args.eval_episodes)
        rng=np.random.default_rng(7);s=env.reset(args.seed_start);episode_seed=args.seed_start+1;curve=[];episodes=0;wins=0;rollout_seconds=update_seconds=0.;total=0
        prior=None
        if args.coverage_prior:
            if not args.coverage_window:raise ValueError('Prior training counters require coverage sampling')
            prior_path=Path(args.coverage_prior)
            prior_manifest=json.loads((prior_path.parent/'run_manifest.json').read_text(encoding='utf-8'))
            prior_sources=prior_manifest['source_sha256']
            if hashlib.sha256(env.dll.read_bytes()).hexdigest() not in [value for key,value in prior_sources.items() if key.endswith('BattleRl.dll')]:
                raise ValueError('Prior training counters used different formal rules; keep them separate')
            original_path='resources/text_json/monster.json'
            if prior_sources.get(original_path)!=hashlib.sha256(Path(original_path).read_bytes()).hexdigest():
                raise ValueError('Prior training counters used different original enemy data')
            prior=json.loads(prior_path.read_text(encoding='utf-8'))
            manifest['coverage_prior']={'path':str(prior_path),'sha256':hashlib.sha256(prior_path.read_bytes()).hexdigest(),
                'scope':'only cumulative actual training counts, not a new-run coverage or consumer claim'}
            (out/'run_manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
        sampler=CoverageSampler(args.seed_start,args.coverage_window,prior=prior,easy_every_other=args.easy_combat_curriculum) if args.coverage_window else None
        episode_seed=args.seed_start;initial_prefix=[];curriculum_episode=0
        reward_objective=CompletionReward(args.completion_first_reward)
        def reset_training():
            nonlocal episode_seed,initial_prefix,curriculum_episode
            if sampler:
                seed,row=sampler.select(env)
                with (out/'curriculum_selections.jsonl').open('a',encoding='utf-8') as f:f.write(json.dumps(row,ensure_ascii=False)+'\n')
            else:seed=episode_seed
            episode_seed=seed+1
            state=env.reset(seed)
            state,initial_prefix=prepare_training_start(env,state,args.mixed_combat_start and curriculum_episode%2==0)
            reward_objective.reset()
            curriculum_episode+=1
            return state
        s=reset_training()
        train_start=time.perf_counter();last_audit=train_start;audits=[];latest_training_episodes={};reviewed_seeds=set();coverage={'heroes':set(),'enemies':set(),'effects':set(),'inputs':set(),'skills':set(),'units':set(),'unit_abilities':set(),'consumers':set(),'encounter_sizes':set(),'inventory_families':set(),'starting_decks':set(),'turns':set(),'rounds':set()};episode_trace=list(initial_prefix)
        while total<args.steps:
            batch=[];start=time.perf_counter()
            for _ in range(min(1024,args.steps-total)):
                x,m=inputs(s,n);p,value=net.action_forward(x,s['legal']);a=int(rng.choice(n,p=p))
                try:nxt=env.step(a)
                except Exception as error:
                    try:actual=env.call(op='state')
                    except Exception as observation_error:actual={'observation_error':str(observation_error)}
                    (out/'protocol_rule_failure.json').write_text(json.dumps({'scope':env.info['scope'],'seed':episode_seed-1,'before':s,'action':env.actions[a],'actual':actual,'trace':episode_trace,'error':str(error)},ensure_ascii=False,indent=2),encoding='utf-8')
                    raise
                coverage['heroes'].add(str(s['state'].get('hero',s['state'].get('hero.id'))));coverage['enemies'].add(str(s['state'].get('enemy','pilot')));coverage['turns'].add(str(s['state'].get('turns',1)))
                action=env.actions[a]
                if sampler:sampler.observe(s['state'],action,nxt['state'])
                payment=s['state'].get('paymentSelection')
                if action['Kind'] in ('convertMovement','convertInfluence'):
                    coverage['consumers'].add(action['Card']+':'+action['Kind']+':'+action['Choice'])
                if (payment and action['Kind'] in ('play','unit')
                        and payment['skill'] not in s['state']['usedSkillsTurn']
                        and payment['skill'] in nxt['state']['usedSkillsTurn']
                        and nxt['state'].get('paymentSelection') is None):
                    coverage['skills'].add(payment['skill']+':native_payment')
                    coverage['consumers'].add(payment['skill']+':payment:'+action['Card']+':'+action['Choice'])
                coverage['inventory_families'].add(s['state'].get('setup','legacy'))
                if s.get('steps')==0:
                    owned=[c['id'] for zone in s['state'].get('zones',{}).values() for c in zone if not c['wound']]
                    coverage['starting_decks'].add(s['state'].get('hero','')+':'+','.join(sorted(owned)))
                coverage['rounds'].add(str(s['state'].get('rounds',1)))
                training_reward=reward_objective.step(s['state'],nxt['state'],nxt['reward'],nxt['done'])
                episode_trace.append({'before':s['state'],'action':action,'after':nxt['state'],'reward':nxt['reward'],'training_reward':training_reward})
                if action['Kind']=='play':coverage['effects'].add(action['Card']+':'+action['Choice'])
                if action['Kind'] in ('artifact','boxUse'):coverage['effects'].add(action['Card']+':'+action['Choice']+':'+str(action['Option']))
                if action['Kind'] in ('bannerUse','protectionCleanup'):coverage['consumers'].add(action['Card']+':'+action['Kind']+':'+str(action['Option']))
                if action['Kind']=='roundFinish':
                    for unit,take in zip(s['state']['units'],s['state']['returningBanners']):
                        if unit['banner']:coverage['consumers'].add(unit['banner']+(':round_return' if take else ':round_keep'))
                if action.get('Input'):coverage['inputs'].add(action['Card']+':'+action['Choice']+':'+action['Input'])
                if action['Kind']=='extraSource':coverage['consumers'].add('earned_extra_source:'+str(action['Targets'])+':'+str(action['Option']))
                if action['Kind'] in ('skill','skillSideways','skillFork','skillBullseye','puppetKeep','puppetUse'):coverage['skills'].add(action['Card']+':'+str(action['Option']))
                if action['Kind'] in ('unit','unitLeadership'):
                    coverage['units'].add(action['Card']);coverage['unit_abilities'].add(action['Card']+':'+action['Choice']+':'+str(action['Option'])+':'+action.get('Input',''))
                if action['Kind']=='soulGuideBlock':coverage['consumers'].add('hero_skill_pool_003:00:actual_block')
                if action['Kind']=='explosiveArmor':coverage['consumers'].add('advanced_card_041:explosiveArmor:'+('decline' if action['Targets']<0 else 'target'))
                if action['Kind'] in ('block','blockSelected') and any(p.get('skill')=='hero_skill_pool_003:06' for p in s['state']['blocks']) and not any(p.get('skill')=='hero_skill_pool_003:06' for p in nxt['state']['blocks']):
                    coverage['consumers'].add('hero_skill_pool_003:06:puppet_actual_block:'+s['state']['enemies'][action['Targets']]['id'])
                if action['Kind']=='unitLeadership':coverage['skills'].add('hero_skill_pool_002:06:unit:'+action['Card']+':'+action['Choice']+':'+str(action['Option']))
                if action['Kind']=='offeredUnit':
                    coverage['unit_abilities'].add(action['Card']+':from_offer:'+action['Choice']+':'+str(action['Option'])+':'+action.get('Input',''))
                    coverage['consumers'].add('original_offered_unit:'+action['Card']+':'+action['Choice'])
                if action['Kind']=='heal':coverage['consumers'].add('healing:'+('hero' if action['Targets']<0 else s['state']['units'][action['Targets']]['id']))
                if action['Kind']=='advance':
                    before_units={u['id']:u for u in s['state'].get('units',[])}
                    for unit in nxt['state'].get('units',[]):
                        if unit['wounds']>before_units[unit['id']]['wounds']:coverage['consumers'].add('damage:'+unit['id'])
                actual_enemies=s['state'].get('enemies',[])
                coverage['enemies'].update(e['id'] for e in actual_enemies)
                if action['Kind']=='fight':coverage['encounter_sizes'].add(str(len(actual_enemies)))
                if action['Kind']=='attack':
                    for source in classify_attack_sources(episode_trace):
                        if source['step']==len(episode_trace)-1:coverage['consumers'].add((source['card'] or source['skill'] or 'unknown')+':'+source['mode']+':attack:'+source['targets'])
                if action['Kind']=='block' and actual_enemies:coverage['consumers'].add('block:'+actual_enemies[action['Targets']]['id']+':slot'+str(action['Option']))
                batch.append((x,m,a,np.log(p[a]),value,training_reward,nxt['done']))
                if nxt['done']:
                    if 'zones' in nxt['state']:
                        report=audit(episode_trace)
                        report['curriculum_prefix_operations']=sum(bool(t.get('curriculum_initialization')) for t in episode_trace)
                        report['policy_selected_fights']=sum(t['action']['Kind']=='fight' and not t.get('curriculum_initialization',False) for t in episode_trace)
                        if report['errors']:
                            (out/'training_rule_failure.json').write_text(json.dumps({'seed':episode_seed-1,'report':report,'trace':episode_trace},ensure_ascii=False,indent=2),encoding='utf-8')
                            raise RuntimeError('Independent transition audit failed; see training_rule_failure.json')
                        latest_training_episodes[nxt['state']['hero']]={'seed':episode_seed-1,'report':report,'trace':episode_trace}
                        for skill in ('hero_skill_pool_000:06','hero_skill_pool_000:07'):
                            if any(t['before'].get('paymentSelection',{} ) and t['before']['paymentSelection']['skill']==skill and t['action']['Kind'] in ('play','unit') and skill not in t['before']['usedSkillsTurn'] and skill in t['after']['usedSkillsTurn'] and t['after'].get('paymentSelection') is None for t in episode_trace):
                                evidence=out/('first_actual_native_payment_'+skill.split(':')[-1]+'.json')
                                if not evidence.exists():evidence.write_text(json.dumps({'scope':env.info['scope'],'seed':episode_seed-1,'report':report,'trace':episode_trace},ensure_ascii=False),encoding='utf-8')
                        for conversion_kind in ('convertMovement','convertInfluence','explosiveArmor'):
                            if any(t['action']['Kind']==conversion_kind for t in episode_trace):
                                evidence=out/('first_actual_'+conversion_kind+'_consumer.json')
                                if not evidence.exists():evidence.write_text(json.dumps({'scope':env.info['scope'],'seed':episode_seed-1,'report':report,'trace':episode_trace},ensure_ascii=False),encoding='utf-8')
                        for rally_mode in (0,1):
                            if any(t['action']['Kind']=='skill' and t['action'].get('Card')=='hero_skill_pool_002:02' and t['action']['Option']==rally_mode for t in episode_trace):
                                evidence=out/('first_actual_native_rally_'+('ready' if rally_mode==0 else 'heal')+'.json')
                                if not evidence.exists():evidence.write_text(json.dumps({'scope':env.info['scope'],'seed':episode_seed-1,'report':report,'trace':episode_trace,'limit':'actual native activation; later same-unit consumer must be separately reviewed'},ensure_ascii=False),encoding='utf-8')
                        for special_kind,special_card,label in (('boxUse',None,'mystic_box'),('artifact','items_022','public_headband')):
                            for choice in ('normal','burn'):
                                if any(t['action']['Kind']==special_kind and (special_card is None or t['action']['Card']==special_card) and t['action']['Choice']==choice for t in episode_trace):
                                    evidence=out/f'first_actual_{label}_{choice}_use.json'
                                    if not evidence.exists():evidence.write_text(json.dumps({'scope':env.info['scope'],'seed':episode_seed-1,'report':report,'trace':episode_trace},ensure_ascii=False),encoding='utf-8')
                        if any(t['action']['Kind']=='learn' or t['action']['Kind'] in ('play','artifact') and t['before']['acquired']!=t['after']['acquired'] for t in episode_trace):
                            evidence=out/'first_actual_supply_acquisition.json'
                            if not evidence.exists():evidence.write_text(json.dumps({'scope':env.info['scope'],'seed':episode_seed-1,'report':report,'trace':episode_trace},ensure_ascii=False,indent=2),encoding='utf-8')
                        if any(t['action']['Kind']=='artifact' and t['action']['Card']=='items_013' and any(later['action']['Kind']=='play' and later['action']['Card'] in set(t['after']['acquired'])-set(t['before']['acquired']) for later in episode_trace[i+1:]) for i,t in enumerate(episode_trace)):
                            evidence=out/'first_actual_wisdom_book_acquired_card_consumer.json'
                            if not evidence.exists():evidence.write_text(json.dumps({'scope':env.info['scope'],'seed':episode_seed-1,'report':report,'trace':episode_trace},ensure_ascii=False,indent=2),encoding='utf-8')
                        if any(source['card'].startswith('magic_') and source['mode'].startswith('play:') for source in classify_attack_sources(episode_trace)):
                            evidence=out/'first_actual_spell_effect_attack_consumer.json'
                            if not evidence.exists():evidence.write_text(json.dumps({'scope':env.info['scope'],'seed':episode_seed-1,'report':report,'trace':episode_trace},ensure_ascii=False,indent=2),encoding='utf-8')
                        if any(t['action']['Kind']=='block' and any(p.get('skill')=='hero_skill_pool_003:06' for p in t['before']['blocks']) and not any(p.get('skill')=='hero_skill_pool_003:06' for p in t['after']['blocks']) for t in episode_trace):
                            evidence=out/'first_actual_puppet_block_consumer.json'
                            if not evidence.exists():evidence.write_text(json.dumps({'scope':env.info['scope'],'seed':episode_seed-1,'report':report,'trace':episode_trace},ensure_ascii=False),encoding='utf-8')
                        for native_kind in ('skillSideways','unitLeadership','skillFork','skillBullseye','soulGuideBlock','puppetKeep','puppetUse'):
                            if any(t['action']['Kind']==native_kind for t in episode_trace):
                                evidence=out/('first_actual_'+native_kind+'_use.json')
                                if not evidence.exists():evidence.write_text(json.dumps({'scope':env.info['scope'],'seed':episode_seed-1,'report':report,'trace':episode_trace},ensure_ascii=False,indent=2),encoding='utf-8')
                        if any(source['mode'].startswith('unit_ability:') for source in classify_attack_sources(episode_trace)):
                            evidence=out/'first_actual_unit_attack_consumer.json'
                            if not evidence.exists():evidence.write_text(json.dumps({'scope':env.info['scope'],'seed':episode_seed-1,'report':report,'trace':episode_trace},ensure_ascii=False,indent=2),encoding='utf-8')
                    episode_trace=[]
                    episodes+=1;wins+=nxt['won']
                    s=reset_training();episode_trace=list(initial_prefix)
                else:s=nxt
            rollout_seconds+=time.perf_counter()-start;total+=len(batch)
            if sampler:(out/'actual_use_curriculum.json').write_text(json.dumps(sampler.report(),ensure_ascii=False,indent=2),encoding='utf-8')
            x,m,a,old,v,reward,done=map(np.array,zip(*batch));_,_,next_value=net.forward(np.array(s['features'])[None],inputs(s,n)[1][None])
            adv=estimate_advantages(reward,v,done,next_value[0],args.gamma,args.gae_lambda)
            returns=adv+v;adv_std=float(adv.std());adv=(adv-adv.mean())/(adv_std+1e-8);start=time.perf_counter();kl=0;update_reports=[]
            for epoch in range(6):
                for idx in np.array_split(rng.permutation(len(batch)),max(1,len(batch)//128)):
                    loss,g,kl=net.loss_grad_legal(x[idx],m[idx],a[idx],old[idx],adv[idx],returns[idx]);update_reports.append(net.update(g,lr=args.learning_rate))
                epoch_kl=net.rollout_kl_legal(x,m,a,old)
                if epoch_kl>.03:break
            update_seconds+=time.perf_counter()-start
            print(json.dumps({'optimizer_steps':total,'updates':len(update_reports),'epochs':epoch+1,
                              'last_minibatch_kl':kl,'updated_rollout_kl':epoch_kl,'raw_advantage_std':adv_std,
                              'return_mean':float(returns.mean()),'value_mean':float(v.mean()),
                              'clipped_updates':sum(r['clip_scale']<1 for r in update_reports),
                              'mean_update':{k:float(np.mean([r[k] for r in update_reports])) for k in update_reports[0]}}),flush=True)
            if 'zones' in s['state'] and (time.perf_counter()-last_audit>=args.audit_seconds or total==args.steps):
                reports=[]
                # Every completed training episode was checked above. Persist fresh
                # actual training evidence, rather than simulating seven extra games
                # every minute merely to repeat the same rule checks.
                for hero_index,entry in enumerate(latest_training_episodes.values()):
                    seed=entry['seed']
                    if seed in reviewed_seeds:continue
                    trace=entry['trace'];report=entry['report'];reports.append({'seed':seed,'source':'completed_training_episode',**report,'policy_behavior':summarize_policy_trace(trace)});reviewed_seeds.add(seed)
                    (out/f'audit_{total}_{hero_index}.json').write_text(json.dumps({'seed':seed,'report':report,'trace':trace},ensure_ascii=False),encoding='utf-8')
                audits.append({'steps':total,'reports':reports});last_audit=time.perf_counter()
                (out/'trajectory_audits.json').write_text(json.dumps(audits,ensure_ascii=False,indent=2),encoding='utf-8')
                print(json.dumps({'trajectory_review_steps':total,'checks':sum(r['checks'] for r in reports),'errors':sum(len(r['errors']) for r in reports),'gaps':sum(len(r['adapter_gaps']) for r in reports),'longest_planning':max((r['policy_behavior']['longest_consecutive_planning'] for r in reports),default=0)}),flush=True)
                if any(r['errors'] for r in reports):raise RuntimeError('Periodic trajectory audit failed; retained traces need review')
            if total%4096==0 or total==args.steps:
                np.savez(out/'policy_checkpoint.npz',**net.p)
                np.savez(out/f'policy_checkpoint_{total}.npz',**net.p)
                stats,_=evaluate(evaluation_env,net,min(100,args.eval_episodes),trace_out=out/f'evaluation_{total}');curve.append({'steps':total,**stats});print(json.dumps(curve[-1]),flush=True)
            print(json.dumps({'training_steps':total,'episodes':episodes,'seconds':time.perf_counter()-train_start,'coverage':{k:len(v) for k,v in coverage.items()}}),flush=True)
        training_seconds=time.perf_counter()-train_start
        np.savez(out/'policy_ppo.npz',**net.p)
        loaded=Policy(d,n);loaded.p=dict(np.load(out/'policy_ppo.npz'))
        evaluation,_=evaluate(evaluation_env,loaded,args.eval_episodes);replay_stats,trace=evaluate(evaluation_env,loaded,1,seed=123456,save_trace=True)
        (out/'replay.json').write_text(json.dumps({'scope':env.info['scope'],'algorithm':'PPO','seed':123456,'result':replay_stats,'trace':trace},ensure_ascii=False,indent=2),encoding='utf-8')
        metrics={'scope':env.info['scope'],'algorithm':f'masked PPO (clip 0.2, GAE {args.gae_lambda}, gamma {args.gamma})','device':'CPU / NumPy','steps':total,'training_episodes':episodes,'training_seconds':training_seconds,'steps_per_second':total/training_seconds,'rollout_seconds':rollout_seconds,'update_seconds':update_seconds,'cold_reset_mask_seconds':cold,'features':d,'actions':n,'mask_cache_states':s['maskCacheStates'],'baseline_random':baseline,'untrained_greedy':untrained,'evaluation_loaded_ppo':evaluation,'replay':replay_stats,'curve':curve,'gradient_check_max_error':gradient_error,'model_bytes':(out/'policy_ppo.npz').stat().st_size}
        if args.resume:metrics['initial_policy_greedy']=metrics.pop('untrained_greedy')
        metrics['warm_start_model']=args.resume;metrics['fight_initial_bias']=args.fight_init_bias;metrics['audit_interval_seconds']=args.audit_seconds
        metrics['gamma']=args.gamma;metrics['gae_lambda']=args.gae_lambda;metrics['entropy_coefficient']=args.entropy_coefficient;metrics['value_coefficient']=args.value_coefficient
        metrics['seed_start']=args.seed_start;metrics['next_training_seed']=episode_seed;metrics['learning_rate']=args.learning_rate
        metrics['completion_first_reward']=args.completion_first_reward
        if sampler:metrics['next_training_seed']=sampler.cursor;metrics['coverage_window']=args.coverage_window
        (out/'metrics.json').write_text(json.dumps(metrics,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(metrics,indent=2),flush=True)
        (out/'training_coverage.json').write_text(json.dumps({k:sorted(v) for k,v in coverage.items()},ensure_ascii=False,indent=2),encoding='utf-8')
        if not replay_stats['wins'] and os.environ.get('BATTLE_RL_PROTOCOL','--serve')=='--serve':raise RuntimeError('PPO has not won the replay fixture; do not claim scene acceptance')
    finally:env.close();evaluation_env.close()
if __name__=='__main__':main()
