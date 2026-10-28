"""Independent shared selection/frequency/physical-material transition checks."""


def audit_shared(before, after, action, check, step):
    kind,skill=action['Kind'],action['Card']
    if not kind.startswith('shared'):return
    old,new=before.get('sharedSkills'),after.get('sharedSkills')
    check(old is not None and new is not None,step,'shared operation has explicit public central state')
    if old is None or new is None:return
    if kind in ('sharedArm','sharedCancel'):
        check(before['usedSkillsRound']==after['usedSkillsRound'] and old['tokens']==new['tokens'],step,'shared selection is not activation/publication')
        check(all(before[k]==after[k] for k in ('zones','tokens','crystals','source','held')),step,'shared selection/cancel does not pay or move physical entities')
        color=('Red','Blue','Green','White')[action['Option']] if kind=='sharedArm' else None
        check(new['enhancementColor']==color,step,'selected enhancement color/cancel is public and exact')
    if kind=='sharedUse':
        check(skill in before['skills'] and skill not in before['usedSkillsRound'] and skill not in before['flippedSkills'] and skill in after['usedSkillsRound'],step,'shared owner use commits actual owned once-per-round skill')
        check(sum(t['skill']==skill for t in new['tokens'])==sum(t['skill']==skill for t in old['tokens'])+1,step,'shared owner publishes one actual central token')
        if skill=='hero_skill_pool_000:09':
            slots=[i for i in range(8) if action['Parts']&(1<<i)]
            chosen={before['handOrder'][i] for i in slots if i<len(before['handOrder'])}
            oldhand={c['instance']:c for c in before['zones']['hand']};newhand={c['instance'] for c in after['zones']['hand']}
            check(len(chosen)==len(slots)<=2 and all(oldhand[i]['wound'] for i in chosen),step,'Pain selects at most two distinct actual ordered hand wounds')
            check(set(oldhand)-newhand==chosen,step,'Pain removes exactly selected original physical hand wounds')
        if skill=='hero_skill_pool_002:09':
            check(len(old['terrain'])==len(new['terrain']) and all(y['type']==x['type'] and y['reduction']==x['reduction']+2 for x,y in zip(old['terrain'],new['terrain'])),step,'Calm adds exactly printed two terrain reduction')
    if kind=='sharedClaim':
        check(sum(t['skill']==skill for t in new['tokens'])==sum(t['skill']==skill for t in old['tokens'])-1,step,'shared claim returns exactly one existing central token')
        if skill=='hero_skill_pool_000:09':
            physical=before['handOrder'][action['Parts']]
            wound=next((c for c in before['zones']['hand'] if c['instance']==physical),None)
            check(wound is not None and wound['wound'] and wound in after['zones']['played'],step,'Pain consumer sideways plays the exact physical wound')
            if action['Choice']=='Move':check(after['movement']==before['movement']+3,step,'Pain printed sideways movement is three')
    if kind=='sharedHowlResolve':
        check(new['howls']==old['howls']-1 and before['phase']==after['phase']=='Ranged',step,'Howl resolves exactly one actual battle-start obligation')
