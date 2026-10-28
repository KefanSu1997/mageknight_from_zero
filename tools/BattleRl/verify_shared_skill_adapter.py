"""Actual original owner/central operations, isolated from PPO and coverage."""
import copy
import hashlib
import json
import os
from pathlib import Path
import sys
os.environ.update(BATTLE_RL_SHARED_SKILLS='1', BATTLE_RL_NATIVE_PAYMENTS='1',
                  BATTLE_RL_NATIVE_CONVERSIONS='1', BATTLE_RL_NATIVE_ENERGY_TARGETS='1',
                  BATTLE_RL_PROTOCOL='--serve-journey-artifacts', BATTLE_RL_ORIGINAL_OPENINGS='1')
from train_ppo import Rules

out=Path(sys.argv[1])
if out.exists():raise ValueError('Preserve prior real evidence')
out.mkdir(parents=True)
pool=json.loads(Path('AutomationOutputs/BattleRl/20261008_global_mixed_pool_224.json').read_text())
rows=pool['tasks']+pool['unknown_tasks']
env=Rules();traces=[];checks=0
def step(index):
    global current,checks
    before=current['state'];action=env.actions[index];current=env.step(index)
    traces[-1].append(dict(before=before,action=action,after=current['state'],reward=current['reward']))
    checks+=1
def find(kind,card='',parts=None):
    return next(i for i in current['legal'] if env.actions[i]['Kind']==kind
                and (not card or env.actions[i]['Card']==card)
                and (parts is None or env.actions[i]['Parts']==parts))
def wound_slots():
    wounds={c['instance'] for c in current['state']['zones']['hand'] if c['wound']}
    return [i for i,physical in enumerate(current['state']['handOrder'][:8]) if physical in wounds]
