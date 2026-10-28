"""Procedural original single-battle tasks with empirical, recoverable curriculum."""
import copy
import json
from pathlib import Path
import numpy as np
from certified_task_sampler import CertifiedTaskSampler
from coverage_tasks import STREAMS, combination, split
from task_feasibility import digest, file_hash, SEQUENCE_GOAL
from original_data_signature import data_signature


class DynamicSingleCurriculum(CertifiedTaskSampler):
    def __init__(self, certificates, rules, output, state=None):
        dynamic=(state or {}).get('dynamic')
        rows=dynamic['rows'] if dynamic else certificates
        super().__init__(rows,state)
        self.rules=rules;self.output=Path(output)
        self.inventory=json.loads(Path('AutomationConfigs/rl_full_coverage_inventory.json').read_text(encoding='utf-8-sig'))
        self.inventory_sha=file_hash('AutomationConfigs/rl_full_coverage_inventory.json')
        if dynamic and dynamic['inventory_sha256']!=self.inventory_sha:raise ValueError('Dynamic inventory changed')
        self.random=np.random.default_rng(6112026)
        self.generated=0;self.results={};self.updates=0
        if dynamic:
            self.random.bit_generator.state=dynamic['rng'];self.generated=dynamic['generated']
            self.results=dynamic['results'];self.updates=dynamic['updates']
        self.definition_keys={digest(combination(r['task']['definition'])) for r in rows}
        self.rules_sha=file_hash(rules.dll);self.catalog_sha=digest(rules.actions)
        self.data=data_signature()

    def draft(self):
        rng=self.random;inv=self.inventory
        hero=inv['heroes'][self.generated % len(inv['heroes'])]
        self.generated+=1
        fame=int(rng.choice([0,3,8,15,24,35,48]))
        level=1+sum(fame>=v for v in [3,8,15,24,35,48,63,80,99])
        cards=[c['id'] for c in inv['cards'] if c['category'] in ('advanced_card','magic','items')]
        units=[c['id'] for c in inv['cards'] if c['category'] in ('army','army_2')]
        enemies=[e['Id'] for e in inv['enemies']]
        choose=lambda values,n:[str(v) for v in rng.choice(values,size=n,replace=False)]
        # Independent loadout/resources and enemy draws; no assumption that a
        # sampled task is solvable. Original hero base deck remains native.
        return dict(HeroId=hero['Id'],OriginalOpening=False,
            **{key:int(rng.integers(0,2**31-256)) for key in STREAMS},
            EnemyGroups=[choose(enemies,int(rng.choice([1,1,1,2,2,3])))],
            Build=dict(Family='dynamic_single',Fame=fame,
                AdditionalCards=choose(cards,int(rng.integers(0,min(12,2*level)+1))),
                UnitIds=choose(units,int(rng.integers(0,min(3,1+(level-1)//2)+1))),
                SkillIds=choose([s['SourceId'] for s in hero['skills']],int(rng.integers(0,level//2+1))),
                Crystals={c:int(rng.integers(0,4)) for c in ('Red','Blue','Green','White')},
                InitialWounds=int(rng.integers(0,5))))

    def expand(self, count=32):
        admitted=0
        for _ in range(count*4):
            definition=self.draft();composition=digest(combination(definition))
            if composition in self.definition_keys:continue
            partition=split(definition)
            if partition!='train':
                self.log('held_out_candidates',dict(composition_id=composition,split=partition,definition=definition))
                continue
            try:root=self.rules.reset_task(definition)
            except RuntimeError as error:
                self.log('rejected_candidates',dict(definition=definition,status='illegal_or_adapter_failure_not_unsolvable',error=str(error)));continue
            if root['done'] or not root['legal']:
                self.log('rejected_candidates',dict(definition=definition,status='terminal_root_unknown'));continue
            binding=dict(generator='independent_original_factors_v1',seed=definition['RandomSeed'],definition=definition,
                rules_sha256=self.rules_sha,feature_schema=self.rules.info['featureSchema'],catalog_sha256=self.catalog_sha,
                initial_state_sha256=digest(root['state']),goal=SEQUENCE_GOAL)
            row=dict(task_id=digest(binding),composition_id=composition,task=binding,hero=root['state']['hero'],
                status='legal_reset_unknown',data_signature=self.data,
                admission=dict(legal_actions=len(root['legal']),native_done=False),
                scope='Native legal single-battle reset only; no solvability or training coverage credit')
            self.certificates.append(row);self.by_id[row['task_id']]=row;self.definition_keys.add(composition)
            self.log('admitted_tasks',row);admitted+=1
            if admitted>=count:break
        if self.data!=data_signature():raise ValueError('Rules data changed during dynamic admission')
        self.updates+=1
        return admitted

    def log(self,name,row):
        with (self.output/(name+'.jsonl')).open('a',encoding='utf-8') as f:f.write(json.dumps(row,ensure_ascii=False)+'\n')

    def probabilities(self):
        # Half focuses on an empirical learning frontier; a quarter revisits
        # all tasks, a quarter explores under-observed tasks. Never delete hard
        # tasks as impossible; sparse outcomes retain a Beta(1,1) prior.
        rates=[];fresh=[]
        for row in self.certificates:
            r=self.results.get(row['task_id'],{});n=r.get('attempts',0);w=r.get('wins',0)
            rates.append((w+1)/(n+2));fresh.append(n<4)
        p=np.asarray(rates);frontier=4*p*(1-p)
        uniform=np.full(len(p),1/len(p));novel=np.asarray(fresh,dtype=float)
        novel=novel/novel.sum() if novel.sum() else uniform
        return .25*uniform+.25*novel+.5*frontier/frontier.sum()

    def select(self,random):
        row=self.certificates[int(random.choice(len(self.certificates),p=self.probabilities()))]
        self.attempts[row['task_id']]+=1
        return row

    def observe(self,episode,episode_index):
        super().observe(episode,episode_index)
        result=self.results.setdefault(episode['task_id'],dict(attempts=0,wins=0,steps=0,last_episode=None))
        result['attempts']+=1;result['steps']+=len(episode['trace']);result['last_episode']=episode_index
        won=bool(episode['trace'][-1]['after'].get('taskProgress',{}).get('sequenceCompleted'))
        result['wins']+=won
        if won and self.by_id[episode['task_id']]['status']=='legal_reset_unknown':
            self.log('winning_rollout_candidates',dict(task_id=episode['task_id'],episode=episode_index,
                status='actual_training_win_requires_independent_replay',trace_file=f'episode_{episode_index}.json'))

    def state(self):
        state=super().state()
        state['dynamic']=dict(rows=self.certificates,results=self.results,rng=self.random.bit_generator.state,
            generated=self.generated,updates=self.updates,inventory_sha256=self.inventory_sha)
        return state
