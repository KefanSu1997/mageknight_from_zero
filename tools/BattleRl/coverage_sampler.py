"""Choose legal original inventories; only actual successful steps count as use.

Inventory inspection is curriculum selection, not an actor observation or a
teacher action. Never reset the live rollout while inspecting candidates.
"""
from collections import Counter

class CoverageSampler:
    def __init__(self,start,window=21,prior=None,easy_every_other=False):
        if window<7:raise ValueError('Need candidates for seven original heroes')
        self.cursor=start;self.window=window;self.heroes=Counter();self.uses=Counter();self.modes=Counter();self.enemies=Counter();self.selected=[];self.families=Counter()
        self.prior_counts={}
        self.easy_every_other=easy_every_other
        if prior is not None:
            for key,target in (('actual_original_uses',self.uses),('actual_modes',self.modes),('actual_fought_enemy_counts',self.enemies)):
                counts=prior[key]
                if not isinstance(counts,dict) or any(not isinstance(k,str) or type(v) is not int or v<0 for k,v in counts.items()):
                    raise ValueError('Prior curriculum must contain nonnegative actual training counters: '+key)
                target.update(counts);self.prior_counts[key]=dict(counts)
            # Fortitude's checkbox only reserves a future wound response.
            # Older histories counted the toggles as activations; retain the
            # original history but exclude these known planning operations.
            planning={k:v for k,v in self.modes.items() if k.startswith('items_018:bannerUse:')}
            total=sum(planning.values())
            if total>self.uses['items_018']:raise ValueError('Fortitude planning history exceeds recorded uses')
            if total:
                self.uses['items_018']-=total
                for key in planning:del self.modes[key]
            self.prior_planning_exclusion={'original_id':'items_018','count':total,'mode_counts':planning}

    def observe(self,b,a,n):
        if n.get('stopReason','').startswith('formal_'):return
        kind=a['Kind']
        if kind=='bannerUse' and a['Card']=='items_018':return
        if kind in ('convertMovement','convertInfluence'):
            resource='movement' if kind=='convertMovement' else 'influence'
            self.modes[a['Card']+':'+kind+':'+a['Choice']+':actual_paid='+str(b[resource]-n[resource])]+=1
        payment=b.get('paymentSelection')
        if kind=='explosiveArmor':
            self.modes['advanced_card_041:explosiveArmor:'+('decline' if a['Targets']<0 else 'target')]+=1
        if (kind in ('play','unit') and payment
                and payment['skill'] not in b.get('usedSkillsTurn',[])
                and payment['skill'] in n.get('usedSkillsTurn',[])
                and n.get('paymentSelection') is None):
            # Selecting inputs is not activation. Count only the real original
            # consumer that committed the fee/material and once-turn flag.
            self.uses[payment['skill']]+=1
            self.modes[payment['skill']+':payment:'+a['Card']+':'+a['Choice']+
                       ':required='+payment['required']+':source='+str(payment['source'])+
                       ':material='+str(payment['material'])+':die='+str(payment['die'])+
                       ':extra='+str(payment['extra'])]+=1
        if kind=='repeat':
            # The donor was permanently removed when the host was played.
            # Record its borrowed effect separately, never another physical play.
            self.modes['advanced_card_024:repeat:'+a['Card']+':'+a['Choice']+':'+str(a['Targets'])+':'+str(a['Option'])+':'+a.get('Input','')]+=1
        if kind in ('play','artifact','skill','skillSideways','skillFork','skillBullseye','puppetKeep','puppetUse','unit','unitLeadership','offeredUnit','bannerUse','protectionCleanup'):
            self.uses[a['Card']]+=1
            self.modes[a['Card']+':'+kind+':'+a['Choice']+':'+str(a['Option'])+':'+a.get('Input','')+(':targets='+str(a['Targets']) if kind=='skillFork' else '')]+=1
        if kind=='soulGuideBlock':self.modes['hero_skill_pool_003:00:soulGuideBlock:actual_consumer']+=1
        if kind=='unitLeadership':
            self.uses['hero_skill_pool_002:06']+=1
            self.modes['hero_skill_pool_002:06:unitLeadership:'+a['Card']+':'+a['Choice']+':'+str(a['Option'])+':'+a.get('Input','')]+=1
        if kind=='boxUse':
            self.uses['items_024']+=1
            self.modes['items_024:boxUse:'+a['Card']+':'+a['Choice']+':'+a.get('Input','')]+=1
        if kind=='fight':self.enemies.update(e['id'] for e in b['enemies'])

    def select(self,env):
        rows=[]
        easy=self.easy_every_other and len(self.selected)%2==0
        for seed in range(self.cursor,self.cursor+self.window):
            state=env.call(op='inspectInventory',seed=seed)['state']
            # Hand, own skills/units and visible current encounter only. A hidden
            # shuffled draw-pile order never selects a seed or enters the actor.
            ids=set(state['hand'])|set(state['skills'])|{u['id'] for u in state['units']}
            novelty=sum(1/(1+self.uses[id]) for id in ids)
            enemy_novelty=sum(1/(1+self.enemies[e['id']]) for e in state['enemies'])
            enemies=state['enemies']
            public_load=(len(enemies),sum(e.get('effectiveArmor') if e.get('effectiveArmor') is not None else e['armor'] for e in enemies),
                         sum(a['effectiveAttack'] for e in enemies for a in e['attacks'])) if easy else None
            balance=(-self.heroes[state['hero']],-self.families[state.get('setup','legacy')])
            # Only curriculum seed selection; no hidden deck order, teacher
            # action or modification to any original enemy/build/rule.
            key=balance+tuple(-v for v in public_load)+(novelty+enemy_novelty,-seed) if easy else balance+(novelty+enemy_novelty,-seed)
            rows.append((key,seed,state,sorted(ids),public_load))
        _,seed,state,ids,public_load=max(rows,key=lambda row:row[0])
        self.cursor+=self.window;self.heroes[state['hero']]+=1;self.families[state.get('setup','legacy')]+=1
        row={'seed':seed,'hero':state['hero'],'inventory_family':state.get('setup','legacy'),'visible_original_ids':ids,'initial_enemies':[e['id'] for e in state['enemies']],
             'previous_actual_uses':{id:self.uses[id] for id in ids},'candidate_count':len(rows),'next_cursor':self.cursor}
        row['curriculum_mode']='balanced_public_lower_load' if easy else 'balanced_original_coverage'
        row['selected_public_enemy_load']=list(public_load) if public_load is not None else None
        self.selected.append(row)
        return seed,row

    def report(self):return {'scope':'actual training steps only; inventory selection is not effect/consumer coverage',
        'counter_history':'current run plus explicitly inherited prior training counts' if self.prior_counts else 'current run only',
        'prior_training_counts':self.prior_counts,
        'prior_planning_exclusion':getattr(self,'prior_planning_exclusion',None),
        'next_cursor':self.cursor,'selected_inventory_families':dict(self.families),'selected_episodes_by_hero':dict(self.heroes),'actual_original_uses':dict(self.uses),
        'actual_modes':dict(self.modes),'actual_fought_enemy_counts':dict(self.enemies)}
