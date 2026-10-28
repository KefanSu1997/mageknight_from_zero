"""Independent transition checks. Audit-only zones never enter policy observations."""
from collections import Counter
import json
from pathlib import Path
from shared_skill_audit import audit_shared

_color_names={'红':'Red','蓝':'Blue','绿':'Green','白':'White'}
_spell_colors={card['id']:_color_names[card['mana_color'][0]] for card in json.loads(Path('resources/text_json/magic.json').read_text(encoding='utf-8'))}
_action_colors={card['id']:_color_names.get(card.get('required_crystals','')[:1]) for name in ('basic_card','advanced_card') for card in json.loads(Path('resources/text_json/'+name+'.json').read_text(encoding='utf-8'))}

def classify_attack_sources(trace):
    """Recover actual physical-card play mode; a sideways spell is not a spell effect."""
    modes={};unit_modes={};fork_modes={};records=[]
    for i,t in enumerate(trace):
        b,a,n=t['before'],t['action'],t['after']
        if a['Kind'] in ('unit','unitLeadership'):unit_modes[a['Card']]=('unit_leadership:hero_skill_pool_002:06:' if a['Kind']=='unitLeadership' else 'unit_ability:')+a['Choice']+':'+str(a['Option'])+':'+a.get('Input','')
        if a['Kind'] in ('play','sideways','artifact'):
            fields=dict(part.split('=',1) for part in a.get('Input','').split(';') if part)
            mode='sideways' if a['Kind']=='sideways' else a['Kind']+':'+a['Choice']
            if a['Card']=='items_023' and a['Kind']=='artifact':mode+=':'+fields['spellId']+':'+fields['spellChoice']
            if a['Card']=='items_022' and a['Kind']=='artifact':mode+=':'+fields['skill']+':'+str(a['Option'])
            prior={c['instance'] for c in b.get('zones',{}).get('played',[])}
            hand={c['instance']:c for c in b.get('zones',{}).get('hand',[])}
            for c in n.get('zones',{}).get('played',[]):
                if c['instance'] not in prior and c['instance'] in hand and hand[c['instance']]['id']==a['Card']:
                    modes[c['instance']]=mode
            if a['Kind']=='artifact' and a['Choice']=='burn':
                for c in n['zones']['removed']:
                    if c['instance'] in hand and hand[c['instance']]['id']==a['Card']:modes[c['instance']]=mode
        if a['Kind']=='skillFork':fork_modes[a['Card']]='skillFork:'+str(a['Targets'])
        if a['Kind']=='puppetUse':fork_modes[a['Card']]='puppetUse:'+a['Choice']+':'+str(a['Option'])
        if a['Kind']=='skillSideways':
            physical=b['handOrder'][a['Parts']]
            modes[physical]='skillSideways:'+a['Card']+':'+a['Choice']+':'+str(a['Option'])
        if a['Kind']=='boxUse':
            pending=b['mysticBox'];modes[pending['physical']]='boxUse:'+a['Card']+':'+a['Choice']+':'+a.get('Input','')
        if a['Kind']=='attack':
            targets=','.join(e['id'] for e in b.get('enemies',[]) if a['Targets']&(1<<e['index']))
            for j,part in enumerate(b.get('attacks',[])):
                if a['Parts']&(1<<j):records.append({'step':i,'card':part.get('card',''),'skill':part.get('skill','') or part.get('unit',''),'instance':part.get('cardInstance',-1),'mode':unit_modes.get(part['unit'],'unknown_unit_ability') if part.get('unit') else fork_modes.get(part.get('skill',''),modes.get(part.get('cardInstance',-1),'unknown')),'targets':targets})
    return records