try:
    assert env.info['featureSchema'].endswith('_shared_seven_owner_inputs_v2')
    for skill in ('hero_skill_pool_000:09','hero_skill_pool_002:09','hero_skill_pool_001:09'):
        definition=copy.deepcopy(next(r['task']['definition'] for r in rows
                                     if skill in r['task']['definition'].get('Build',{}).get('SkillIds',[])))
        if skill.endswith('000:09'):definition['Build']['InitialWounds']=16
        current=env.reset_task(definition);traces.append([])
        if skill.endswith('001:09'):
            step(find('fight'))
        if skill.endswith('000:09'):
            woundslots=wound_slots()
            for _ in range(8):
                if len(woundslots)>=2:break
                step(find('rest'));step(find('nextTurn'))
                woundslots=wound_slots()
            # Owner can legally publish without wounds; require actual physical
            # wound removal when this independently reset fixture provides one.
            assert len(woundslots)>=2, 'Fixture must contain two actual hand wounds'
            mask=1<<woundslots[0];physical=current['state']['handOrder'][woundslots[0]]
            step(find('sharedUse',skill,mask))
            assert all(c['instance']!=physical for c in current['state']['zones']['hand'])
        else:step(find('sharedUse',skill))
        assert skill in current['state']['usedSkillsRound']
        assert any(t['skill']==skill and t['own'] for t in current['state']['sharedSkills']['tokens'])
        assert not any(env.actions[i]['Kind']=='sharedUse' and env.actions[i]['Card']==skill for i in current['legal'])
        if skill.endswith('002:09'):
            before=current['state']['sharedSkills']['terrain']
            assert all(t['reduction']==2 for t in before)
            step(find('sharedClaim',skill))
            assert all(t['reduction']==3 for t in current['state']['sharedSkills']['terrain'])
            assert not current['state']['sharedSkills']['tokens']
        elif skill.endswith('000:09'):
            slot=wound_slots()[0];physical=current['state']['handOrder'][slot]
            claim=next(i for i in current['legal'] if env.actions[i]['Kind']=='sharedClaim'
                       and env.actions[i]['Card']==skill and env.actions[i]['Parts']==slot
                       and env.actions[i]['Choice']=='Move')
            step(claim)
            assert current['state']['movement']==3 and not current['state']['sharedSkills']['tokens']
            assert any(c['instance']==physical for c in current['state']['zones']['played'])
        else:
            step(find('advance'))
            move=next(i for i in current['legal'] if env.actions[i]['Kind']=='play'
                      and env.actions[i]['Card']=='basic_card_007' and env.actions[i]['Choice']=='base')
            step(move);assert current['state']['movement']==2
            before=current['state']['enemies'][0]['attacks'][0]['effectiveAttack']
            step(find('cumbersome'));assert current['state']['movement']==1
            assert current['state']['enemies'][0]['attacks'][0]['effectiveAttack']==before-1
            step(find('cumbersome'));assert current['state']['movement']==0
            assert current['state']['enemies'][0]['attacks'][0]['effectiveAttack']==0
            assert current['state']['enemies'][0]['attacks'][0]['blocked']
        # Every real step runs the adapter's full preview/restore invariant.
    originals=json.loads(Path('resources/text_json/basic_card.json').read_text())
    costs={r['id']:{'红':0,'蓝':1,'绿':2,'白':3}.get(r.get('required_crystals')) for r in originals}
    for skill in ('hero_skill_pool_003:09','hero_skill_pool_007:09','hero_skill_pool_004:09','hero_skill_pool_005:09'):
        definition=copy.deepcopy(next(r['task']['definition'] for r in rows
                                     if skill in r['task']['definition'].get('Build',{}).get('SkillIds',[])))
        definition['Build']['Crystals']={c:1 for c in ('Red','Blue','Green','White')}
        current=env.reset_task(definition);traces.append([])
        if skill.endswith(('003:09','007:09')):
            consumer=next(i for i in current['legal'] if env.actions[i]['Kind']=='play'
                          and env.actions[i]['Card'] in costs and costs[env.actions[i]['Card']] is not None
                          and env.actions[i]['Choice'].startswith('strong'))
            color=costs[env.actions[consumer]['Card']]
            if skill.endswith('003:09'):
                arm=next(i for i in current['legal'] if env.actions[i]['Kind']=='sharedArm' and env.actions[i]['Option']==color)
                step(arm);assert skill not in current['state']['usedSkillsRound']
                step(find('sharedCancel',skill));assert current['state']['sharedSkills']['enhancementColor'] is None
                step(arm)
            else:
                use=next(i for i in current['legal'] if env.actions[i]['Kind']=='sharedUse' and env.actions[i]['Card']==skill and env.actions[i]['Option']==color)
                step(use)
            step(consumer)
            assert skill in current['state']['usedSkillsRound']
            if skill.endswith('003:09'):
                assert current['state']['sharedSkills']['enhancementColor'] is None
                assert current['state']['crystals']['Blue']==1
                assert any(t['skill']==skill for t in current['state']['sharedSkills']['tokens'])
                step(find('sharedClaim',skill))
                assert not current['state']['sharedSkills']['tokens']
            else:
                while current['state']['sharedSkills']['overload']:step(find('sharedOverloadResolve',skill))
                assert not current['state']['sharedSkills']['tokens']
                assert current['state']['influence']==8 and current['state']['tokens'].get('White',0)==0
        elif skill.endswith('004:09'):
            step(find('sharedUse',skill));assert any(t['skill']==skill for t in current['state']['sharedSkills']['tokens'])
            step(find('fight'));assert current['state']['sharedSkills']['howls']==1
            before=copy.deepcopy(current['state']['enemies'][0])
            step(find('sharedHowlResolve',skill));assert current['state']['sharedSkills']['howls']==0
            after=current['state']['enemies'][0]
            assert after['effectiveArmor']==max(1,before['effectiveArmor']-1)
            assert after['attacks'][0]['effectiveAttack']==max(0,before['attacks'][0]['effectiveAttack']-1)
        else:
            step(find('sharedUse',skill,-1))
            step(find('sharedClaim',skill));assert current['state']['sharedSkills']['sourceReturns']
            assert current['state']['tokens'].get('White',0)==1 and current['state']['crystals']['White']==2
            step(find('sharedSourceReturn',skill));assert not current['state']['sharedSkills']['sourceReturns']
            step(find('rest'));step(find('nextTurn'))
            assert current['state']['held'] is None and 'White' in current['state']['source']
    (out/'actual_traces.json').write_text(json.dumps(traces,ensure_ascii=False,indent=2),encoding='utf-8')
    report=dict(status='seven_owner_native_entries_checked_not_full_effects_or_PPO',checks=checks,
                feature_schema=env.info['featureSchema'],features=len(current['features']),actions=len(env.actions),
                gaps=['alternate multiplayer effects',
                      'checkpoint/pool/schema migration','Unity Console and scene acceptance'],
                dll_sha256=hashlib.sha256(Path(os.environ['BATTLE_RL_DLL']).read_bytes()).hexdigest())
    (out/'report.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report))
except Exception as failure:
    (out/'failure.json').write_text(json.dumps(dict(reason=repr(failure),current=current,
        legal_actions=[env.actions[i] for i in current['legal']],traces=traces),ensure_ascii=False,indent=2),encoding='utf-8')
    raise
finally:env.close()