def audit(trace):
    errors=[];gaps=[];checks=0;original=None;ambush_attack=ambush_block=0
    borrowed_time_bending=False
    def check(ok,i,rule):
        nonlocal checks
        checks+=1
        if not ok:errors.append({'step':i,'rule':rule})
    for i,t in enumerate(trace):
        b,a,n=t['before'],t['action'],t['after']
        fields=dict(part.split('=',1) for part in a.get('Input','').split(';') if part)
        audit_shared(b,n,a,check,i)
        if a['Kind']=='skillShapeshift':
            skill='hero_skill_pool_001:06';physical=b['handOrder'][a['Parts']]
            host=next((c for c in b['zones']['hand'] if c['instance']==physical),None)
            check(skill in b['skills'] and skill not in b['usedSkillsTurn'] and skill in n['usedSkillsTurn'],i,'Shapeshift consumes actual owned once-per-turn skill')
            check(host is not None and host['id'].startswith('basic_card_') and host in n['zones']['played'],i,'Shapeshift plays exactly the original physical basic deed')
            check(b['phase']==n['phase']==('Outside' if a['Targets']==0 else 'Melee' if a['Targets']==1 else 'Block'),i,'Shapeshift output uses the actual formal consumer phase')
            if 'materialSlot' in fields:
                material=b['handOrder'][int(fields['materialSlot'])]
                actual=next((c for c in b['zones']['hand'] if c['instance']==material),None)
                check(material!=physical and actual is not None and not actual['wound'] and actual in n['zones']['discard'] and actual not in n['zones']['played'],i,'Shapeshift original material is a separate actual nonwound deed discarded once')
        if a['Kind']=='explosiveArmor':
            check(b['explosiveArmorUses']>0,i,'explosive follow-up requires an actual earned pending use')
            if a['Targets']<0:
                check(n['explosiveArmorUses']==0 and n['enemies']==b['enemies'],i,'explicit explosive decline clears uses without reducing enemies')
            else:
                target=a['Targets'];enemy=b['enemies'][target];after=n['enemies'][target]
                check(not enemy['defeated'] and 'Fire' not in enemy['effectiveResistances'] and 'MagicResist' not in enemy['abilities'],i,'explosive target is an actual living nonimmune enemy')
                check(n['explosiveArmorUses']==b['explosiveArmorUses']-1 and enemy['instance']==after['instance'] and after['effectiveArmor']==max(1,enemy['effectiveArmor']-1),i,'explosive consumes exactly one earned use and reduces the same actual target armor')
                check(all(x==y for j,(x,y) in enumerate(zip(b['enemies'],n['enemies'])) if j!=target),i,'explosive leaves every other enemy unchanged')
            check(n['zones']==b['zones'] and n['tokens']==b['tokens'] and n['crystals']==b['crystals'] and n['phase']==b['phase'],i,'explosive follow-up does not replay its card, charge again or advance phase')
        if (a['Kind'] in ('artifact','boxUse') and a.get('Card')=='items_023'
                and a.get('Choice')=='burn' and fields.get('spellId')=='magic_016'
                and fields.get('spellChoice')=='strong' and not n.get('stopReason','').startswith('formal_')):
            borrowed_time_bending=True
        if (borrowed_time_bending and a['Kind']=='nextTurn'
                and n.get('turns',0)>b.get('turns',0)):
            returned={c['instance'] for c in n['zones']['hand']}
            check(all(c['instance'] in returned for c in b['zones']['played']),i,
                  'Borrowed Time Bending returns every other actually played physical card at formal turn end')
            borrowed_time_bending=False
        if a['Kind'] in ('convertInfluence','convertMovement'):
            conversion=b.get('combatConversions',{})
            check(bool(conversion),i,'combat conversion has actual observable original permission')
            check(all(b[k]==n[k] for k in ('zones','tokens','crystals','source','held','units','usedSkillsTurn')),i,'combat conversion spends existing points without fabricating card or mana payment')
            if a['Kind']=='convertInfluence':
                amount=a['Option'] or b['influence']
                check(b['phase']=='Block' and conversion.get('influenceAllowed') and amount>0,i,'Diplomacy conversion uses its actual permission in Block phase')
                check(b['influence']-n['influence']==amount and n['block']-b['block']==amount,i,'Diplomacy converts exactly paid influence one to one')
                before_blocks=Counter((x['value'],x['element']) for x in b['blocks'])
                after_blocks=Counter((x['value'],x['element']) for x in n['blocks'])
                check(after_blocks-before_blocks==Counter([(amount,conversion.get('influenceElement'))]) and not before_blocks-after_blocks,i,'Diplomacy creates exactly the selected original element contribution')
            else:
                ranged=a['Choice']=='ranged';cost=conversion.get('rangedCost' if ranged else 'meleeCost',0)
                amount=a['Option'] or (b['movement']//cost if cost>0 else 0)
                check(cost>0 and amount>0 and (b['phase'] in ('Ranged','Melee') if ranged else b['phase']=='Melee'),i,'Agility conversion uses actual phase and original movement cost')
                check(b['movement']-n['movement']==cost*amount,i,'Agility pays exactly actual movement before generating attack')
                attack_type='Ranged' if ranged else 'Melee'
                check(sum(x['value'] for x in n['attacks'] if x['type']==attack_type)-sum(x['value'] for x in b['attacks'] if x['type']==attack_type)==amount,i,'Agility generates exactly its original attack type and amount')
        if a['Kind'] in ('payment','cancelPayment'):
            excluded=('paymentSelection','planningRun')
            check({k:v for k,v in b.items() if k not in excluded}=={k:v for k,v in n.items() if k not in excluded},i,'payment input selection and cancellation do not change actual game state')
            check(n['planningRun']==b['planningRun']+1,i,'payment planning advances only its explicit planning counter once')
            pending=n.get('paymentSelection')
            if a['Kind']=='cancelPayment':
                check(b.get('paymentSelection') is not None and pending is None,i,'payment cancellation clears only an actual pending selection')
            else:
                check(b.get('paymentSelection') is None and pending is not None,i,'payment selection cannot silently replace an existing selection')
                if pending:
                    check(pending['skill']==a['Card'] and pending['skill'] not in b['usedSkillsTurn'],i,'payment input binds the actual unused original skill')
                    if pending['skill']=='hero_skill_pool_000:06':
                        order=b['handOrder'];slot=a['Parts']
                        check(0<=slot<len(order) and order[slot]==pending['material'],i,'Invocation material binds the actual ordered physical hand slot')
                        material=next((c for c in b['zones']['hand'] if c['instance']==pending['material']),None)
                        check(material is not None and pending['required'] in (('Red','Black') if material['wound'] else ('White','Green')),i,'Invocation actual wound and non-wound materials retain original color restrictions')
                    elif pending['skill']=='hero_skill_pool_000:07':
                        pair=(pending['source'],pending['required'])
                        check(pair in (('Red','Green'),('Green','Red'),('Blue','White'),('White','Blue')) or (b['dayPart']=='Day' and pair[0]=='Black' and pair[1] in ('Red','Blue','Green','White')) or (b['dayPart']=='Night' and pair==('Gold','Black')),i,'Polarization input retains original opposite colors and day/night restrictions')
        pending=b.get('paymentSelection')
        if pending and a['Kind'] in ('play','unit'):
            check(n.get('paymentSelection') is None,i,'actual original payment consumer clears its pending selection')
            check(pending['skill'] not in b['usedSkillsTurn'] and pending['skill'] in n['usedSkillsTurn'],i,'actual payment consumer commits original skill use exactly once per turn')
        if a['Kind']=='repeat':
            pending=b.get('repeated')
            check(bool(pending) and pending['original']==a['Card'] and pending['remaining']>0,i,'repeat binds actual pending original donor')
            if pending:
                if pending['remaining']==1:
                    check(n.get('repeated') is None,i,'final repeat completes exact original count')
                else:
                    expected=dict(pending,remaining=pending['remaining']-1,completed=pending['completed']+1)
                    check(n.get('repeated')==expected,i,'repeat decrements once and preserves original mode')
        if a['Kind']=='nextTurn' and b.get('repeated'):
            check(False,i,'turn cannot end with unresolved original repeated effects')
        if 'enemySupply' in b and 'enemySupply' in n:
            def supply_tokens(state):
                return [e['instance'] for zone in ('draw','discard','inUse') for e in state['enemySupply'][zone]]
            for state in (b,n):
                owned=supply_tokens(state)
                check(len(owned)==len(set(owned)),i,'summon physical supply zones are disjoint')
                summoned=state['summoning']['bindings']
                for binding in summoned:
                    actual=next((e for e in state['enemies'] if e['index']==binding['index']),None)
                    check(actual is not None and (actual['instance'],actual['id'])==(binding['token'],binding['tokenId']),i,'summon defender is the actual bound physical brown token')
                    check(any(e['instance']==binding['token'] for e in state['enemySupply']['inUse']),i,'active summoned token is owned by actual in-use supply')
            check(Counter(supply_tokens(b))==Counter(supply_tokens(n)),i,'summon transitions conserve actual physical supply tokens')
            if not b['summoning']['active'] and n['summoning']['bindings']:
                prior={e['instance'] for e in b['enemySupply']['draw']+b['enemySupply']['discard']}
                for binding in n['summoning']['bindings']:
                    check(binding['token'] in prior,i,'summon draws an existing actual brown token')
                    check(any(e['index']==binding['originalIndex'] and (e['instance'],e['id'])==(binding['summoner'],binding['summonerId']) for e in b['enemies']),i,'summon binding preserves its original actual parent')
            if b['summoning']['bindings'] and not n['summoning']['active']:
                for binding in b['summoning']['bindings']:
                    check(any(e['index']==binding['originalIndex'] and (e['instance'],e['id'])==(binding['summoner'],binding['summonerId']) for e in n['enemies']),i,'defense completion restores the same original summoner')
                    check(any(e['instance']==binding['token'] for e in n['enemySupply']['discard']),i,'defense completion discards the same summoned physical token')
        if a['Kind']=='skill' and a.get('Card')=='hero_skill_pool_002:02':
            target=a['Targets'];mode=a['Option'];skill=a['Card']
            check(b['phase']==n['phase']=='Outside',i,'native Rally only outside battle')
            check(skill in b['skills'] and skill not in b['usedSkillsRound'] and skill in n['usedSkillsRound'],i,'native Rally consumes owned once-per-round use')
            check(len(b['units'])==len(n['units']) and 0<=target<len(b['units']),i,'native Rally chooses actual owned unit slot')
            if 0<=target<len(b['units']) and len(b['units'])==len(n['units']):
                old_unit=b['units'][target];new_unit=n['units'][target]
                check(old_unit['id']==new_unit['id'] and not old_unit['destroyed'],i,'native Rally preserves original unit identity')
                check(all(b['units'][j]==n['units'][j] for j in range(len(b['units'])) if j!=target),i,'native Rally affects only selected unit')
                if mode==0:
                    check(not old_unit['ready'] and new_unit['ready'] and old_unit['wounds']==new_unit['wounds'],i,'native Rally readies selected exhausted unit without healing')
                elif mode==1:
                    check(old_unit['wounds']>0 and new_unit['wounds']==old_unit['wounds']-1,i,'native Rally heals one actual selected unit wound')
                else:check(False,i,'native Rally uses original ready or heal branch')
            check(all(b[k]==n[k] for k in ('zones','tokens','crystals','source','held','healing')),i,'native Rally has no deed mana or healing-point fee')
        if a['Kind']=='cumbersome':
            target=a['Targets'];attack=a['Option']
            check(b['phase']==n['phase']=='Block' and b['movement']>=1 and n['movement']==b['movement']-1,i,'cumbersome consumes one actual movement point in block phase')
            old=b['enemies'][target];new=n['enemies'][target]
            check(old['instance']==new['instance'] and old['attacks'][attack]['effectiveAttack']>0 and new['attacks'][attack]['effectiveAttack']==old['attacks'][attack]['effectiveAttack']-1,i,'cumbersome reduces the selected actual attack by exactly one')
            check(all(old_enemy==new_enemy for j,(old_enemy,new_enemy) in enumerate(zip(b['enemies'],n['enemies'])) if j!=target),i,'cumbersome leaves all other actual enemies unchanged')
            check(all(b[k]==n[k] for k in ('zones','tokens','crystals','source','held','units')),i,'cumbersome payment does not fabricate cards mana or unit wounds')
            if new['attacks'][attack]['effectiveAttack']==0:
                check(new['attacks'][attack]['blocked'],i,'zero attack from cumbersome is successfully blocked')
        if 'puppetStored' in b:
            for state in (b,n):
                tokens=state['puppetCandidates']+state['puppetStored']
                check(len(tokens)==len({e['instance'] for e in tokens}),i,'puppet candidate and kept zones contain distinct physical tokens')
            old_candidates={e['instance'] for e in b['puppetCandidates']}
            for enemy in n['puppetCandidates']:
                if enemy['instance'] not in old_candidates:
                    check(any(e['instance']==enemy['instance'] and e['defeated'] for e in n['enemies']),i,'new puppet candidate comes from a real defeated enemy instance')
            if a['Kind'] in ('puppetKeep','puppetUse'):
                skill='hero_skill_pool_003:06'
                check(a['Card']==skill and skill in b['skills'] and skill not in b['usedSkillsTurn'] and skill in n['usedSkillsTurn'],i,'native puppet operation uses owned skill once this turn')
                check(all(n[k]==b[k] for k in ('zones','tokens','crystals','source','held')),i,'native puppet has no deed or mana fee')
                zone='puppetCandidates' if a['Kind']=='puppetKeep' else 'puppetStored'
                selected=next((e for e in b[zone] if e['id']==a['Choice']),None)
                check(selected is not None,i,'puppet selects first actual physical token of the chosen original ID')
                if selected is not None:
                    check(n[zone]==[e for e in b[zone] if e['instance']!=selected['instance']],i,'puppet removes exactly the selected physical token from its prior zone')
                    if a['Kind']=='puppetKeep':
                        check(n['puppetStored']==b['puppetStored']+[selected],i,'puppet keep transfers the same defeated token into storage')
                    else:
                        check(n['puppetCandidates']==b['puppetCandidates'],i,'puppet consumption does not consume other defeated candidates')
                        if a['Option']==1:
                            added=n['attacks'][len(b['attacks']):]
                            check(b['phase']=='Melee' and len(added)==len(selected['attacks']),i,'puppet melee produces every original printed attack in attack phase')
                            check(all(p['value']==(raw['value']+1)//2 and p['element']==raw['element'] and p['type']=='Melee' and p['skill']==skill and not p['card'] and not p['unit'] for p,raw in zip(added,selected['attacks'])),i,'puppet melee preserves original attack elements and rounds halves upward')
                        else:
                            abilities=selected['abilities'];fire=any(x in abilities for x in ('FireResist','ColdFireResist'));ice=any(x in abilities for x in ('IceResist','ColdFireResist'))
                            element='ColdFire' if fire and ice else 'Ice' if fire else 'Fire' if ice else 'Physical'
                            added=n['blocks'][len(b['blocks']):]
                            value=(selected['armor']+1)//2
                            # The formal pool omits zero power; a printed zero
                            # armor token is consumed without creating a block.
                            expected=(not added if value==0 else
                                      len(added)==1 and added[0]['value']==value and added[0]['element']==element and added[0].get('skill')==skill)
                            check(a['Option']==2 and b['phase']=='Block' and expected,i,'puppet block uses half printed armor upward and opposite resistance element')
                            check(n['blocks'][:len(b['blocks'])]==b['blocks'],i,'puppet block preserves every prior block contribution')
            elif a['Kind'] in ('artifact','boxUse') and 'skill=hero_skill_pool_003:06' in a.get('Input',''):
                gaps.append({'step':i,'gap':'borrowed puppet physical consumer requires independent typed audit'})
            elif a['Kind'] not in ('puppetKeep','puppetUse'):
                check(n['puppetStored']==b['puppetStored'],i,'kept physical puppet tokens persist through other operations and real turn/round changes')
        # Reconstruct the printed once-per-turn Ambush permission from actual
        # operations for frozen v12 traces, which do not expose its context fields.
        # It boosts the first printed card attack OR block, never both.
        ambush_gain_attack=ambush_gain_block=0
        if a['Kind']=='play' and a['Card']!='advanced_card_023':
            if sum(p['value'] for p in n.get('attacks',[]))>sum(p['value'] for p in b.get('attacks',[])):
                ambush_gain_attack=ambush_attack;ambush_attack=ambush_block=0
            elif n.get('block',0)>b.get('block',0):
                ambush_gain_block=ambush_block;ambush_attack=ambush_block=0
        if a['Kind']=='play' and a['Card']=='advanced_card_023':
            enhanced=a['Choice']=='strong' or b['nextEnhanced']
            ambush_attack=2 if enhanced else 1;ambush_block=4 if enhanced else 2
        if a['Kind']=='nextTurn':ambush_attack=ambush_block=0
        if 'units' in b:
            prior={u['id']:u for u in b['units']};after={u['id']:u for u in n['units']}
            check(len(prior)==len(b['units']) and len(after)==len(n['units']),i,'configured original-unit curriculum retains distinct identities')
            check(len(n['units'])<=n['commandSlots'],i,'owned original units fit actual command slots')
            if a['Kind']=='offeredUnit':
                check(a['Card'] in b['unitOffer'] and b['unitOffer']==n['unitOffer'],i,'borrowed ability uses actual unchanged unit supply')
                check(b['offeredUnitUses']>0 and n['offeredUnitUses']==b['offeredUnitUses']-1 and prior.keys()==after.keys(),i,'supplied ability consumes exactly one permission without recruiting a ghost unit')
            if a['Kind']=='bannerUse':
                unit=b['units'][a['Targets']];updated=after[unit['id']]
                check(unit.get('bannerEffect',unit['banner'])==a['Card'] and updated['banner']==unit['banner'] and b['zones']['banners']==n['zones']['banners'],i,'attached banner use retains actual owned physical card')
                if a['Card']=='items_001':
                    check(b['phase']=='Block' and unit['ready'] and not updated['ready'] and n['fame']==b['fame']+1,i,'Fear banner exhausts actual ready unit and grants exactly one fame')
                    check(n['enemies'][a['Parts']]['attacks'][a['Option']]['cancelled'] and not n['enemies'][a['Parts']]['attacks'][a['Option']]['blocked'],i,'Fear cancels selected attack without marking a successful block')
                elif a['Card']=='items_003':
                    check(not b['inBattle'] and not unit['bannerSpent'] and updated['ready'] and updated['bannerSpent'] and unit['wounds']==updated['wounds'],i,'Courage readies same unit once per round without healing')
                elif a['Card']=='items_018':
                    check(updated['fortitudeSelected']==bool(a['Option']) and updated['wounds']==unit['wounds'],i,'Fortitude choice reserves protection without preemptively healing')
            if a['Kind']=='protectionCleanup':
                check(b['artifactFlags']['protectionBurned'] and n['artifactFlags']['removeNewWounds']==bool(a['Option']),i,'Protection cleanup choice requires actual burned banner')
            if a['Kind'] in ('unit','unitLeadership'):
                check(a['Card'] in prior and a['Card'] in after,i,'unit ability belongs to an actual owned original unit')
                if a['Card'] in after:check(not after[a['Card']]['ready'],i,'actual unit activation exhausts its command marker')
            if a['Kind']=='unitDamage':
                check(n['units']==b['units'],i,'damage reservation does not wound or exhaust units before confirmation')
                plan=n.get('unitDamagePlan') or []
                selected=[p for p in plan if p['target']==a['Targets'] and p['attack']==a['Parts']]
                enemy_attack=b['enemies'][a['Targets']]['attacks'][a['Parts']]
                forced=enemy_attack.get('forcedUnit') if enemy_attack.get('effectiveAttack',0)>0 else None
                check(bool(selected)==(a['Option']==0 or bool(forced)),i,'real damage plan preserves mandatory first damage even when optional assignment is cancelled')
                if a.get('Choice') or 'forcedUnit' in enemy_attack:
                    requested=[] if a['Option']==1 else [b['units'][int(slot)]['id'] for slot in a['Choice']] if a.get('Choice') else [u['id'] for u in b['units'] if not u['destroyed']]
                    expected=([forced]+[id for id in requested if id!=forced]) if forced else requested
                    check((selected[0]['units'] if selected else [])==expected,i,'actual damage order is mandatory bound Shocktroops first, followed by exact selected order')
            if a['Kind']=='nextTurn':
                for id in prior.keys() & after.keys():
                    check(prior[id]['wounds']==after[id]['wounds'],i,'formal turn and round transition preserves actual unit wounds')
                    if n['rounds']==b['rounds']:check(prior[id]['ready']==after[id]['ready'],i,'same-round new turn does not refresh exhausted unit')
            if a['Kind']=='heal' and a['Targets']>=0:
                id=b['units'][a['Targets']]['id'];check(after[id]['wounds']==prior[id]['wounds']-1,i,'unit healing removes one actual wound')
        check(b['hero']==n['hero'],i,'same actual hero across turns/rounds')
        if 'setup' in b:check(b['setup']==n['setup'],i,'declared initial inventory family remains unchanged across real turns')
        for state in (b,n):
            if 'zones' not in state:
                gaps.append({'step':i,'gap':'missing physical-zone audit fields'});continue
            flat=[c for zone in state['zones'].values() for c in zone];ids=[c['instance'] for c in flat]
            check(len(ids)==len(set(ids)),i,'physical card occurs in exactly one zone')
            if 'totalWounds' in state:check(state['totalWounds']==sum(c['wound'] for zone,cards in state['zones'].items() if zone!='removed' for c in cards),i,'active wound cost counts all physical owned wounds except removed, independent of hand relocation')
            normal={(c['instance'],c['id']) for c in flat if not c['wound']}
            if original is None:original=normal
            check(original<=normal,i,'original card identities conserved across actual turns/rounds')
            gained=Counter(card_id for instance,card_id in normal-original)
            check(gained==Counter(state.get('acquired',[])),i,'new physical cards require actual recorded formal reward acquisition')
            check(all(v>=0 for stock in (state['tokens'],state['crystals']) for v in stock.values()),i,'mana inventories nonnegative')
        kind=a['Kind']
        fields=dict(part.split('=',1) for part in a.get('Input','').split(';') if part)
        if kind=='extraSource':
            colors=['Red','Blue','Green','White','Gold','Black']
            face=colors[a['Targets']];chosen=colors[a['Option']]
            check(Counter(b['source'])-Counter(n['source'])==Counter([face]),i,'extra quota consumes one actual selected source face')
            check(n['held']==b['held'],i,'extra source leaves ordinary die/quota unchanged')
            check(n['extraDice']==b['extraDice']-1,i,'extra source consumes exactly one earned extra quota')
            delta={c:n['tokens'].get(c,0)-b['tokens'].get(c,0) for c in set(b['tokens'])|set(n['tokens'])}
            check(delta.get(chosen)==1 and sum(abs(v) for v in delta.values())==1,i,'extra source produces exactly its explicitly chosen token')
            check(n['crystals']==b['crystals'],i,'extra source does not alter crystal inventory')
            if face=='Gold':check(chosen=='Gold',i,'taken gold stays wild until actual payment')
        if kind=='play' and a['Card']=='basic_card_023' and 'material' in fields:
            leaving=[c for c in b['zones']['hand'] if c['instance'] not in {ncard['instance'] for ncard in n['zones']['hand']}]
            payment=b.get('paymentSelection')
            if payment and payment['skill']=='hero_skill_pool_000:06':
                fee=[c for c in leaving if c['instance']==payment['material']]
                check(len(fee)==1 and fee[0]['wound'] and payment['required']=='Red' and a['Choice'].startswith('strong'),i,'improvisation Invocation pays original enhanced red fee with exact separate wound')
                check(len(fee)==1 and fee[0] in n['zones']['discard'] and fee[0] not in n['zones']['played'],i,'improvisation Invocation exact wound payment enters discard, not played')
                check(n['tokens']==b['tokens'] and n['crystals']==b['crystals'],i,'improvisation Invocation immediately consumes red permission without residual or double payment')
                leaving=[c for c in leaving if c['instance']!=payment['material']]
            check(len(leaving)==2 and any(c['id']==fields['material'] and not c['wound'] for c in leaving),i,'improvisation consumes original host and one actual non-wound material')
            check(any(c['id']==fields['material'] and c['instance'] in {d['instance'] for d in n['zones']['discard']} for c in leaving),i,'selected actual improvisation material goes to discard')
        if kind=='play' and a['Card'] in ('advanced_card_020','advanced_card_029','advanced_card_027','advanced_card_025') and 'material' in fields:
            zone='discard' if a['Card']=='advanced_card_025' else 'removed'
            old_ids={c['instance'] for c in b['zones'][zone]}
            material=[c for c in n['zones'][zone] if c['instance'] not in old_ids and c['id']==fields['material']]
            check(len(material)==1,i,'selected actual original material enters its printed discard/removal destination')
            if zone=='removed':check(bool(material) and not material[0]['wound'] and fields['material'].startswith(('basic_card_','advanced_card_')),i,'permanent printed material is an original action, not a wound or spell')
        if kind=='play' and ('advanced' in fields or 'spells' in fields):
            strong=a['Choice'].startswith('strong') or b['nextEnhanced']
            expected=[]
            if a['Card']=='advanced_card_027' or a['Card']=='advanced_card_030' and not strong:expected=[fields['advanced']]
            if a['Card']=='advanced_card_025' and strong:expected=fields['spells'].split(',')
            if expected:
                offer=b['advancedOffer'] if 'advanced' in fields else b['spellOffer']
                check(all(card in offer for card in expected),i,'acquisition chooses actual previous original supply')
                check(Counter(n['acquired'])-Counter(b['acquired'])==Counter(expected),i,'actual original supply acquisition is recorded exactly')
        if kind=='play' and a['Card']=='advanced_card_033':
            chosen=fields.get('spells','').split(',');strong=a['Choice'].startswith('strong') or b['nextEnhanced']
            check(len(chosen)==(2 if strong else 1) and len(set(chosen))==len(chosen) and all(card in b['spellOffer'] for card in chosen),i,'printed Spell Forge selects one/two different actual supply spells')
            check(n['spellOffer']==b['spellOffer'] and n['acquired']==b['acquired'],i,'printed Spell Forge grants crystals and leaves spells in actual supply')
            awards=Counter(_spell_colors[card] for card in chosen if card in _spell_colors)
            fee=1 if strong and not b['nextEnhanced'] and 'Blue' not in b.get('artifactFlags',{}).get('infinite',[]) else 0
            fee_color='Blue'
            original_fee=fee
            payment=b.get('paymentSelection')
            if payment:
                check(fee==1 and payment['required']=='Blue',i,'Spell Forge native payment replaces its actual single printed Blue enhancement fee')
                if payment['skill']=='hero_skill_pool_000:06':
                    check(any(c['instance']==payment['material'] for c in n['zones']['discard']),i,'Spell Forge Invocation actually discards its exact physical payment material')
                    fee=0
                elif payment['die']>=0:
                    # RawState sorts source faces; the pending index refers to
                    # native Dice order, so these coordinates cannot be equated.
                    check(0<=payment['die']<len(b['source']) and payment['source'] in b['source'],i,'Spell Forge payment selects an existing source face and valid native die index')
                    native_order=b.get('auditSourceOrder')
                    if native_order is not None:
                        check(0<=payment['die']<len(native_order) and native_order[payment['die']]==payment['source'],i,'Spell Forge selected native die index has the actual declared face')
                        check(Counter(native_order)==Counter(b['source']),i,'native die order and source display describe the same actual source')
                    else:gaps.append({'step':i,'gap':'native payment die index-to-face order is not serialized; source-face consumption checked independently'})
                    check(Counter(b['source'])-Counter(n['source'])==Counter([payment['source']]),i,'Spell Forge actual payment consumes exactly its selected source die')
                    if payment['extra']:
                        check(n['extraDice']==b['extraDice']-1,i,'Spell Forge actual extra payment consumes one real extra quota')
                    else:
                        check(n['held']==payment['source'] and n['sourceUsedThisTurn'],i,'Spell Forge ordinary payment commits its actual held die and ordinary quota')
                    fee=0
                else:fee_color=payment['source']
            enhancement='hero_skill_pool_003:09'
            if enhancement not in b['usedSkillsRound'] and enhancement in n['usedSkillsRound']:
                old_shared=b.get('sharedSkills',{});new_shared=n.get('sharedSkills',{})
                token_key=lambda t:(t['skill'],t['owner'],t.get('color'),t['own'])
                old=Counter(map(token_key,old_shared.get('tokens',[])))
                new=Counter(map(token_key,new_shared.get('tokens',[])))
                bonus_valid=(original_fee==1 and enhancement in b['skills']
                    and old_shared.get('enhancementColor')=='Blue' and new_shared.get('enhancementColor') is None
                    and new-old==Counter([(enhancement,b['hero'],'Blue',True)]) and not old-new)
                check(bonus_valid,i,'Spell Forge paid enhancement publishes exact owned Blue token once')
                if bonus_valid:awards['Blue']+=1
            valid=False
            for crystal_fee in range(fee+1):
                if b['crystals'].get(fee_color,0)<crystal_fee:continue
                raw={color:b['crystals'].get(color,0)-(crystal_fee if color==fee_color else 0)+awards[color] for color in ('Red','Blue','Green','White')}
                expected={color:min(3,amount) for color,amount in raw.items()}
                overflow={color:max(0,amount-3) for color,amount in raw.items()}
                if not all(n['crystals'].get(color,0)==amount for color,amount in expected.items()):continue
                # Original rulebook p5 "Gain", item 2: a fourth crystal becomes
                # a matching temporary token, unlike the round-end mine timing.
                token_fee=fee-crystal_fee
                for pay_color in (fee_color,'Gold') if token_fee else (None,):
                    if pay_color and b['tokens'].get(pay_color,0)<token_fee:continue
                    colors=set(b['tokens'])|set(n['tokens'])|set(overflow)
                    valid|=all(n['tokens'].get(color,0)==b['tokens'].get(color,0)+overflow.get(color,0)-(token_fee if color==pay_color else 0) for color in colors)
            check(valid,i,'printed Spell Forge gains matching crystals, fourth crystals become same-color tokens, exact enhancement fee paid')
        if kind=='play' and a['Card']=='magic_012' or kind in ('artifact','boxUse') and fields.get('spellId')=='magic_012':
            color=fields.get('colors');face=fields.get('dice')
            check(color in ('Red','Blue','Green','White') and face==color,i,'original Mana Claim command selects and gains the same basic-color face')
            check(Counter(n['roundClaimedDice'])-Counter(b['roundClaimedDice'])==Counter([color]),i,'Mana Claim reserves exactly the selected actual basic-color die until round end')
            removed=Counter([color])
            payment=b.get('paymentSelection')
            if payment and payment['skill']=='hero_skill_pool_000:07' and payment['die']>=0:
                removed.update([payment['source']])
            check(Counter(b['source'])-Counter(n['source'])==removed,i,'Mana Claim removes its selected face plus any actual native source-die payment')
            deferred=fields.get('spellChoice',a['Choice']).endswith('_deferred')
            if deferred:check(n['tokensPerTurn'].get(color,0)==b['tokensPerTurn'].get(color,0)+1,i,'deferred Mana Claim stores one actual matching income for future turns')
            else:check(n['tokensPerTurn']==b['tokensPerTurn'],i,'immediate Mana Claim does not create deferred income')
        if kind=='learn':
            check(a['Card'] in b['advancedOffer'] and b['learning']&(2 if a['Option']==1 else 1)!=0,i,'learning uses actual original offer and active printed purchase permission')
            check(n['acquired']==b['acquired']+[a['Card']],i,'learning records exactly the formally purchased original card')
            check(b['influence']-n['influence']==(9 if a['Option']==1 else 6),i,'original Learning charges exactly printed influence 6/9')
            destination=n['zones']['hand'] if a['Option']==1 else n['zones']['discard']
            check(any(c['id']==a['Card'] for c in destination),i,'original Learning base goes to discard and enhanced to hand')
        if kind=='play' and a['Card'] in ('basic_card_009','basic_card_013') and 'dice' in fields:
            faces=fields['dice'].split(',');chosen=fields['colors'].split(',')
            count=1 if a['Card']=='basic_card_009' else 2
            check(len(faces)==len(chosen)==count and 'Gold' not in chosen,i,'printed conversion uses required distinct actual dice and non-gold targets')
            amount=2 if count==1 else 1
            original_color='Blue' if a['Card']=='basic_card_009' else 'White'
            fee=0 if b['nextEnhanced'] or original_color in b.get('artifactFlags',{}).get('infinite',[]) else 1
            original_fee=fee
            payment=b.get('paymentSelection');fee_color=original_color;fee_die=None
            if payment:
                check(fee==1 and payment['required']==original_color,i,'conversion native payment replaces exactly its printed enhancement color')
                if payment['skill']=='hero_skill_pool_000:06':
                    material=next((c for c in b['zones']['hand'] if c['instance']==payment['material']),None)
                    check(material is not None and not material['wound'] and any(c==material for c in n['zones']['discard']),i,'conversion Invocation discards its exact actual non-wound material for Blue or White')
                    fee=0
                elif payment['die']>=0:
                    fee_die=payment['source'];fee=0
                    order=b.get('auditSourceOrder')
                    if order is not None:
                        check(0<=payment['die']<len(order) and order[payment['die']]==fee_die,i,'conversion native payment binds its actual indexed source face')
                    else:gaps.append({'step':i,'gap':'conversion native payment source index order missing'})
                else:fee_color=payment['source']
            removed=Counter(faces)+Counter([fee_die] if fee_die else [])
            check(Counter(b['source'])-Counter(n['source'])==removed and not Counter(n['source'])-Counter(b['source']),i,'conversion removes exactly selected effect dice and any distinct native payment die')
            gained=Counter(chosen);gained={c:v*amount for c,v in gained.items()};valid=False
            # Krang's printed enhancement gains a crystal after the real fee
            # commit. Model fee consumption and gain together; unchanged final
            # crystal count is not a waived payment.
            enhancement='hero_skill_pool_003:09'
            awards=Counter()
            if enhancement not in b['usedSkillsRound'] and enhancement in n['usedSkillsRound']:
                old_shared=b.get('sharedSkills',{});new_shared=n.get('sharedSkills',{})
                armed=old_shared.get('enhancementColor')
                token_key=lambda t:(t['skill'],t['owner'],t.get('color'),t['own'])
                delta=Counter(map(token_key,new_shared.get('tokens',[])))-Counter(map(token_key,old_shared.get('tokens',[])))
                bonus_valid=(original_fee==1 and armed==original_color and enhancement in b['skills']
                    and new_shared.get('enhancementColor') is None
                    and delta==Counter([(enhancement,b['hero'],armed,True)])
                    and not (Counter(map(token_key,old_shared.get('tokens',[])))-Counter(map(token_key,new_shared.get('tokens',[])))))
                check(bonus_valid,i,'shared conversion enhancement publishes exact owned color only after original paid fee')
                if bonus_valid:awards[armed]=1
            for crystal_fee in range(fee+1):
                if crystal_fee and b['crystals'].get(fee_color,0)<crystal_fee:continue
                crystal_colors=set(b['crystals'])|set(n['crystals'])|set(awards)
                raw_crystals={c:b['crystals'].get(c,0)-(crystal_fee if c==fee_color else 0)+awards[c] for c in crystal_colors}
                if any(n['crystals'].get(c,0)!=min(3,raw_crystals[c]) for c in crystal_colors):continue
                overflow={c:max(0,raw_crystals[c]-3) for c in crystal_colors}
                token_fee=fee-crystal_fee
                for pay_color in (fee_color,'Gold') if token_fee else (None,):
                    if pay_color and b['tokens'].get(pay_color,0)<token_fee:continue
                    valid|=all(n['tokens'].get(c,0)==b['tokens'].get(c,0)+gained.get(c,0)+overflow.get(c,0)-(token_fee if c==pay_color else 0) for c in set(b['tokens'])|set(n['tokens'])|set(gained)|set(overflow))
            check(valid,i,'printed conversion exact output colors and actual enhancement payment are conserved')
            if fee_die and not payment['extra']:
                check(not b['sourceUsedThisTurn'] and n['sourceUsedThisTurn'] and n['held']==fee_die and n['extraDice']==b['extraDice'],i,'conversion native ordinary fee die commits only its ordinary quota')
            else:
                check(n['held']==b['held'] and n['extraDice']==b['extraDice']-(1 if fee_die else 0),i,'conversion effect dice remain separate from actual fee and extra quota')
        if kind=='play' and a['Card']=='magic_000' and 'unit' in fields:
            slot=int(fields['unit']);units=b['units']
            check(b['phase']=='Outside' and 0<=slot<len(units),i,'energy spell selects an actual owned outside-battle unit slot')
            if 0<=slot<len(units) and slot<len(n['units']):
                target=units[slot];result=n['units'][slot]
                check(not target['ready'] and not target['destroyed'] and result['ready'] and result['id']==target['id'],i,'energy spell readies the same actual exhausted owned unit')
                check(result['wounds']==(0 if a['Choice']=='strong' else target['wounds']),i,'energy base preserves wounds and strong heals the selected unit')
                check(len(units)==len(n['units']) and all(u==n['units'][j] for j,u in enumerate(units) if j!=slot),i,'energy spell leaves all other owned units unchanged')
        if kind in ('skillBullseye','soulGuideBlock'):
            attack=kind=='skillBullseye';key='attacks' if attack else 'blocks';part=a['Parts']
            check(len(n[key])==len(b[key]) and part<len(b[key]),i,'exact native bonus selects an existing unconsumed contribution')
            bonus=(1 if b['phase']=='Ranged' else 2) if attack else 1
            for j,(old,new) in enumerate(zip(b[key],n[key])):
                check(new['value']==old['value']+(bonus if j==part else 0),i,'native skill boosts only the selected exact contribution by its printed phase value')
                check(all(new[k]==old[k] for k in old if k not in ('value','effectiveValue')),i,'exact skill bonus retains original contribution element type physical source and restrictions')
            if attack:
                check(a['Card'] in b['skills'] and a['Card'] not in b['usedSkillsTurn'] and a['Card'] in n['usedSkillsTurn'] and b[key][part]['card'] and not b[key][part]['unit'],i,'Bullseye uses actual owned once-per-turn skill and actual original deed source')
            else:
                check(b['phase']=='Block' and b['soulGuideBlockPending'] and not n['soulGuideBlockPending'] and b['usedSkillsTurn']==n['usedSkillsTurn'],i,'Soul Guide consumes actual pending block permission without activating the skill again')
            check(all(b[k]==n[k] for k in ('tokens','crystals','source','held','zones')),i,'exact native contribution bonus has no printed extra fee or card play')
        if kind=='skillFork':
            skill='hero_skill_pool_001:05'
            selected=[e for e in b['enemies'] if a['Targets']&(1<<e['index'])]
            added=n['attacks'][len(b['attacks']):]
            check(a['Card']==skill and skill in b['skills'] and skill not in b['usedSkillsTurn'] and skill in n['usedSkillsTurn'],i,'Forked Lightning uses actual native hero skill once per turn')
            check(b['phase'] in ('Ranged','Melee') and 1<=len(selected)<=3 and all(not e['defeated'] for e in selected),i,'Forked Lightning selects one to three different actual surviving enemies in an attack phase')
            check(len(added)==len(selected) and Counter(p['requiredEnemy'] for p in added)==Counter(e['instance'] for e in selected),i,'each Forked Lightning contribution is bound to one selected real enemy instance')
            check(all(p['value']==1 and p['element']=='ColdFire' and p['type']=='Ranged' and p['skill']==skill and not p['card'] and not p['unit'] for p in added),i,'printed Forked Lightning gives separate ColdFire ranged one without a ghost deed or unit')
            check(all(n[k]==b[k] for k in ('tokens','crystals','source','held','zones')),i,'native Forked Lightning has no printed mana fee or physical card movement')
        if kind=='unitLeadership':
            skill='hero_skill_pool_002:06'
            check(skill in b['skills'] and skill not in b['usedSkillsTurn'] and skill in n['usedSkillsTurn'],i,'native leadership binds owned unused skill and consumes exactly one turn activation')
            check(b['inBattle'] and b['phase'] in ('Ranged','Block','Melee'),i,'native leadership is activated in a formal combat phase')
        if kind=='skillSideways':
            physical=b['handOrder'][a['Parts']];host=next(c for c in b['zones']['hand'] if c['instance']==physical)
            check(a['Card'] in b['skills'] and a['Card'] not in b['usedSkillsTurn'],i,'native sideways skill is owned and unused this turn')
            check(not host['wound'] and host in n['zones']['played'] and host not in n['zones']['hand'],i,'skill sideways consumes exactly the selected actual nonwound hand slot')
            check(a['Card'] in n['usedSkillsTurn'] and n['activity']==b['activity']+1,i,'one actual sideways play consumes the once-per-turn native skill')
            check(n['source']==b['source'] and n['held']==b['held'],i,'native sideways skill does not claim a source die')
            colors=['Red','Blue','Green','White','Gold','Black'];paid=colors[a['Option']] if a['Card']=='hero_skill_pool_005:07' else None
            if a['Card']=='hero_skill_pool_007:06':value=2 if host['id'].startswith('basic_card_') else 3
            elif a['Card']=='hero_skill_pool_007:07':
                value=3 if a['Option']==1 else 2
                check(not a['Option'] or not b['sourceUsedThisTurn'] and b['held'] is None,i,'Magic Repel source promise respects whole-turn real source history')
                check(n['forgoSource']==(b['forgoSource'] or a['Option']==1),i,'Magic Repel promise remains active for the rest of this turn')
            else:
                value=4 if (_action_colors.get(host['id']) or _spell_colors.get(host['id']))==paid else 3
                stock=lambda state:sum(state['tokens'].values())+sum(state['crystals'].values())
                fee=0 if paid in b.get('artifactFlags',{}).get('infinite',[]) else 1
                check(stock(b)-stock(n)==fee,i,'Universal Power pays a real selected-color mana, respecting actual Infinite Mana')
                delta={c:b['tokens'].get(c,0)+b['crystals'].get(c,0)-n['tokens'].get(c,0)-n['crystals'].get(c,0) for c in set(b['tokens'])|set(n['tokens'])|set(b['crystals'])|set(n['crystals'])}
                check(all(v==0 for c,v in delta.items() if c not in (paid,'Gold')) and sum(delta.get(c,0) for c in {paid,'Gold'})==fee and all(v>=0 for v in delta.values()),i,'Universal Power consumes the selected actual color or real gold wildcard, never another color')
            if a['Choice']=='Influence':check(n['influence']-b['influence']==value,i,'printed native sideways influence value')
            if a['Choice']=='Block':
                added=[c for c in n['blocks'] if c['cardInstance']==physical]
                check(len(added)==1 and added[0]['value']==value and added[0]['element']=='Physical',i,'printed native sideways physical block binds the actual deed')
            if a['Choice']=='Attack':
                added=[c for c in n['attacks'] if c['cardInstance']==physical]
                check(len(added)==1 and added[0]['value']==value and added[0]['element']=='Physical',i,'printed native sideways physical attack binds the actual deed')
        if kind=='skill':
            check(a['Card'] in b['skills'],i,'activated skill belongs to actual hero player')
            if a['Option']!=100:
                check(a['Card'] not in b['usedSkillsTurn'] and a['Card'] not in b['usedSkillsRound'] and a['Card'] not in b['flippedSkills'],i,'owned skill activation respects turn/round/flip markers')
        if 'level' in b and kind!='nextTurn':check(n['level']==b['level'],i,'earned Fame must not upgrade level before formal end-turn rewards')
        if kind=='source':
            diff=Counter(b['source'])-Counter(n['source']);color=n['held']
            check(sum(diff.values())==1 and diff[color]==1,i,'ordinary source consumes exactly the held real face')
            check(b['held'] is None,i,'ordinary source quota available before claim')
            delta={c:n['tokens'].get(c,0)-b['tokens'].get(c,0) for c in set(b['tokens'])|set(n['tokens'])}
            check(delta.get(color)==1 and sum(abs(v) for v in delta.values())==1,i,'one matching token; gold stays gold')
            check(n['crystals']==b['crystals'],i,'source does not create or consume crystals')
        if kind in ('play','sideways'):
            used=[c for c in b['zones']['hand'] if c['id']==a['Card']];after_hand={c['instance'] for c in n['zones']['hand']}
            check(any(c['instance'] not in after_hand for c in used),i,'played original card leaves actual hand')
        if kind=='boxReveal':
            host=next(c for c in b['zones']['hand'] if c['id']=='items_024')
            check(n['mysticBox']['physical']==host['instance'] and not n['mysticBox']['used'] and host in n['zones']['played'],i,'Box reveals actual supply while moving only the physical box into play')
            check(n['artifactSupply']==b['artifactSupply']-1 and n['acquired']==b['acquired'],i,'revealed artifact is reserved from actual supply, never acquired')
        if kind in ('artifact','boxUse'):
            borrowed=kind=='boxUse'
            pending=b.get('pendingHeadband') if a['Card']=='items_022' else None
            host=next(c for c in b['zones']['played'] if c['instance']==b['mysticBox']['physical']) if borrowed else next(c for c in b['zones']['played'] if c['instance']==pending['physical']) if pending else next(c for c in b['zones']['hand'] if c['id']==a['Card'])
            if borrowed:
                check(not b['mysticBox']['used'] and b['mysticBox']['revealed']==a['Card'] and n['mysticBox']['used'] and n['mysticBox']['physical']==host['instance'],i,'Box borrows the actual revealed effect exactly once')
                check(n['artifactSupply']==b['artifactSupply'],i,'borrowed original stays reserved until formal end turn')
            destination='removed' if a['Choice']=='burn' else 'banners' if any(u.get('banner')==host['id'] for u in n.get('units',[])) else 'played'
            check(any(c['instance']==host['instance'] for c in n['zones'][destination]),i,'original artifact physical instance reaches printed normal/attached/burn destination')
            if a['Card']=='items_022':
                skill=fields['skill']
                check(skill in b['publicSkills'],i,'Headband selects a real prior public skill offer')
                if a['Choice']=='burn':
                    check(Counter(b['publicSkills'])-Counter(n['publicSkills'])==Counter([skill]) and Counter(n['skills'])-Counter(b['skills'])==Counter([skill]),i,'burned Headband acquires exactly the selected actual public skill')
                else:
                    check(b['publicSkills']==n['publicSkills'] and b['skills']==n['skills'],i,'normal Headband borrows without acquiring or removing the public skill')
                    check(all(b[k]==n[k] for k in ('usedSkillsTurn','usedSkillsRound','flippedSkills')),i,'public Headband borrow leaves personal frequency and flip flags unchanged')
                    following=n.get('pendingHeadband')
                    if pending:
                        check(pending['skill']==skill and pending['physical']==host['instance'],i,'remaining Headband use stays bound to the exact prior physical card and public skill')
                        if following:check(following['remaining']==pending['remaining']-1 and following['completed']==pending['completed']+1,i,'typed Headband consumes exactly one pending use')
                        else:check(pending['remaining']==1,i,'typed Headband only clears after its final pending use')
                    if skill=='hero_skill_pool_003:01':
                        h=n['hardened'];target=b['enemies'][a['Targets']]['instance']
                        check(h['enemy']==target and h['attack']==a['Parts'] and h['physical' if a['Option']==0 else 'nonPhysical']==(2 if a['Option']==0 else 1),i,'borrowed Battle Hardened binds exact original attack and printed damage reduction without doubling the same permission')
            if a['Card'] in ('items_013','items_021','items_023'):
                material=None
                if 'materialSlot' in fields:
                    physical=[x for x in b['handOrder'] if x!=host['instance']][int(fields['materialSlot'])]
                    material=next(c for c in b['zones']['hand'] if c['instance']==physical)
                    zone='removed' if a['Card']=='items_013' else 'discard'
                    if a['Card']=='items_023' and fields.get('spellId')=='magic_002':
                        # Tome pays into discard before Meditation selects from it.
                        # That exact material can therefore be among the returned cards.
                        eligible={c['instance']:c for c in b['zones']['discard']+[material]}
                        remaining={c['instance']:c for c in n['zones']['discard']}
                        returned={key:card for key,card in eligible.items() if key not in remaining}
                        prior_draw={c['instance']:c for c in b['zones']['draw']}
                        actual_draw={c['instance']:c for c in n['zones']['draw']}
                        check(all(eligible.get(key)==card for key,card in remaining.items()) and len(returned)==min(2,len(eligible)),i,'Tome Meditation returns exactly two actual post-payment discard cards or all if fewer')
                        check(actual_draw==prior_draw|returned and material not in n['zones']['hand'],i,'Tome Meditation preserves prior draw cards and moves only exact post-payment discard instances')
                        if all('auditDrawOrder' in state and 'auditNextDrawBonus' in state for state in (b,n)):
                            count=len(returned);order=n['auditDrawOrder'];prior_order=b['auditDrawOrder']
                            top=fields.get('spellChoice')=='top'
                            inserted=order[:count] if top else order[len(prior_order):]
                            unchanged=order[count:] if top else order[:len(prior_order)]
                            check(set(inserted)==set(returned) and len(inserted)==count and unchanged==prior_order,i,'Tome Meditation returns selected cards at chosen end and preserves exact prior draw order')
                            check(n['auditNextDrawBonus']==b['auditNextDrawBonus']+2,i,'Tome Meditation grants exactly the original next draw bonus two')
                        else:
                            gaps.append({'step':i,'gap':'Tome Meditation draw order and next-draw bonus absent from legacy audit state'})
                    else:
                        check(material in n['zones'][zone],i,'original material artifact consumes the selected exact physical hand slot')
                if a['Card']=='items_013':
                    burn=a['Choice']=='burn';gained=fields['spells' if burn else 'advanced'];color=_action_colors.get(material['id'])
                    check(not material['wound'] and color is not None and color==(_spell_colors if burn else _action_colors).get(gained),i,'printed Wisdom Book removes a colored original action for a same-color original supply card')
                    check(gained in b['spellOffer' if burn else 'advancedOffer'] and Counter(n['acquired'])-Counter(b['acquired'])==Counter([gained]),i,'Wisdom Book acquisition is exactly the selected prior actual supply card')
                    check(Counter(n['hand'])-Counter(b['hand'])==Counter([gained]),i,'Wisdom Book puts the actually acquired card in hand')
                    if burn:
                        prior=b['crystals'].get(color,0)
                        check(n['crystals'].get(color,0)==min(3,prior+1) and n['tokens'].get(color,0)-b['tokens'].get(color,0)==int(prior==3),i,'burned Wisdom Book adds one matching crystal with original cap overflow')
                elif a['Card']=='items_021':
                    first=int(fields['first']);second=int(fields['second']);burn=a['Choice']=='burn';choices=[first,second] if burn else [first]
                    if not burn:check((_action_colors|_spell_colors).get(material['id'])=={1:'Blue',2:'Red',3:'Green'}.get(first),i,'normal Staff effect matches original discarded card color')
                    else:check(first!=second,i,'burned Staff uses two different printed effects')
                    if 1 in choices:
                        color=('Red','Blue','Green','White')[int(fields['color'])];prior=b['crystals'].get(color,0)
                        check(n['crystals'].get(color,0)==min(3,prior+2) and n['tokens'].get(color,0)-b['tokens'].get(color,0)==max(0,prior+2-3),i,'Staff blue grants two selected basic-color crystals with original cap overflow')
                    if 2 in choices:
                        old=b['units'][a['Targets']];new=n['units'][a['Targets']]
                        check(old['level']<=3 and new['ready'] and new['wounds']==old['wounds'],i,'Staff red readies the selected actual level-three-or-lower unit without healing it')
                    if 3 in choices:check(n['healing']==b['healing']+3 and b['phase']=='Outside',i,'Staff green grants three healing only outside combat')
                else:
                    spell=fields['spellId'];color=(_action_colors|_spell_colors).get(material['id'])
                    check(color is not None and color==_spell_colors.get(spell) and spell in b['spellOffer'],i,'Tome pays an actual same-color hand card for the original actual supply spell')
                    check(n['spellOffer']==b['spellOffer'] and n['acquired']==b['acquired'],i,'Tome borrows an effect without claiming its supply card')
                    if spell=='magic_013':
                        target=a['Targets'];check(n['enemies'][target]['skipped'],i,'original borrowed Freeze stops the selected actual enemy')
                        for j,enemy in enumerate(b['enemies']):
                            if j!=target:check(enemy['skipped']==n['enemies'][j]['skipped'],i,'borrowed Freeze does not stop another enemy')
                    if spell in ('magic_009','magic_015'):
                        check(n['tokens']==b['tokens'] and n['crystals']==b['crystals'],i,'printed Tome waives ordinary basic and strong spell mana fees')
                        previous={json.dumps(p,sort_keys=True) for p in b['attacks']}
                        for part in n['attacks']:
                            if json.dumps(part,sort_keys=True) not in previous:check(part['cardInstance']==host['instance'],i,'borrowed fireball/blizzard contribution retains the physical artifact source')
            rings={'items_004':'Red','items_005':'Blue','items_006':'White','items_007':'Green'}
            if a['Card'] in rings:
                color=rings[a['Card']]
                if a['Choice']=='normal':
                    prior=b['crystals'].get(color,0)
                    check(n['fame']==b['fame']+1+int(borrowed) and n['crystals'].get(color,0)==min(3,prior+1) and n['tokens'].get(color,0)-b['tokens'].get(color,0)==1+int(prior==3),i,'printed original ring grants matching token/crystal with cap overflow and Fame 1')
                else:
                    check(set(n['artifactFlags']['infinite'])==set(b['artifactFlags']['infinite'])|{color,'Black'} and n['artifactFlags']['famePerSpell'].get(color,0)==b['artifactFlags']['famePerSpell'].get(color,0)+1,i,'printed burned ring authorizes only matching/black infinite mana and matching spell Fame')
            if a['Card']=='items_020' and 'blocks' in n:
                added=n['blocks'][len(b['blocks']):];burn=a['Choice']=='burn';split=a['Option']==1
                check(len(added)==(3 if burn else 2) if split else len(added)==1,i,'original shield split count is two normal or three burned; single is one')
                check(all(p['value']==(4 if split else 8 if burn else 6) and p['element']==('ColdFire' if burn else 'Physical') for p in added),i,'original shield printed block magnitude and element')
                if split:check(bool(added) and len({p['distinctGroup'] for p in added})==1 and added[0]['distinctGroup']>0,i,'split shield binds distinct enemy attacks in one formal group')
        if kind=='bowChoice':
            target=a['Targets'];check(b['artifactFlags']['starBow'] and b['phase'] in ('Ranged','Melee'),i,'bow choice requires real burned permission and attack phase')
            check(len(b['attacks'])==len(n['attacks']) and n['attacks'][target]['starBowChoice']==a['Option'],i,'bow changes the selected actual contribution mode')
            for j,(old,new) in enumerate(zip(b['attacks'],n['attacks'])):
                check(all(old[k]==new[k] for k in old if k not in ('starBowChoice','effectiveValue','effectiveSiege')),i,'bow retains physical source, printed value, element and target restriction')
                if j!=target:check(old==new,i,'bow does not alter another contribution')
            check(b['zones']==n['zones'] and b['tokens']==n['tokens'] and b['crystals']==n['crystals'],i,'bow choice does not spend another card or mana')
        if kind=='killCrystal':
            enemy=b['enemies'][a['Targets']];color=('Red','Blue','Green','White')[a['Option']]
            permitted={'White'}|({'Red'} if 'FireResist' in enemy['abilities'] else set())|({'Blue'} if 'IceResist' in enemy['abilities'] else set())|({'Green'} if 'PhysicalResist' in enemy['abilities'] else set())
            check(b['phase']=='Melee' and not enemy['defeated'] and color in permitted,i,'original kill crystal is chosen for a living enemy and its printed resistance colors')
            check(n['killCrystalChoices'].get(str(a['Targets']))==color and b['crystals']==n['crystals'],i,'kill reward selection records color without awarding crystals before defeat')
        if kind=='play' and a['Card']=='basic_card_021' and 'attacks' in b:
            # Original face: attack OR block 2 / attack 4 for one red.
            strong=a['Choice']=='strong' or b['nextEnhanced'];expected=(4 if strong else 2)+b['boost']
            # Original Tovak Mana Overlord adds exactly four to the first
            # matching-color enhanced card output, returning its central token.
            # A free enhancement/infinite fee cannot consume that payment token.
            old_tokens=b.get('sharedSkills',{}).get('tokens',[])
            new_tokens=n.get('sharedSkills',{}).get('tokens',[])
            eligible=[token for token in old_tokens
                      if token['skill']=='hero_skill_pool_007:09' and token.get('color')=='Red']
            returned=[token for token in eligible if token not in new_tokens]
            overlord_gain=0
            if strong and not b['nextEnhanced'] and 'Red' not in b['artifactFlags']['infinite'] and returned:
                check(len(returned)==1 and returned[0]==eligible[0],i,'Rage overload consumes only the first exact matching Red central token')
                check(new_tokens==[token for token in old_tokens if token not in returned],i,'Rage overload returns only its exact matching central token')
                if len(returned)==1 and returned[0]==eligible[0]:overlord_gain=4
            value=lambda state:sum(p['value'] for p in state['attacks'] if p['card']=='basic_card_021')
            check(n['attacks'][:len(b['attacks'])]==b['attacks'],i,'Rage preserves previously existing physical attack contributions')
            if a['Choice']=='block' and not strong:
                check(value(n)==value(b),i,'printed Rage basic block does not create attack')
                if 'block' in b:check(n['block']-b['block']==expected+ambush_gain_block,i,'printed Rage basic block 2 plus explicit focus and first Ambush bonus')
            else:check(value(n)-value(b)==expected+ambush_gain_attack+overlord_gain,i,'printed Rage attack 2/4 plus exact focus Ambush and consumed Overlord bonus')
            if a['Choice']=='strong' and not b['nextEnhanced']:
                spent={c:b['tokens'].get(c,0)+b['crystals'].get(c,0)-n['tokens'].get(c,0)-n['crystals'].get(c,0) for c in set(b['tokens'])|set(b['crystals'])|set(n['tokens'])|set(n['crystals'])}
                payment=b.get('paymentSelection')
                stock_unchanged=all(b[zone].get(color,0)==n[zone].get(color,0) for zone in ('tokens','crystals') for color in set(b[zone])|set(n[zone]))
                if payment:
                    check(payment['required']=='Red',i,'original 021 native payment must replace its printed Red fee')
                    check(n.get('paymentSelection') is None and payment['skill'] not in b['usedSkillsTurn'] and payment['skill'] in n['usedSkillsTurn'],i,'original 021 fee commits the actual selected native skill once')
                    if payment['skill']=='hero_skill_pool_000:06':
                        material=next((c for c in b['zones']['hand'] if c['instance']==payment['material']),None)
                        check(material is not None and material['wound'] and material in n['zones']['discard'] and material not in n['zones']['hand'],i,'original 021 Invocation fee discards the exact physical wound for immediate Red')
                        check(stock_unchanged and b['source']==n['source'] and b['held']==n['held'],i,'original 021 Invocation pays its issued Red without another stock fee or leftover mana')
                    elif payment['skill']=='hero_skill_pool_000:07':
                        source=payment['source']
                        check(source=='Green' or source=='Black' and b['dayPart']=='Day',i,'original 021 Polarization uses opposite Green or daytime Black for Red')
                        if payment['die']>=0:
                            order=b.get('auditSourceOrder',[]);slot=payment['die']
                            check(0<=slot<len(order) and order[slot]==source,i,'original 021 Polarization binds the actual fee die index and face')
                            check(Counter(b['source'])-Counter(n['source'])==Counter([source]) and not Counter(n['source'])-Counter(b['source']) and stock_unchanged,i,'original 021 native die pays exactly one fee with no stock deduction')
                            if payment['extra']:
                                check(n['held']==b['held'] and b['extraDice']-n['extraDice']+b['extraBlackDice']-n['extraBlackDice']==1 and n['extraDice']<=b['extraDice'] and n['extraBlackDice']<=b['extraBlackDice'],i,'original 021 extra fee die consumes its earned allowance and preserves ordinary held die')
                            else:
                                check(n['held']==source and n['sourceUsedThisTurn'] and n['extraDice']==b['extraDice'] and n['extraBlackDice']==b['extraBlackDice'],i,'original 021 ordinary native fee records held die without consuming extra allowances')
                        else:
                            deltas=[(color,b[zone].get(color,0)-n[zone].get(color,0)) for zone in ('tokens','crystals') for color in set(b[zone])|set(n[zone])]
                            # Source is the substituted COST color, not a promise
                            # to spend a crystal of that color. Daytime Gold
                            # tokens can pay a basic-color cost (never Black).
                            allowed={source}
                            if b['dayPart']=='Day' and source in ('Red','Blue','Green','White'):
                                allowed.add('Gold')
                            check(sum(value for _,value in deltas)==1 and all(value>=0 and (color in allowed or value==0) for color,value in deltas) and b['source']==n['source'] and b['held']==n['held'],i,'original 021 Polarization stock pays exactly one selected opposite-color fee')
                    else:check(False,i,'original 021 rejects unrecognized native fee authority')
                elif 'Red' in b.get('artifactFlags',{}).get('infinite',[]):check(all(v==0 for v in spent.values()),i,'printed burned red ring authorizes Rage red fee without spending finite mana')
                elif b.get('sharedSkills',{}).get('enhancementColor')=='Red':
                    enhancement='hero_skill_pool_003:09'
                    token_key=lambda t:(t['skill'],t['owner'],t.get('color'),t['own'])
                    old=Counter(map(token_key,b['sharedSkills']['tokens']))
                    new=Counter(map(token_key,n['sharedSkills']['tokens']))
                    check(enhancement in b['skills'] and enhancement not in b['usedSkillsRound']
                        and enhancement in n['usedSkillsRound'] and n['sharedSkills'].get('enhancementColor') is None
                        and new-old==Counter([(enhancement,b['hero'],'Red',True)]) and not old-new,
                        i,'Rage paid enhancement publishes exact owned Red token once')
                    valid=False
                    for zone,color in [('tokens','Red'),('crystals','Red')]+([('tokens','Gold')] if b['dayPart']=='Day' else []):
                        if b[zone].get(color,0)<1:continue
                        expected_stock={z:dict(b[z]) for z in ('tokens','crystals')}
                        expected_stock[zone][color]-=1
                        red=expected_stock['crystals'].get('Red',0)+1
                        expected_stock['crystals']['Red']=min(3,red)
                        expected_stock['tokens']['Red']=expected_stock['tokens'].get('Red',0)+max(0,red-3)
                        valid |= all(expected_stock[z].get(c,0)==n[z].get(c,0) for z in expected_stock for c in set(expected_stock[z])|set(n[z]))
                    check(valid and b['source']==n['source'] and b['held']==n['held'],i,'Rage exact one fee and enhancement crystal conserved jointly')
                else:check(sum(spent.values())==1 and spent.get('Red',0)+spent.get('Gold',0)==1,i,'printed red Rage fee exactly one red or legal wild gold')
        if kind=='attack' and 'attacks' in b:
            if 'kills' in b:
                check(n['kills']>b['kills'],i,'rulebook p9.6: insufficient attack must allow adding cards or changing target, not commit and consume contributions')
            if 'enemies' in b:
                targets=[e for e in b['enemies'] if a['Targets']&(1<<e['index'])]
                check(bool(targets) and all(not e['defeated'] for e in targets),i,'attack targets actual surviving enemy instances')
            selected=[p for j,p in enumerate(b['attacks']) if a['Parts']&(1<<j)]
            # New runtime metadata exposes effective armor/element; older traces
            # retain their existing scope instead of guessing missing modifiers.
            targets=[e for e in b.get('enemies',[]) if a['Targets']&(1<<e['index'])]
            if targets and all(e.get('effectiveArmor') is not None for e in targets) and all('effectiveElement' in p for p in selected):
                flags=b.get('attackResistanceFlags',{});free=0;bound=Counter()
                for part in selected:
                    element=part['effectiveElement'];value=part['effectiveValue']
                    resisted=not flags.get('ignoreAll',False) and not (flags.get('ignorePhysical',False) and element=='Physical') and any(element in e['effectiveResistances'] for e in targets)
                    points=value*(1 if resisted else 2)
                    if part.get('requiredEnemy',-1)<0:free+=points
                    else:bound[part['requiredEnemy']]+=points
                usable=free+sum(min(points,2*next(e['effectiveArmor'] for e in targets if e['instance']==entity)) for entity,points in bound.items() if any(e['instance']==entity for e in targets))
                # Provisional defender assignments are validated separately; their
                # target-dependent automatic choices are not inferred here.
                if not b.get('defenderSelected') and not any(e.get('defend',0)>0 and not e['defeated'] for e in b['enemies']):
                    check(usable//2>=sum(e['effectiveArmor'] for e in targets),i,'selected attack totals resistance halves once and meets effective armor')
                else:gaps.append({'step':i,'gap':'effective attack threshold requires provisional defender assignments'})
            target_instances={e['instance'] for e in b.get('enemies',[]) if a['Targets']&(1<<e['index'])}
            check(all(p.get('requiredEnemy',-1)<0 or p['requiredEnemy'] in target_instances for p in selected),i,'selected target-bound contribution cannot be transferred to a different actual enemy instance')
            fingerprint=lambda p:(p['value'],p['element'],p['type'],p['card'],p.get('cardInstance'),p.get('skill'),p.get('requiredEnemy'))
            remaining=Counter(map(fingerprint,b['attacks']))-Counter(map(fingerprint,selected))
            check(Counter(map(fingerprint,n['attacks']))==remaining,i,'only selected actual attack contributions consumed')
        if kind in ('block','blockSelected') and 'enemies' in b:
            target=b['enemies'][a['Targets']];slot=target['attacks'][a['Option']]
            check(not target['defeated'] and not slot['blocked'],i,'block selects a surviving enemy and its unblocked actual attack slot')
            for old,new in zip(b['enemies'],n['enemies']):
                check(old['instance']==new['instance'],i,'block preserves actual enemy identity')
                for previous,current in zip(old['attacks'],new['attacks']):
                    if old['index']!=a['Targets'] or previous['index']!=a['Option']:
                        check(previous['blocked']==current['blocked'],i,'block does not mark another enemy attack blocked')
            if kind=='blockSelected':
                selected=[p for j,p in enumerate(b['blocks']) if a['Parts']&(1<<j)]
                fingerprint=lambda p:(p['value'],p['element'],p['distinctGroup'],p.get('skill'),p.get('card'),p.get('cardInstance'))
                check(a['Parts']>0 and a['Parts']<(1<<len(b['blocks'])),i,'selected block binds nonempty actual contribution indices')
                remaining=Counter(map(fingerprint,b['blocks']))-Counter(map(fingerprint,selected))
                check(Counter(map(fingerprint,n['blocks']))==remaining,i,'only selected actual block sources consumed; others retained')
        if kind=='defend':
            source=b['enemies'][a['Parts']];target=b['enemies'][a['Option']]
            check(not source['defeated'] and source['defend']>0 and 'Defend' in source['abilities'] and 'Defend' not in source['ignoredAbilities'],i,'defender is a living original non-ignored Defend source')
            check(not target['defeated'] and bool(a['Targets']&(1<<a['Option'])),i,'defender protects an actual attacked surviving target')
            expected={'source':source['instance'],'target':target['instance'],'value':source['defend']}
            check(expected in n['defenderSelected'],i,'provisional defense retains original source target and printed value')
            check(b['defenderApplied']==n['defenderApplied'],i,'provisional defense does not commit before actual attack')
        if kind=='clearDefend':
            check(not n['defenderSelected'] and b['defenderApplied']==n['defenderApplied'],i,'clear only provisional defense choices')
        if kind=='nextTurn' and b.get('mysticBox') and not n['stopReason'].startswith('formal_'):
            check(n.get('mysticBox') is None and n['artifactSupply']==b['artifactSupply']+1,i,'formal end turn clears Box and returns reserved original to supply')
        if kind=='nextTurn' and 'enemies' in b and not n['stopReason'].startswith('formal_'):
            survivors={e['instance'] for e in b['enemies'] if not e['defeated']}
            if survivors:
                if t.get('task_boundary')=='formal_single_battle_v1':
                    history=lambda state:[(e['instance'],e['defeated']) for e in state['enemies']]
                    check(history(b)==history(n) and b['phase']==n['phase']=='End'
                          and n['turns']==b['turns'] and n['rounds']==b['rounds']
                          and n['completedBattles']==b['completedBattles']+1
                          and n['completedTurns']==b['completedTurns']+1
                          and not n['inBattle'] and n['stopReason']=='single_battle_lost'
                          and not n['taskProgress']['sequenceCompleted'],i,
                          'single battle loss formally completes exactly once and retains enemy identities')
                elif n.get('roundWindow') and n['phase']=='End' and n['turns']==b['turns']:
                    history=lambda state:[(e['instance'],e['defeated']) for e in state['enemies']]
                    check(history(b)==history(n) and n['rounds']==b['rounds'] and n['dayPart']==b['dayPart'],i,'real banner window retains final battle history before round cleanup')
                elif n['phase']=='End' and n['turns']==b['turns']:
                    # EndRound/turn-limit exits before BeginTurn. The formal
                    # Journey keeps the final battle history, including dead
                    # enemies; this is not a newly instantiated next encounter.
                    history=lambda state:[(e['instance'],e['defeated']) for e in state['enemies']]
                    check(history(b)==history(n) and (n['rounds']==2 or n['stopReason']=='turn_limit'),i,'terminal journey retains exact final battle identities and defeated flags')
                else:check(survivors=={e['instance'] for e in n['enemies']},i,'surviving enemies retain physical identity for next battle')
        if kind=='rest':
            old={c['instance']:c for c in b['zones']['hand']};new={c['instance'] for c in n['zones']['hand']};removed=[old[x] for x in old.keys()-new]
            ordinary=[c for c in removed if not c['wound']];wounds=[c for c in removed if c['wound']]
            check(not b['inBattle'] and not b['rested'],i,'rest outside battle once per turn')
            check(len(ordinary)==1 if a['Card'] else len(ordinary)==0 and len(wounds)==1,i,'standard rest one ordinary; slow recovery exactly one wound')
            discard={c['instance'] for c in n['zones']['discard']}
            check(all(c['instance'] in discard for c in removed),i,'rest discards cards rather than healing/deleting them')
        if kind=='roundReturn':
            check(b['roundWindow'] and n['roundWindow'] and b['zones']==n['zones'] and b['turns']==n['turns'] and b['rounds']==n['rounds'],i,'banner return reservation occurs only in actual window without moving cards or advancing time')
            check(n['returningBanners'][a['Targets']]==bool(a['Option']) and b['units'][a['Targets']]['banner'] is not None,i,'round reservation belongs to actual attached banner')
        if kind=='roundFinish':
            check(b['roundWindow'] and not n['roundWindow'] and b['completedTurns']==n['completedTurns'],i,'formal banner round cleanup does not invent another completed turn')
            if b['rounds']==1:check(n['rounds']==2 and n['turns']==b['turns']+1 and n['dayPart']=='Night',i,'banner round cleanup begins actual next night turn exactly once')
        if kind=='nextTurn' and not n['stopReason'].startswith('formal_'):
            check(n['completedTurns']==b['completedTurns']+1,i,'exactly one actual turn completed')
            check(n['held'] is None,i,'real end turn returns ordinary die')
            # nextTurn includes the subsequent formal StartTurn, which issues
            # Mana Claim's persistent income after clearing the old tokens.
            income=dict(n.get('tokensPerTurn',{})) if n['turns']>b['turns'] else {}
            # Original place_006: starting a turn on a Magical Glade adds
            # exactly one Gold (day) or Black (night), after old mana expires.
            # Entering the site alone is not an income event.
            world=n.get('world',{})
            if n['turns']>b['turns'] and world.get('enabled'):
                cells=[cell for cell in world['cells']
                       if (cell['q'],cell['r'])==(world['q'],world['r'])]
                check(len(cells)==1,i,'actual world turn income has exactly one current map cell')
                if len(cells)==1 and cells[0].get('site')=='MagicalGlade':
                    color='Gold' if n['dayPart']=='Day' else 'Black'
                    income[color]=income.get(color,0)+1
            check(all(n['tokens'].get(color,0)==income.get(color,0) for color in n['tokens'].keys()|income.keys()),i,'real turn boundary clears old mana then issues exact persistent income')
        if n.get('rounds',1)>b.get('rounds',1):check(b['dayPart']=='Day' and n['dayPart']=='Night',i,'real round transition changes day to night')
        if n['stopReason'].startswith('formal_') or n['stopReason']=='no_legal_continuation':gaps.append({'step':i,'gap':n['stopReason']})
    if trace and not any(t['action']['Kind']=='fight' for t in trace):gaps.append({'gap':'policy never attempted combat; unsuitable as combat tester'})
    return {'checks':checks,'errors':errors,'adapter_gaps':gaps,'status':'failed' if errors else 'checks_passed_with_gaps' if gaps else 'checks_passed','scope':'physical identity, ordinary source, rest, turn/round lifecycle; not all card/armor rules'}
