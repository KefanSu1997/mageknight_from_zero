"""Actual pointer replay of frozen sampled pairs; incomplete mappings retained.

This first worker supports advance and plain sideways. Extending dispatch
must keep source IDs/selected cases frozen and validate native effect.
"""
import argparse
import hashlib
import json
import math
import re
from pathlib import Path
import sqlite3
import zipfile
import time
from collections import OrderedDict
from functools import lru_cache
from gate_unity_cost_task_reset import CostDemo
from gate_unity_cost_prefix import differences
from measure_ui_fluency import MeasurementDemo
from structured_ui_ppo import ROOT,state,write_json
from ui_cost_views import panel,VERSION as VIEW_METRIC_VERSION
from cost_entity_identity import align
from cost_result_io import write_result, write_case_bundle


class ReplayDemo(CostDemo,MeasurementDemo):
    # Use measured click implementation; generic setup RPC remains separate.
    call=MeasurementDemo.call
    def __init__(self,directory):
        try:super().__init__(directory)
        except (TimeoutError,RuntimeError) as error:
            # A menu timeout is an unknown outcome, not permission to launch
            # another scene. Probe the exact existing directory once instead.
            initial_error=repr(error)
            active=Path((ROOT/'AutomationOutputs/structured_ui_pilot_directory.txt').read_text(encoding='utf-8').strip()).resolve()
            if active!=self.directory or not (self.directory/'launch.json').exists():raise
            self.initial_observation=self.call('observe',timeout=60)['observation']
            write_json(self.directory/'launch_recovery.json',dict(initial_error=initial_error,
                same_directory=str(self.directory),repeated_menu=False,probe_succeeded=True))

    @classmethod
    def attach_current(cls,directory):
        directory=Path(directory).resolve()
        active=Path((ROOT/'AutomationOutputs/structured_ui_pilot_directory.txt').read_text(encoding='utf-8').strip()).resolve()
        if active!=directory or not (directory/'launch.json').exists():
            raise ValueError('Attachment requires the current launched pilot directory')
        demo=cls.__new__(cls);demo.directory=directory;demo.calls=0
        demo.initial_observation=demo.call('observe',timeout=30)['observation']
        write_json(directory/'attachment.json',dict(repeated_menu=False,
            observation_version=demo.initial_observation['version'],fresh_observe=True))
        return demo


def click(demo,obs,name,rows,path_part=None):
    matches=[c for c in obs['controls'] if c['name']==name and c['clickable'] and (path_part is None or path_part in c['path'])]
    probe=None
    if not matches:
        candidates=[c for c in obs['controls'] if c['name']==name and c['interactable'] and (path_part is None or path_part in c['path'])]
        if len(candidates)==1:
            probe=demo.call('probe',obs,candidates[0]);obs=probe['observation']
            points=[p for p in probe.get('raycastProbes',[]) if p['valid']]
            if points:
                control=next(c for c in obs['controls'] if c['path']==candidates[0]['path'])
                matches=[dict(control,raycastPoint=points[0])]
    if len(matches)!=1:
        if probe and rows:rows[-1].setdefault('failed_control_probes',[]).append(dict(button=name,response=probe))
        raise ValueError('Missing or ambiguous actual control '+name)
    actual=demo.call('click',obs,matches[0])
    row=dict(button=name,actual=actual)
    if probe:row['raycast_probe']=probe
    rows.append(row);return actual['observation']

def available(obs,name):
    return [c for c in obs['controls'] if c['name']==name and c['clickable']]

def select_card(demo,obs,card,rows,physical_slot=None):
    native=json.loads(state(obs)['audit.costNativeState']);slot=native['hand'].index(card) if physical_slot is None else physical_slot
    if native['hand'][slot]!=card:raise ValueError('Requested physical hand slot differs from original card')
    if native['phase']=='Outside':
        if not any(c['name'].startswith('Btn_Hand_') and c['clickable'] for c in obs['controls']):
            obs=click(demo,obs,'Btn_JourneyHand',rows)
        name='Btn_Hand_'+str(slot)
        for _ in range(32):
            if available(obs,name):return click(demo,obs,name,rows)
            visible=[int(c['name'][9:]) for c in obs['controls'] if c['name'].startswith('Btn_Hand_') and c['clickable']]
            if not visible:raise ValueError('No physical outside hand slots')
            obs=click(demo,obs,'Btn_NextHand' if slot>max(visible) else 'Btn_PreviousHand',rows)
        raise ValueError('Outside hand pagination did not reach physical slot')
    if any('/Skills/' in c['path'] and c['clickable'] for c in obs['controls']) and available(obs,'Btn_Skills'):
        obs=click(demo,obs,'Btn_Skills',rows)
    if not any(c['name'].startswith('Btn_Card_') and c['clickable'] for c in obs['controls']) and available(obs,'Btn_Roster'):
        obs=click(demo,obs,'Btn_Roster',rows)
    page=slot//7;pages=(len(native['hand'])+6)//7
    if pages>1:
        control=next(c for c in obs['controls'] if c['name']=='Btn_HandPage')
        match=re.search(r'手牌\s*(\d+)/(\d+)',control['text'])
        if not match:raise ValueError('Actual hand page indicator missing')
        current=int(match[1])-1
        for _ in range((page-current)%pages):obs=click(demo,obs,'Btn_HandPage',rows)
    return click(demo,obs,'Btn_Card_'+card+'_'+str(native['hand'][:slot].count(card)),rows)

def select_choice(demo,obs,prefix,choice,enhanced,rows):
    mode=prefix+('strong' if enhanced else 'base')
    controls=available(obs,mode)
    if len(controls)!=1:raise ValueError('Original choice mode unavailable: '+mode)
    if '●' not in controls[0]['text']:obs=click(demo,obs,mode,rows)
    if choice in ('base','strong'):return obs
    for _ in range(16):
        controls=available(obs,prefix+choice)
        if controls:
            if '●' not in controls[0]['text']:obs=click(demo,obs,prefix+choice,rows)
            return obs
        if not available(obs,'Btn_MoreChoices'):break
        obs=click(demo,obs,'Btn_MoreChoices',rows)
    raise ValueError('Exact original branch unavailable: '+choice)

def play(demo,obs,action,rows):
    if action.get('Parts',0):raise ValueError('Play carries unbound Parts')
    fields=dict(part.split('=',1) for part in action.get('Input','').split(';') if part)
    copying=action['Card']=='advanced_card_024' and 'copy' in fields
    unknown=fields.keys()-({'colors','dice','crystal','material','spells','copy','borrow'} if copying else {'colors','dice','crystal','material','advanced','spells'})
    if unknown:raise ValueError('Play input fields require explicit mapping: '+','.join(sorted(unknown)))
    if fields.get('material') and not copying and action['Card'] not in ('basic_card_023','advanced_card_020','advanced_card_029','advanced_card_027','advanced_card_025'):
        raise ValueError('Material input requires this card-specific physical filter')
    native=json.loads(state(obs)['audit.costNativeState']);outside=native['phase']=='Outside'
    bindings=json.loads(state(obs)['audit.costFixture'])['cardChoices']
    host=next(x for x in bindings if x['id']==action['Card'])
    choice=next(x for x in host['choices'] if x['id']==action['Choice'])
    ui_choice=action['Choice']
    if native['nextEnhanced'] and host['type']=='Action' and not choice['enhanced']:
        candidate='strong'+ui_choice[4:] if ui_choice.startswith('base') else None
        equivalent=next((c for c in host['choices'] if c['id']==candidate and c['enhanced']
            and c['target']==choice['target'] and c['option']==choice['option']),None)
        if equivalent is None:raise ValueError('Free enhancement lacks an explicit equivalent UI branch')
        choice=equivalent;ui_choice=candidate
    obs=select_card(demo,obs,action['Card'],rows)
    obs=select_choice(demo,obs,'Btn_OutsideChoice_' if outside else 'Btn_Choice_',ui_choice,choice['enhanced'],rows)
    if not outside and choice['target']!='None':
        obs=click(demo,obs,'Btn_Target',rows,'/Enemy_'+str(action['Targets'])+'/')
        for _ in range(action.get('Option',0)):obs=click(demo,obs,'Btn_CardAttackSlot',rows)
    elif action.get('Option',0):raise ValueError('Play Option has no bound attack selector')
    obs=click(demo,obs,'Btn_PlayOutside' if outside else 'Btn_Play',rows)
    if copying:
        if not fields.get('borrow'):raise ValueError('Copy needs an explicit original borrowed choice')
        materials=[x for x in bindings if x['type']=='Action' and x['index']!=host['index']]
        donor=next(x for x in materials if x['id']==fields['copy']);slot=materials.index(donor)
        for _ in range(slot//6):obs=click(demo,obs,'Btn_CopyNext',rows)
        obs=click(demo,obs,'Btn_CopyDonor_'+str(slot),rows)
        obs=click(demo,obs,'Btn_CopyEffect',rows)
        borrowed=dict(action,Card=donor['id'],Choice=fields['borrow'])
        nested={k:v for k,v in fields.items() if k not in ('copy','borrow')}
        return borrowed_effect_inputs(demo,obs,borrowed,nested,choice['enhanced'] or native.get('nextEnhanced',False),rows,donor['index'])
    if available(obs,'Btn_ConfirmInputs'):
        obs=select_choice(demo,obs,'Btn_InputChoice_',action['Choice'],choice['enhanced'],rows)
        for i,color in enumerate(fields.get('colors','').split(',') if fields.get('colors') else []):
            control=available(obs,f'Btn_InputColor_{i}_{color}')
            if not control:raise ValueError('Original input color unavailable')
            if '●' not in control[0]['text']:obs=click(demo,obs,f'Btn_InputColor_{i}_{color}',rows)
        used=set()
        for color in fields.get('dice','').split(',') if fields.get('dice') else []:
            if action['Card']=='magic_012':
                if fields['dice']!=color or fields.get('colors')!=color:
                    raise ValueError('Claim UI needs one matching original source color')
                obs=click(demo,obs,'Btn_ClaimColor_'+color,rows)
                continue
            slot=next(i for i,c in enumerate(native['auditSourceOrder']) if c==color and i not in used);used.add(slot)
            obs=click(demo,obs,'Btn_InputDie_'+str(slot),rows)
        if fields.get('crystal'):obs=click(demo,obs,'Btn_InputCrystal_'+fields['crystal'],rows)
        if fields.get('advanced'):obs=click(demo,obs,'Btn_InputOffer_'+str(native['advancedOffer'].index(fields['advanced'])),rows)
        for spell in fields.get('spells','').split(',') if fields.get('spells') else []:
            obs=click(demo,obs,'Btn_InputOffer_'+str(native['spellOffer'].index(spell)),rows)
        if fields.get('material'):
            by_instance={card['instance']:card for card in native['zones']['hand']}
            cards=[by_instance[instance] for instance in native['handOrder']]
            host_instance=native['handOrder'][host['index']]
            removing=action['Card'] in ('advanced_card_020','advanced_card_029','advanced_card_027')
            materials=[x for i,x in enumerate(cards) if x['instance']!=host_instance and (bindings[i]['type']=='Action' if removing else bindings[i]['type']!='Wound')]
            if action['Card']=='advanced_card_025':
                typed={x['id'] for x in bindings if x['type'] in ('Action','Spell')}
                materials=[x for x in materials if x['id'] in typed and x['id'] in original_colored_cards()]
            slot=next(i for i,x in enumerate(materials) if x['id']==fields['material'])
            for _ in range(slot//4):obs=click(demo,obs,'Btn_InputNext',rows)
            obs=click(demo,obs,'Btn_InputMaterial_'+str(slot),rows)
        obs=click(demo,obs,'Btn_ConfirmInputs',rows)
    elif fields:raise ValueError('Expected explicit input panel was not opened')
    return obs

def combat_commit(demo,obs,action,rows):
    if action.get('Card') or action.get('Choice') or action.get('Input'):
        raise ValueError('Combat commit carries unbound extra fields')
    attacking=action['Kind']=='attack'
    if json.loads(state(obs)['audit.costFixture']).get('uiUnit') is not None:
        obs=click(demo,obs,'Btn_Roster',rows)
    native=json.loads(state(obs)['audit.costNativeState'])
    audit=json.loads(state(obs)['audit.costFixture'])
    wanted={i for i in range(len(native['enemies'])) if action['Targets']&(1<<i)} if attacking else {action['Targets']}
    current=set(audit['uiTargets'])
    if attacking:
        for i in sorted(current^wanted):obs=click(demo,obs,'Btn_Target',rows,'/Enemy_'+str(i)+'/')
    elif current!=wanted:
        obs=click(demo,obs,'Btn_Target',rows,'/Enemy_'+str(action['Targets'])+'/')
    if not attacking:
        for _ in range(16):
            audit=json.loads(state(obs)['audit.costFixture'])
            if audit['uiAttackSlot']==action['Option']:break
            obs=click(demo,obs,'Btn_AttackSlot',rows)
        else:raise ValueError('Attack slot did not reach original index')
    audit=json.loads(state(obs)['audit.costFixture'])
    if attacking:
        indices=list(range(len(native['attacks'])));reserved=set(audit['uiReservedAttacks'])
        desired={i for i in indices if action['Parts']&(1<<i)}
        panel_button='Btn_AttackParts'
    else:
        indices=audit['uiBlockIndices'];reserved=set(audit['uiReservedBlocks'])
        desired=set(indices) if action['Kind']=='block' else {i for i in range(len(native['blocks'])) if action['Parts']&(1<<i)}
        if not desired<=set(indices):raise ValueError('Original block mask includes a source unavailable in the actual UI')
        panel_button='Btn_BlockParts'
    toggles=[i for i in indices if (i not in reserved)!=(i in desired)]
    if toggles:
        obs=click(demo,obs,panel_button,rows)
        for global_index in toggles:
            slot=global_index if attacking else indices.index(global_index)
            for _ in range(32):
                name='Btn_Part_'+str(slot)
                if available(obs,name):
                    obs=click(demo,obs,name,rows);break
                visible=[int(c['name'][9:]) for c in obs['controls'] if c['name'].startswith('Btn_Part_')]
                if slot in visible and attacking and global_index not in desired:break  # UI excludes ineligible source automatically
                if not visible:raise ValueError('No actual combat source controls')
                obs=click(demo,obs,'Btn_Next' if slot>max(visible) else 'Btn_Previous',rows)
            else:raise ValueError('Combat source pagination did not reach index')
        obs=click(demo,obs,'Btn_Done',rows)
    return click(demo,obs,'Btn_Resolve',rows)

def unit_action(demo,obs,action,rows):
    fields=dict(part.split('=',1) for part in action.get('Input','').split(';') if part)
    if fields.keys()-{'colors','boost'}:raise ValueError('Unbound unit input fields')
    native=json.loads(state(obs)['audit.costNativeState']);outside=native['phase']=='Outside'
    audit=json.loads(state(obs)['audit.costFixture'])
    binding=next(x for x in audit['unitAbilities'] if x['id']==action['Card']);slot=binding['index'];ability=int(action['Choice'])
    profile=next(x for x in binding['abilities'] if x['slot']==ability)
    if outside:
        if not any('/OwnedUnits/' in c['path'] for c in obs['controls']):
            if not available(obs,'Btn_OwnedUnits'):obs=click(demo,obs,'Btn_JourneyHand',rows)
            obs=click(demo,obs,'Btn_OwnedUnits',rows)
        name='Btn_Unit_'+str(slot)
        for _ in range(32):
            if available(obs,name):break
            visible=[int(c['name'][9:]) for c in obs['controls'] if c['name'].startswith('Btn_Unit_') and c['clickable']]
            if not visible:raise ValueError('No actual owned unit slots')
            obs=click(demo,obs,'Btn_Next' if slot>max(visible) else 'Btn_Previous',rows)
        if json.loads(state(obs)['audit.costFixture']).get('uiOutsideUnit')!=action['Card']:obs=click(demo,obs,name,rows)
        controls=available(obs,'Btn_Ability_'+str(ability))
        if not controls:raise ValueError('Original outside unit ability missing')
        if '●' not in controls[0]['text']:obs=click(demo,obs,'Btn_Ability_'+str(ability),rows)
        if profile['kind']=='MovementOrInfluence':
            controls=available(obs,'Btn_Option_'+str(action['Option']))
            if not controls or '●' not in controls[0]['text']:obs=click(demo,obs,'Btn_Option_'+str(action['Option']),rows)
        if profile['kind']=='ReadyUnit':obs=click(demo,obs,'Btn_Target_'+str(action['Targets']),rows)
        for i,color in enumerate(fields.get('colors','').split(',') if fields.get('colors') else []):
            controls=available(obs,f'Btn_Color_{i}_{color}')
            if not controls:raise ValueError('Actual outside unit color unavailable')
            if '●' not in controls[0]['text']:obs=click(demo,obs,f'Btn_Color_{i}_{color}',rows)
        if action.get('Parts') or fields.get('boost'):raise ValueError('Outside unit carries unsupported extra parameters')
        return click(demo,obs,'Btn_Use',rows)
    if not available(obs,'Btn_Unit_'+str(slot)):obs=click(demo,obs,'Btn_Roster',rows)
    if json.loads(state(obs)['audit.costFixture']).get('uiUnit')!=action['Card']:obs=click(demo,obs,'Btn_Unit_'+str(slot),rows)
    if profile['targeted']:
        if set(json.loads(state(obs)['audit.costFixture'])['uiTargets'])!={action['Targets']}:
            obs=click(demo,obs,'Btn_Target',rows,'/Enemy_'+str(action['Targets'])+'/')
        for _ in range(16):
            if json.loads(state(obs)['audit.costFixture'])['uiAttackSlot']==action['Parts']:break
            obs=click(demo,obs,'Btn_AttackSlot',rows)
    elif action.get('Parts'):raise ValueError('Unit Parts lacks an actual attack selector')
    if not available(obs,'Btn_UnitAbility_'+str(ability)) and available(obs,'Btn_UnitUtility'):
        obs=click(demo,obs,'Btn_UnitUtility',rows)
    if available(obs,'Btn_UnitMode_'+str(ability)):
        if json.loads(state(obs)['audit.costFixture'])['uiUnitMode']!=ability:obs=click(demo,obs,'Btn_UnitMode_'+str(ability),rows)
        for i,color in enumerate(fields.get('colors','').split(',') if fields.get('colors') else []):
            key='uiUnitManaFirst' if i==0 else 'uiUnitManaSecond';button='Btn_UnitManaFirst' if i==0 else 'Btn_UnitManaSecond'
            for _ in range(6):
                if json.loads(state(obs)['audit.costFixture'])[key]==color:break
                obs=click(demo,obs,button,rows)
        if fields.get('boost'):
            for _ in range(4):
                if json.loads(state(obs)['audit.costFixture'])['uiUnitBoost']==int(fields['boost']):break
                obs=click(demo,obs,'Btn_UnitBoost',rows)
        if ability==2 and json.loads(state(obs)['audit.costFixture'])['uiUnitConversion']!=action['Option']:
            obs=click(demo,obs,'Btn_UnitConversion',rows)
    elif fields:raise ValueError('Unit inputs require a special ability panel')
    if available(obs,'Btn_UnitSupportTarget') and profile['kind'] in ('Healing','ReadyUnit'):
        for _ in range(len(native['units'])+1):
            if json.loads(state(obs)['audit.costFixture'])['uiSupportTarget']==action['Targets']:break
            obs=click(demo,obs,'Btn_UnitSupportTarget',rows)
    return click(demo,obs,'Btn_UnitAbility_'+str(ability),rows)

SHARED_KINDS={'sharedArm','sharedCancel','sharedUse','sharedClaim','sharedSourceReturn','sharedHowlResolve'}

def open_owned_special_skill(demo,obs,card,rows):
    native=json.loads(state(obs)['audit.costNativeState'])
    audit=json.loads(state(obs)['audit.costFixture']);slot=next(x['index'] for x in audit['skillBindings'] if x['id']==card)
    if native['phase']=='Outside':
        if not available(obs,'Btn_BackSupport'):
            if not available(obs,'Btn_Support'):obs=click(demo,obs,'Btn_JourneyHand',rows)
            obs=click(demo,obs,'Btn_Support',rows)
        if json.loads(state(obs)['audit.costFixture'])['uiOutsideSkill']!=card:obs=click(demo,obs,'Btn_OutsideSkill_'+str(slot),rows)
        return click(demo,obs,'Btn_UseOutsideSkill',rows)
    if not any(c['name'].startswith('Btn_Skill_') and c['clickable'] for c in obs['controls']):obs=click(demo,obs,'Btn_Skills',rows)
    name='Btn_Skill_'+str(slot)
    for _ in range(len(native['skills'])+1):
        if available(obs,name):break
        obs=click(demo,obs,'Btn_NextSkill',rows)
    if json.loads(state(obs)['audit.costFixture'])['uiSkill']!=card:obs=click(demo,obs,name,rows)
    return click(demo,obs,'Btn_UseSkill',rows)


def fork_skill(demo,obs,action,rows):
    if action['Card']!='hero_skill_pool_001:05' or action['Choice'] or action['Input'] or action['Parts'] or action['Option']:
        raise ValueError('Unbound original forked lightning parameters')
    native=json.loads(state(obs)['audit.costNativeState'])
    targets=[i for i in range(len(native['enemies'])) if action['Targets']&(1<<i)]
    if not 1<=len(targets)<=3 or action['Targets']>>len(native['enemies']):raise ValueError('Actual forked targets required')
    alive=[i for i,enemy in enumerate(native['enemies']) if not enemy['defeated']]
    obs=open_owned_special_skill(demo,obs,action['Card'],rows)
    for target in targets:
        slot=alive.index(target)
        for _ in range(16):
            if available(obs,'Btn_Enemy_'+str(slot)):break
            obs=click(demo,obs,'Btn_Next',rows)
        obs=click(demo,obs,'Btn_Enemy_'+str(slot),rows)
    return click(demo,obs,'Btn_Confirm',rows)


def shapeshift_skill(demo,obs,action,rows):
    if action['Card']!='hero_skill_pool_001:06' or action['Choice'] not in ('base','strong'):
        raise ValueError('Original shapeshift skill and mode required')
    fields=dict(p.split('=',1) for p in action['Input'].split(';') if p)
    if fields.keys()-{'materialSlot'}:raise ValueError('Unbound original shapeshift inputs')
    native=json.loads(state(obs)['audit.costNativeState'])
    desired=0 if not native['inBattle'] else 2 if native['phase']=='Block' else 1
    if action['Targets']!=desired:raise ValueError('Original shapeshift output direction differs from actual UI phase')
    bindings=json.loads(state(obs)['audit.costFixture'])['cardChoices'];host=bindings[action['Parts']]
    eligible=[x['index'] for x in bindings if x['type']=='Action' and x['id'].startswith('basic_card_')]
    slot=eligible.index(host['index'])
    obs=open_owned_special_skill(demo,obs,action['Card'],rows)
    for _ in range(slot//6):obs=click(demo,obs,'Btn_Next',rows)
    obs=click(demo,obs,'Btn_Card_'+str(slot),rows)
    if action['Choice']=='strong':obs=click(demo,obs,'Btn_Strong',rows)
    # Output indices are original ShapeshiftActions options, not UI slots.
    obs=click(demo,obs,'Btn_Output_'+str(action['Option']),rows)
    if fields:
        if host['id']!='basic_card_023':raise ValueError('Shapeshift material belongs only to original improvisation')
        material=int(fields['materialSlot'])
        candidates=[x['index'] for x in bindings if x['index']!=host['index'] and x['type']!='Wound']
        slot=candidates.index(material)
        obs=click(demo,obs,'Btn_Material',rows)
        for _ in range(slot//6):obs=click(demo,obs,'Btn_Next',rows)
        obs=click(demo,obs,'Btn_Card_'+str(slot),rows)
    return click(demo,obs,'Btn_Confirm',rows)


def puppet_skill(demo,obs,action,rows):
    if action['Card']!='hero_skill_pool_003:06' or action['Input'] or action['Parts'] or action['Targets']:
        raise ValueError('Unbound original puppet parameters')
    keeping=action['Kind']=='puppetKeep'
    if action['Option'] not in ((0,) if keeping else (1,2)):raise ValueError('Original puppet mode required')
    native=json.loads(state(obs)['audit.costNativeState'])
    tokens=native['puppetCandidates' if keeping else 'puppetStored']
    slot=next(i for i,t in enumerate(tokens) if t['id']==action['Choice'])
    obs=open_owned_special_skill(demo,obs,action['Card'],rows)
    if not keeping:obs=click(demo,obs,'Btn_Mode_'+str(action['Option']),rows)
    for _ in range(slot//4):obs=click(demo,obs,'Btn_Next',rows)
    obs=click(demo,obs,'Btn_Enemy_'+str(slot),rows)
    return click(demo,obs,'Btn_Confirm',rows)


def offered_unit(demo,obs,action,rows):
    fields=dict(p.split('=',1) for p in action['Input'].split(';') if p)
    if fields.keys()-{'colors','boost'}:raise ValueError('Unbound original offered-unit inputs')
    native=json.loads(state(obs)['audit.costNativeState']);slot=native['unitOffer'].index(action['Card']);ability=int(action['Choice'])
    obs=click(demo,obs,'Btn_OfferedUnits',rows)
    for _ in range(slot//3):obs=click(demo,obs,'Btn_OfferNext',rows)
    obs=click(demo,obs,'Btn_Offer_'+str(slot),rows)
    name='Btn_Ability_'+str(ability);control=available(obs,name)
    if not control:raise ValueError('Original offered ability unavailable')
    if '●' not in control[0]['text']:obs=click(demo,obs,name,rows)
    rule=available(obs,name)[0]['text'].removeprefix('● ').split(' [',1)[0]
    # Exact original targeted cases in UnitJsonLoader/CostPrefixAdapter.
    targeted={
        '使一个敌人的护甲和一次攻击降低1点。',
        '使一次敌人的攻击降低3点。即使敌人具有刺杀，此次攻击造成的伤害必须先分配给这支部队。',
        '目标敌人不攻击。','如果目标敌人被格挡，消灭目标敌人。',
        '目标敌人护甲降低5点（最低至1）。','目标敌人失去所有城防和抗性。',
        '本次战斗中无城防的目标敌人不攻击。',
        '目标敌人失去所有城防。远程攻击3。','目标敌人失去所有抗性。远程攻击3。',
        '本次战斗中目标敌人不攻击，且受到护甲-3（最低至1）。'}
    if rule in targeted:obs=click(demo,obs,'Btn_Target_'+str(action['Targets']),rows)
    elif rule=='重整一支1或2级部队。':obs=click(demo,obs,'Btn_UnitTarget_'+str(action['Targets']),rows)
    if action['Parts']:
        obs=click(demo,obs,'Btn_AttackSlot_'+str(action['Parts']),rows)
    options=available(obs,'Btn_Option_'+str(action['Option']))
    if options:
        if '●' not in options[0]['text']:obs=click(demo,obs,'Btn_Option_'+str(action['Option']),rows)
    elif action['Option']:raise ValueError('Offered ability has no original option selector')
    if fields.get('boost'):
        name='Btn_Boost_'+fields['boost'];control=available(obs,name)
        if not control:raise ValueError('Original offered-unit boost unavailable')
        if '●' not in control[0]['text']:obs=click(demo,obs,name,rows)
    for i,color in enumerate(fields.get('colors','').split(',') if fields.get('colors') else []):
        name=f'Btn_Color_{i}_{color}';control=available(obs,name)
        if not control:raise ValueError('Original offered-unit color unavailable')
        if '●' not in control[0]['text']:obs=click(demo,obs,name,rows)
    return click(demo,obs,'Btn_Use',rows)


def leadership_unit(demo,obs,action,rows):
    native=json.loads(state(obs)['audit.costNativeState'])
    # The original native protocol uses the ordinary outside ability here.
    if native['phase']!='Outside':obs=open_owned_special_skill(demo,obs,'hero_skill_pool_002:06',rows)
    return unit_action(demo,obs,action,rows)

@lru_cache(maxsize=1)
def original_colored_cards():
    # Mirror CardColorHelper's public JSON membership, not an invented ID range.
    colored=set();valid={'红','藍','蓝','綠','绿','白','金','黑','紫','无'}
    for name,field in (('basic_card.json','required_crystals'),('advanced_card.json','required_crystals'),('magic.json','mana_color')):
        for card in json.loads((ROOT/'resources/text_json'/name).read_text(encoding='utf-8-sig')):
            raw=card.get(field) or ''
            if not raw.strip():continue
            parts=[x.strip().removesuffix('色') for x in raw.replace('，',',').replace('、',',').split(',') if x]
            if any(x not in valid for x in parts):raise ValueError('Public original color parser mismatch: '+card['id'])
            if parts:colored.add(card['id'])
    return colored

def repeated_effect(demo,obs,action,rows):
    fields=dict(p.split('=',1) for p in action.get('Input','').split(';') if p)
    if fields.keys()-{'colors','dice','crystal','material','spells'}:raise ValueError('Unbound repeated input fields')
    if action['Parts']:raise ValueError('Repeated Parts requires explicit mapping')
    native=json.loads(state(obs)['audit.costNativeState']);pending=native['repeated']
    if not pending or pending['original']!=action['Card']:raise ValueError('Actual repeated donor differs')
    obs=click(demo,obs,'Btn_ContinueOriginal',rows)
    return borrowed_effect_inputs(demo,obs,action,fields,pending['enhanced'],rows)


def borrowed_effect_inputs(demo,obs,action,fields,enhanced,rows,physical_donor_slot=None):
    if fields.keys()-{'colors','dice','crystal','material','spells'}:raise ValueError('Unbound borrowed input fields')
    native=json.loads(state(obs)['audit.costNativeState'])
    obs=select_choice(demo,obs,'Btn_InputChoice_',action['Choice'],enhanced,rows)
    if available(obs,'Btn_InputEnemy_'+str(action['Targets'])):
        obs=click(demo,obs,'Btn_InputEnemy_'+str(action['Targets']),rows)
        if action['Option']:obs=click(demo,obs,'Btn_InputAttack_'+str(action['Option']),rows)
    elif action['Targets'] or action['Option']:raise ValueError('Repeated target/attack selector unavailable')
    for i,color in enumerate(fields.get('colors','').split(',') if fields.get('colors') else []):
        control=available(obs,f'Btn_InputColor_{i}_{color}')
        if not control:raise ValueError('Original repeated color unavailable')
        if '●' not in control[0]['text']:obs=click(demo,obs,f'Btn_InputColor_{i}_{color}',rows)
    used=set()
    for color in fields.get('dice','').split(',') if fields.get('dice') else []:
        slot=next(i for i,c in enumerate(native['auditSourceOrder']) if c==color and i not in used);used.add(slot)
        obs=click(demo,obs,'Btn_InputDie_'+str(slot),rows)
    if fields.get('crystal'):obs=click(demo,obs,'Btn_InputCrystal_'+fields['crystal'],rows)
    for spell in fields.get('spells','').split(',') if fields.get('spells') else []:
        obs=click(demo,obs,'Btn_InputOffer_'+str(native['spellOffer'].index(spell)),rows)
    if fields.get('material'):
        if action['Card'] not in ('basic_card_023','advanced_card_025'):raise ValueError('Repeated material needs its original card-specific filter')
        bindings=json.loads(state(obs)['audit.costFixture'])['cardChoices']
        materials=[x for x in bindings if x['type']!='Wound' and x['index']!=physical_donor_slot]
        if action['Card']=='advanced_card_025':
            materials=[x for x in materials if x['type'] in ('Action','Spell') and x['id'] in original_colored_cards()]
        slot=next(i for i,x in enumerate(materials) if x['id']==fields['material'])
        for _ in range(slot//4):obs=click(demo,obs,'Btn_InputNext',rows)
        obs=click(demo,obs,'Btn_InputMaterial_'+str(slot),rows)
    return click(demo,obs,'Btn_ConfirmInputs',rows)


WORLD_KINDS={'worldMove','worldFly','worldInfluence','worldInteract','worldInteractEnd'}

def cleanup_choice(demo,obs,action,rows):
    kind=action['Kind'];native=json.loads(state(obs)['audit.costNativeState'])
    if action.get('Choice') or action.get('Input') or action.get('Parts') or action['Option'] not in (0,1):
        raise ValueError('Unbound cleanup parameters')
    if kind=='protectionCleanup':
        if action['Card']!='items_002' or action['Targets']:raise ValueError('Original protection cleanup identity required')
        if native['phase']!='Outside' and not (native['phase']=='End' and (available(obs,'Btn_JourneyHand') or available(obs,'Btn_CloseHand'))):
            raise ValueError('Protection cleanup outside control not available in this actual phase')
        if not available(obs,'Btn_CloseHand'):obs=click(demo,obs,'Btn_JourneyHand',rows)
        control=available(obs,'Btn_ProtectionCleanup')
        if not control:raise ValueError('Actual protection cleanup checkbox unavailable')
        wanted=action['Option']==1
        if ('●' in control[0]['text'])!=wanted:obs=click(demo,obs,'Btn_ProtectionCleanup',rows)
        return obs
    if action['Card'] or not native['roundWindow']:raise ValueError('Original banner round window required')
    name='Btn_BannerReturn_0_'+str(action['Targets'])
    for _ in range(16):
        control=available(obs,name)
        if control:break
        if not available(obs,'Btn_BannerNext'):raise ValueError('Actual banner return slot unavailable')
        obs=click(demo,obs,'Btn_BannerNext',rows)
    else:raise ValueError('Banner return pagination did not reach original unit')
    if ('已选取回' in control[0]['text'])!=(action['Option']==1):obs=click(demo,obs,name,rows)
    return obs

def world_action(demo,obs,action,rows):
    if (action.get('Card') and action['Kind']!='worldFly') or action.get('Choice') or action.get('Input') or action.get('Option'):
        raise ValueError('World action carries unbound parameters')
    native=json.loads(state(obs)['audit.costNativeState'])
    if native['phase']!='Outside' or not native.get('world',{}).get('enabled'):
        raise ValueError('Actual original world outside turn required')
    kind=action['Kind']
    if kind=='worldFly':
        if action['Card']!='hero_skill_pool_005:06':raise ValueError('World flight requires its original supported skill')
        slot=native['skills'].index(action['Card'])
        if not available(obs,'Btn_Support'):obs=click(demo,obs,'Btn_JourneyHand',rows)
        obs=click(demo,obs,'Btn_Support',rows)
        obs=click(demo,obs,'Btn_OutsideSkill_'+str(slot),rows)
        obs=click(demo,obs,'Btn_UseOutsideSkill',rows)
        obs=click(demo,obs,f"Btn_Cell_{action['Targets']}_{action['Parts']}",rows)
        return click(demo,obs,'Btn_Move',rows)
    if kind=='worldMove':
        if not available(obs,'Btn_CloseMap'):obs=click(demo,obs,'Btn_JourneyMap',rows)
        obs=click(demo,obs,f"Btn_Cell_{action['Targets']}_{action['Parts']}",rows)
        return click(demo,obs,'Btn_Move',rows)
    if kind=='worldInfluence':
        if action['Targets']:raise ValueError('Influence contains unbound Targets')
        slot=action['Parts'];obs=select_card(demo,obs,native['hand'][slot],rows,physical_slot=slot)
        return click(demo,obs,'Btn_SidewaysInfluence',rows)
    if action['Targets'] or action['Parts']:raise ValueError('Site interaction contains unbound parameters')
    if not available(obs,'Btn_CloseInteraction'):obs=click(demo,obs,'Btn_JourneyInteraction',rows)
    return click(demo,obs,'Btn_BeginInteraction' if kind=='worldInteract' else 'Btn_EndInteraction',rows)


def shared_action(demo,obs,action,rows):
    if action.get('Input'):raise ValueError('Unbound shared input fields')
    kind=action['Kind'];card=action['Card']
    if not any('/SharedSkills/' in c['path'] and c['clickable'] for c in obs['controls']):
        if not available(obs,'Btn_SharedSkills'):
            if not available(obs,'Btn_JourneyTools'):obs=click(demo,obs,'Btn_Menu',rows)
            obs=click(demo,obs,'Btn_JourneyTools',rows)
        obs=click(demo,obs,'Btn_SharedSkills',rows)
    native=json.loads(state(obs)['audit.costNativeState'])
    if kind=='sharedSourceReturn':
        return click(demo,obs,('Btn_ReturnRoll_' if action['Option']==1 else 'Btn_ReturnKeep_')+str(action['Parts']),rows)
    if kind=='sharedHowlResolve':
        obs=click(demo,obs,'Btn_ArmorEnemy_'+str(action['Targets']),rows)
        obs=click(demo,obs,'Btn_Enemy_'+str(action['Parts']),rows)
        for _ in range(action['Option']):obs=click(demo,obs,'Btn_AttackSlot',rows)
        return click(demo,obs,'Btn_Confirm',rows,'/SharedSkills/')
    claiming=kind=='sharedClaim'
    if claiming:
        slot=next(i for i,t in enumerate(native['sharedSkills']['tokens']) if t['skill']==card)
        obs=click(demo,obs,'Btn_Token_'+str(slot),rows)
    else:
        bindings=json.loads(state(obs)['audit.costFixture'])['skillBindings']
        own=[x['id'] for x in bindings if x['interactive']]
        obs=click(demo,obs,'Btn_Own_'+str(own.index(card)),rows)
    if kind=='sharedCancel':return click(demo,obs,'Btn_CancelEnhancement',rows)
    if kind=='sharedArm' or card=='hero_skill_pool_007:09' and not claiming:
        color=('Red','Blue','Green','White','Gold','Black')[action['Option']]
        if json.loads(state(obs)['audit.costFixture'])['uiSharedColor']!=color:
            obs=click(demo,obs,'Btn_Color_'+color,rows)
    elif card=='hero_skill_pool_001:09':
        obs=click(demo,obs,'Btn_Enemy_'+str(action['Targets']),rows)
        for _ in range(action['Option']):obs=click(demo,obs,'Btn_AttackSlot',rows)
    elif card=='hero_skill_pool_005:09':
        if action['Parts']>=0:obs=click(demo,obs,'Btn_Die_'+str(action['Parts']),rows)
    elif card in ('hero_skill_pool_000:09','hero_skill_pool_004:09'):
        audit=json.loads(state(obs)['audit.costFixture'])
        if claiming or card=='hero_skill_pool_004:09':
            if audit['uiSharedSideways']!=action['Choice']:obs=click(demo,obs,'Btn_Sideways_'+action['Choice'],rows)
            slots=[action['Parts']]
            filtered=[x['index'] for x in audit['cardChoices'] if x['type']=='Wound'] if card=='hero_skill_pool_000:09' else list(range(len(native['hand'])))
        else:
            slots=[i for i in range(8) if action['Parts']&(1<<i)]
            filtered=[x['index'] for x in audit['cardChoices'] if x['type']=='Wound']
        for physical in slots:
            index=filtered.index(physical)
            for _ in range(32):
                if available(obs,'Btn_Card_'+str(index)):break
                visible=[int(c['name'][9:]) for c in obs['controls'] if c['clickable'] and c['name'].startswith('Btn_Card_') and c['name'][9:].isdigit()]
                if not visible:raise ValueError('No actual shared physical hand slots')
                obs=click(demo,obs,'Btn_NextCards' if index>max(visible) else 'Btn_PreviousCards',rows)
            obs=click(demo,obs,'Btn_Card_'+str(index),rows,'/SharedSkills/')
    elif card not in ('hero_skill_pool_002:09','hero_skill_pool_003:09'):
        raise ValueError('Unknown original shared skill')
    return click(demo,obs,'Btn_Confirm',rows,'/SharedSkills/')


def banner_use(demo,obs,action,rows):
    if action.get('Choice') or action.get('Input'):raise ValueError('Banner use carries unbound fields')
    native=json.loads(state(obs)['audit.costNativeState']);slot=action['Targets']
    unit=native['units'][slot]
    if unit['bannerEffect']!=action['Card']:raise ValueError('Physical unit does not hold the requested original banner')
    if native['phase']=='Outside':
        if action['Card']!='items_003' or action['Parts'] or action['Option']:
            raise ValueError('Unbound original outside banner parameters')
        if not any('/OwnedUnits/' in c['path'] and c['clickable'] for c in obs['controls']):
            if not available(obs,'Btn_OwnedUnits'):obs=click(demo,obs,'Btn_JourneyHand',rows)
            obs=click(demo,obs,'Btn_OwnedUnits',rows)
        for _ in range(32):
            if available(obs,'Btn_Unit_'+str(slot)):break
            visible=[int(c['name'][9:]) for c in obs['controls'] if c['name'].startswith('Btn_Unit_') and c['clickable']]
            if not visible:raise ValueError('No actual owned unit slots for original banner')
            obs=click(demo,obs,'Btn_Next' if slot>max(visible) else 'Btn_Previous',rows)
        obs=click(demo,obs,'Btn_Unit_'+str(slot),rows)
        if not available(obs,'Btn_Courage'):raise ValueError('Original courage banner use control is unavailable outside battle')
        return click(demo,obs,'Btn_Courage',rows)
    if not available(obs,'Btn_Unit_'+str(slot)):obs=click(demo,obs,'Btn_Roster',rows)
    if json.loads(state(obs)['audit.costFixture']).get('uiUnit')!=unit['id']:
        obs=click(demo,obs,'Btn_Unit_'+str(slot),rows)
    if action['Card']=='items_018':
        if action['Parts'] or action['Option'] not in (0,1):raise ValueError('Unbound fortitude parameters')
        if not any(c['name']=='Btn_Fortitude' and c['interactable'] for c in obs['controls']):raise ValueError('Original fortitude checkbox is unavailable in this actual UI phase')
        if unit['fortitudeSelected']==(action['Option']==1):
            if not available(obs,'Btn_Fortitude'):raise ValueError('No-input fortitude intent needs a visible checkbox proof')
            return obs
        return click(demo,obs,'Btn_Fortitude',rows)
    if action['Card']=='items_003':
        if action['Parts'] or action['Option']:raise ValueError('Unbound courage parameters')
        return click(demo,obs,'Btn_Courage',rows)
    if action['Card']=='items_001':
        audit=json.loads(state(obs)['audit.costFixture'])
        desired={action['Parts']}
        for enemy in sorted(set(audit['uiTargets'])^desired):
            obs=click(demo,obs,'Btn_Target',rows,'/Enemy_'+str(enemy)+'/')
        if not available(obs,'Btn_UseFear'):obs=click(demo,obs,'Btn_BannerMode',rows)
        for _ in range(16):
            if json.loads(state(obs)['audit.costFixture'])['uiAttackSlot']==action['Option']:break
            obs=click(demo,obs,'Btn_AttackSlot',rows)
        else:raise ValueError('Fear banner attack selector did not reach original index')
        return click(demo,obs,'Btn_UseFear',rows)
    raise ValueError('Unknown original banner use')


def damage_plan(demo,obs,action,rows):
    if action.get('Card') or action.get('Input') or action['Option'] not in (0,1):raise ValueError('Unbound damage planning parameters')
    native=json.loads(state(obs)['audit.costNativeState'])
    if native['phase']!='AssignDamage':raise ValueError('Actual damage planning phase required')
    target,attack=action['Targets'],action['Parts'];units=native['units']
    desired=[] if action['Option']==1 else [units[int(i)]['id'] for i in action['Choice']] if action['Choice'] else [u['id'] for u in units if not u['destroyed']]
    profile=native['enemies'][target]['attacks'][attack];forced=profile.get('forcedUnit') if profile['effectiveAttack']>0 else None
    if forced:desired=[forced]+[u for u in desired if u!=forced]
    current=next((p['units'] for p in native['unitDamagePlan'] if p['target']==target and p['attack']==attack),[])
    audit=json.loads(state(obs)['audit.costFixture'])
    if set(audit['uiTargets'])!={target}:obs=click(demo,obs,'Btn_Target',rows,'/Enemy_'+str(target)+'/')
    if current==desired:return obs  # visible native plan already fulfils this intent; no synthetic submit
    if not any(c['name'].startswith('Btn_Unit_') and c['clickable'] for c in obs['controls']):obs=click(demo,obs,'Btn_Roster',rows)
    selectable=next(i for i,u in enumerate(units) if not u['destroyed'])
    obs=click(demo,obs,'Btn_Unit_'+str(selectable),rows)
    for _ in range(16):
        if json.loads(state(obs)['audit.costFixture'])['uiAttackSlot']==attack:break
        obs=click(demo,obs,'Btn_AttackSlot',rows)
    # Keep the longest desired prefix already present as an ordered subsequence;
    # UI can remove existing assignments and append new physical units.
    keep=[];position=0
    for name in desired:
        try:position=current.index(name,position)+1;keep.append(name)
        except ValueError:break
    remove=[u for u in current if u not in keep and u!=forced]
    add=desired[len(keep):]
    for name in remove+add:
        slot=next(i for i,u in enumerate(units) if u['id']==name)
        if json.loads(state(obs)['audit.costFixture']).get('uiUnit')!=name:obs=click(demo,obs,'Btn_Unit_'+str(slot),rows)
        obs=click(demo,obs,'Btn_AssignUnit',rows)
    return obs

def skill_action(demo,obs,action,rows):
    if action.get('Input') or action.get('Choice'):raise ValueError('Skill carries unbound input fields')
    native=json.loads(state(obs)['audit.costNativeState']);outside=native['phase']=='Outside'
    profile=next(x for x in json.loads(state(obs)['audit.costFixture'])['skillBindings'] if x['id']==action['Card'])
    slot=profile['index'];option=action['Option']
    if profile['sideways'] or action['Card'] in ('hero_skill_pool_001:05','hero_skill_pool_003:06','hero_skill_pool_002:06','hero_skill_pool_004:04','hero_skill_pool_001:06'):
        raise ValueError('Special skill requires its explicit physical selection panel')
    if outside:
        if not available(obs,'Btn_BackSupport'):
            if not available(obs,'Btn_Support'):obs=click(demo,obs,'Btn_JourneyHand',rows)
            obs=click(demo,obs,'Btn_Support',rows)
        if json.loads(state(obs)['audit.costFixture'])['uiOutsideSkill']!=action['Card']:
            obs=click(demo,obs,'Btn_OutsideSkill_'+str(slot),rows)
        controls=available(obs,'Btn_OutsideOption_'+str(option))
        if not controls:raise ValueError('Original outside skill option unavailable')
        if '●' not in controls[0]['text']:obs=click(demo,obs,'Btn_OutsideOption_'+str(option),rows)
        if action['Card']=='hero_skill_pool_002:02':
            alive=[u for u in native['units'] if not u['destroyed']]
            target=alive.index(native['units'][action['Targets']])
            for _ in range(target//4):obs=click(demo,obs,'Btn_SkillUnitsNext',rows)
            obs=click(demo,obs,'Btn_SkillUnit_'+str(target),rows)
        if profile['wound']:obs=click(demo,obs,'Btn_Wound_0',rows)
        if action.get('Parts'):raise ValueError('Outside skill Parts has no bound selector')
        return click(demo,obs,'Btn_UseOutsideSkill',rows)
    if not any(c['name'].startswith('Btn_Skill_') and c['clickable'] for c in obs['controls']):
        obs=click(demo,obs,'Btn_Skills',rows)
    name='Btn_Skill_'+str(slot)
    for _ in range(len(native['skills'])):
        if available(obs,name):break
        obs=click(demo,obs,'Btn_NextSkill',rows)
    if json.loads(state(obs)['audit.costFixture'])['uiSkill']!=action['Card']:obs=click(demo,obs,name,rows)
    targeted=profile['needsEnemyZero'] if option==0 else profile['needsEnemyOther']
    if targeted and set(json.loads(state(obs)['audit.costFixture'])['uiTargets'])!={action['Targets']}:
        obs=click(demo,obs,'Btn_Target',rows,'/Enemy_'+str(action['Targets'])+'/')
    controls=available(obs,'Btn_SkillChoice_'+str(option))
    if not controls:raise ValueError('Original battle skill option unavailable')
    if '●' not in controls[0]['text']:obs=click(demo,obs,'Btn_SkillChoice_'+str(option),rows)
    attack=profile['attack200Plus'] if option>=200 else profile['attackZero'] if option==0 else profile['attackOne'] if option==1 else profile['attackOther']
    if attack:
        for _ in range(16):
            if json.loads(state(obs)['audit.costFixture'])['uiAttackSlot']==action['Parts']:break
            obs=click(demo,obs,'Btn_SkillAttackSlot',rows)
    elif action.get('Parts'):raise ValueError('Skill Parts has no actual attack selector')
    if profile['wound']:
        obs=click(demo,obs,'Btn_SkillWound',rows);obs=click(demo,obs,'Btn_Wound_0',rows)
    return click(demo,obs,'Btn_UseSkill',rows)

def sideways_skill(demo,obs,action,rows):
    if action.get('Input') or action.get('Targets'):raise ValueError('Unbound sideways skill parameters')
    native=json.loads(state(obs)['audit.costNativeState']);outside=native['phase']=='Outside'
    audit=json.loads(state(obs)['audit.costFixture']);profile=next(x for x in audit['skillBindings'] if x['id']==action['Card'])
    if not profile['sideways']:raise ValueError('Original skill is not a sideways skill')
    slot=profile['index']
    if outside:
        if not available(obs,'Btn_BackSupport'):
            if not available(obs,'Btn_Support'):obs=click(demo,obs,'Btn_JourneyHand',rows)
            obs=click(demo,obs,'Btn_Support',rows)
        if json.loads(state(obs)['audit.costFixture'])['uiOutsideSkill']!=action['Card']:obs=click(demo,obs,'Btn_OutsideSkill_'+str(slot),rows)
        obs=click(demo,obs,'Btn_UseOutsideSkill',rows)
    else:
        if not any(c['name'].startswith('Btn_Skill_') and c['clickable'] for c in obs['controls']):obs=click(demo,obs,'Btn_Skills',rows)
        name='Btn_Skill_'+str(slot)
        for _ in range(len(native['skills'])):
            if available(obs,name):break
            obs=click(demo,obs,'Btn_NextSkill',rows)
        if json.loads(state(obs)['audit.costFixture'])['uiSkill']!=action['Card']:obs=click(demo,obs,name,rows)
        obs=click(demo,obs,'Btn_UseSkill',rows)
    choices=json.loads(state(obs)['audit.costFixture'])['cardChoices']
    physical=action['Parts'];host=choices[physical]
    if host['type']=='Wound':raise ValueError('This original skill cannot sideways this wound')
    eligible=[x['index'] for x in choices if x['type']!='Wound'];index=eligible.index(physical)
    for _ in range(index//6):obs=click(demo,obs,'Btn_Next',rows)
    obs=click(demo,obs,'Btn_Card_'+str(index),rows)
    if outside:
        if action['Choice'] not in ('Move','Influence'):raise ValueError('Outside sideways type lacks an original UI control')
        if action['Choice']=='Influence':obs=click(demo,obs,'Btn_Influence',rows)
    elif action['Choice']!=('Block' if native['phase']=='Block' else 'Attack'):raise ValueError('Original battle phase mismatches sideways direction')
    if action['Card']=='hero_skill_pool_005:07' and action['Option']!=0:obs=click(demo,obs,'Btn_Color_'+str(action['Option']),rows)
    if action['Card']=='hero_skill_pool_007:07' and action['Option']==1:obs=click(demo,obs,'Btn_Value3',rows)
    return click(demo,obs,'Btn_Confirm',rows)

def conversion(demo,obs,action,rows):
    expected_card='advanced_card_018' if action['Kind']=='convertInfluence' else 'advanced_card_006'
    if action.get('Card') not in ('',expected_card) or action.get('Input') or action.get('Targets') or action.get('Parts'):raise ValueError('Unbound conversion parameters')
    if not available(obs,'Btn_CloseConversions'):obs=click(demo,obs,'Btn_Conversions',rows)
    prefix='Btn_InfluenceBlock' if action['Kind']=='convertInfluence' else 'Btn_MoveRanged' if action['Choice']=='ranged' else 'Btn_MoveAttack'
    controls=available(obs,prefix+'All')
    if not controls:raise ValueError('Actual conversion unavailable')
    numbers=re.findall(r'\d+',controls[0]['text']);full=int(numbers[-1])
    amount=action['Option'] if action['Option']>0 else full
    if amount==full:return click(demo,obs,prefix+'All',rows)
    if amount==1:return click(demo,obs,prefix+'One',rows)
    raise ValueError('Exact atomic conversion amount has no UI control; UI offers one or all')

def heal(demo,obs,action,rows):
    if action.get('Card') or action.get('Choice') or action.get('Input') or action.get('Parts') or action.get('Option'):raise ValueError('Unbound healing parameters')
    native=json.loads(state(obs)['audit.costNativeState']);target=action['Targets']
    if native['phase']=='Outside':
        if not available(obs,'Btn_CloseHand'):obs=click(demo,obs,'Btn_JourneyHand',rows)
        if target<0:return click(demo,obs,'Btn_HealHero',rows)
        selected=[c['name'] for c in obs['controls'] if c['name'].startswith('Btn_RecoveryUnit_') and c['clickable'] and '●' in c['text']]
        wanted='Btn_RecoveryUnit_'+str(target)
        for name in selected:
            if name!=wanted:obs=click(demo,obs,name,rows)
        if wanted not in selected:obs=click(demo,obs,wanted,rows)
        return click(demo,obs,'Btn_HealUnit',rows)
    if not available(obs,'Btn_UnitSupportTarget'):obs=click(demo,obs,'Btn_Recovery',rows)
    for _ in range(len(native['units'])+1):
        if json.loads(state(obs)['audit.costFixture'])['uiSupportTarget']==target:break
        obs=click(demo,obs,'Btn_UnitSupportTarget',rows)
    return click(demo,obs,'Btn_HealWound',rows)

def rest(demo,obs,action,rows):
    if action.get('Input') or action.get('Choice') or any(action.get(k,0) for k in ('Targets','Parts','Option')):raise ValueError('Unbound rest parameters')
    native=json.loads(state(obs)['audit.costNativeState'])
    if native['phase']!='Outside':raise ValueError('Rest requires actual outside phase')
    if available(obs,'Btn_BackSupport'):obs=click(demo,obs,'Btn_BackSupport',rows)
    if not available(obs,'Btn_Support'):obs=click(demo,obs,'Btn_JourneyHand',rows)
    obs=click(demo,obs,'Btn_Support',rows)
    types=json.loads(state(obs)['audit.costFixture'])['cardChoices']
    slots=[native['hand'].index(action['Card'])] if action['Card'] else []
    wounds=[x['index'] for x in types if x['type']=='Wound']
    slots+=wounds if action['Card'] else wounds[:1]
    for slot in slots:
        name='Btn_RestCard_'+str(slot)
        for _ in range(32):
            if available(obs,name):break
            visible=[int(c['name'][13:]) for c in obs['controls'] if c['name'].startswith('Btn_RestCard_') and c['clickable']]
            if not visible:raise ValueError('No actual rest card slots')
            obs=click(demo,obs,'Btn_RestNext' if slot>max(visible) else 'Btn_RestPrevious',rows)
        obs=click(demo,obs,name,rows)
    return click(demo,obs,'Btn_Rest',rows)

def contribution(demo,obs,action,rows):
    if action.get('Input') or action.get('Choice') or action.get('Targets') or action.get('Option'):raise ValueError('Unbound contribution parameters')
    native=json.loads(state(obs)['audit.costNativeState'])
    soul=action['Kind']=='soulGuideBlock';skill='hero_skill_pool_003:00' if soul else action['Card']
    slot=native['skills'].index(skill)
    if not any(c['name'].startswith('Btn_Skill_') and c['clickable'] for c in obs['controls']):obs=click(demo,obs,'Btn_Skills',rows)
    name='Btn_Skill_'+str(slot)
    for _ in range(len(native['skills'])):
        if available(obs,name):break
        obs=click(demo,obs,'Btn_NextSkill',rows)
    if json.loads(state(obs)['audit.costFixture'])['uiSkill']!=skill:obs=click(demo,obs,name,rows)
    obs=click(demo,obs,'Btn_SoulContribution' if soul else 'Btn_UseSkill',rows)
    eligible=list(range(len(native['blocks']))) if soul else [i for i,a in enumerate(native['attacks']) if a['card'] and not a['unit']]
    index=eligible.index(action['Parts'])
    for _ in range(index//4):obs=click(demo,obs,'Btn_Next',rows)
    obs=click(demo,obs,'Btn_Part_'+str(index),rows)
    return click(demo,obs,'Btn_Confirm',rows)

def defend(demo,obs,action,rows):
    if action.get('Card') or action.get('Choice') or action.get('Input'):raise ValueError('Unbound defense selection parameters')
    native=json.loads(state(obs)['audit.costNativeState'])
    wanted={i for i in range(len(native['enemies'])) if action['Targets']&(1<<i)}
    if any('/EnemyDefend/' in c['path'] and c['clickable'] for c in obs['controls']) and set(json.loads(state(obs)['audit.costFixture'])['uiTargets'])!=wanted:
        obs=click(demo,obs,'Btn_Cancel',rows)
    if not any('/EnemyDefend/' in c['path'] and c['clickable'] for c in obs['controls']):
        audit=json.loads(state(obs)['audit.costFixture'])
        if audit.get('uiUnit') is not None:obs=click(demo,obs,'Btn_Roster',rows)
        for i in sorted(set(json.loads(state(obs)['audit.costFixture'])['uiTargets'])^wanted):
            obs=click(demo,obs,'Btn_Target',rows,'/Enemy_'+str(i)+'/')
        obs=click(demo,obs,'Btn_Resolve',rows)
    if not any('/EnemyDefend/' in c['path'] and c['clickable'] for c in obs['controls']):
        raise ValueError('Native defense preview has no current UI selection panel')
    # Native preview binds an actual source enemy to a target enemy. It does
    # not consume attack parts; final attack confirmation is a separate intent.
    obs=click(demo,obs,'Btn_Source_'+str(action['Parts']),rows)
    return click(demo,obs,'Btn_Target_'+str(action['Option']),rows)

def reward_choice(demo,obs,action,rows):
    if action.get('Card') or action.get('Choice') or action.get('Input') or action.get('Parts'):raise ValueError('Unbound reward parameters')
    if action['Kind']=='explosiveArmor' and action['Targets']<0:return click(demo,obs,'Btn_SkipExplosiveArmor',rows)
    target=action['Targets']
    if set(json.loads(state(obs)['audit.costFixture'])['uiTargets'])!={target}:
        obs=click(demo,obs,'Btn_Target',rows,'/Enemy_'+str(target)+'/')
    if action['Kind']=='explosiveArmor':return click(demo,obs,'Btn_ExplosiveArmor',rows)
    color=('Red','Blue','Green','White','Gold','Black')[action['Option']]
    for _ in range(7):
        native=json.loads(state(obs)['audit.costNativeState'])
        if native['killCrystalChoices'].get(str(target))==color:return obs
        obs=click(demo,obs,'Btn_KillCrystal',rows)
    raise ValueError('Actual kill-reward cycle did not reach requested original color')

def bow_choice(demo,obs,action,rows):
    if action.get('Card') or action.get('Choice') or action.get('Input') or action.get('Parts'):raise ValueError('Unbound bow choice parameters')
    native=json.loads(state(obs)['audit.costNativeState'])
    part=native['attacks'][action['Targets']]
    indices=[i for i,a in enumerate(native['attacks']) if a['type']!='Melee']
    slot=indices.index(action['Targets'])
    if not available(obs,'Btn_BowContribution'):obs=click(demo,obs,'Btn_BowChoices',rows)
    for _ in range(len(indices)):
        control=available(obs,'Btn_BowContribution')[0]
        match=re.search(r'第(\d+)/(\d+)笔',control['text'])
        if not match:raise ValueError('Original bow source page indicator missing')
        if int(match[1])-1==slot:break
        obs=click(demo,obs,'Btn_BowContribution',rows)
    if action['Option']==1:return click(demo,obs,'Btn_BowDouble',rows)
    if action['Option']==(0 if part['type']=='Siege' else 2):return click(demo,obs,'Btn_BowSiege',rows)
    raise ValueError('Original bow retention mode has no actual current UI control')

def artifact_hand_slot(native,physical_card):
    if physical_card=='items_024' and native.get('mysticBox'):
        identity=native['mysticBox']['physical']
        return native['handOrder'].index(identity) if identity in native['handOrder'] else -1
    return native['hand'].index(physical_card)


def artifact(demo,obs,action,rows,physical_card=None,selected=False):
    card=action['Card'];burn=action['Choice']=='burn'
    physical_card=physical_card or card
    if card=='items_013':return wisdom_artifact(demo,obs,action,rows,physical_card=physical_card)
    if physical_card!=card and card in ('items_013','items_021','items_023','items_017','items_022','items_024'):
        raise ValueError('Borrowed artifact needs its original material/supply route')
    if card=='items_021':return staff_artifact(demo,obs,action,rows)
    if card=='items_023':return tome_artifact(demo,obs,action,rows)
    if card=='items_017':return free_unit_artifact(demo,obs,action,rows)
    if card in ('items_013','items_021','items_023','items_017','items_022','items_024'):
        raise ValueError('Artifact requires its explicit original material/supply panel')
    fields=dict(part.split('=',1) for part in action.get('Input','').split(';') if part)
    if fields.keys()-{'colors'}:raise ValueError('Unbound artifact input fields')
    native=json.loads(state(obs)['audit.costNativeState']);outside=native['phase']=='Outside'
    if action['Parts'] and not (card=='items_001' and burn) and not (card in ('items_008','items_016') and not burn):raise ValueError('Artifact physical material mask needs explicit UI mapping')
    if not selected:obs=select_card(demo,obs,physical_card,rows)
    mode='Btn_ArtifactBurn' if burn else 'Btn_ArtifactNormal'
    if selected:
        control=available(obs,mode)
        if not control:raise ValueError('Borrowed artifact mode unavailable')
        if '●' not in control[0]['text']:obs=click(demo,obs,mode,rows)
    elif burn:obs=click(demo,obs,mode,rows)
    palette=('Red','Blue','Green','White','Gold','Black');defaults=('Red','Blue','White')
    for i,color in enumerate(fields.get('colors','').split(',') if fields.get('colors') else []):
        limit=4 if card=='items_011' else 6
        for _ in range((palette.index(color)-palette.index(defaults[i]))%limit):
            obs=click(demo,obs,'Btn_ArtifactColor_'+str(i),rows)
    if card in ('items_000','items_001','items_002','items_003','items_018') and not burn:
        if outside:obs=click(demo,obs,'Btn_RecoveryUnit_'+str(action['Targets']),rows)
        else:
            for _ in range(action['Targets']):obs=click(demo,obs,'Btn_ArtifactUnit',rows)
    if card=='items_009' and burn:
        for _ in range(action['Option']):obs=click(demo,obs,'Btn_HornBonus',rows)
    elif card=='items_020' and action['Option']==1:obs=click(demo,obs,'Btn_ShieldMode',rows)
    elif action['Option']:raise ValueError('Artifact option has no bound original control')
    if card=='items_001' and burn:
        for i in range(len(native['enemies'])):
            if action['Parts']&(1<<i):obs=click(demo,obs,'Btn_Target',rows,'/Enemy_'+str(i)+'/')
    if card in ('items_008','items_016') and not burn and action['Parts']:
        if outside:raise ValueError('Original outside artifact material panel is not exposed')
        host=artifact_hand_slot(native,physical_card);slots=[i for i in range(len(native['hand'])) if i!=host][:8]
        obs=click(demo,obs,'Btn_ArtifactMaterials',rows)
        for bit,slot in enumerate(slots):
            if action['Parts']&(1<<bit):obs=select_card(demo,obs,native['hand'][slot],rows,physical_slot=slot)
        obs=click(demo,obs,'Btn_ArtifactMaterials',rows)
    return click(demo,obs,'Btn_PlayOutside' if outside else 'Btn_ArtifactPlay',rows)


def box_use(demo,obs,action,rows):
    native=json.loads(state(obs)['audit.costNativeState']);box=native.get('mysticBox')
    if not box or box['used'] or box['revealed']!=action['Card']:raise ValueError('Actual unused box reveal must match the original borrowed artifact')
    if action['Choice'] not in ('normal','burn'):raise ValueError('Unbound box mode')
    if not available(obs,'Btn_CloseSpecial'):
        if not available(obs,'Btn_PendingBox') and available(obs,'Btn_JourneyHand'):obs=click(demo,obs,'Btn_JourneyHand',rows)
        obs=click(demo,obs,'Btn_PendingBox',rows)
    mode='Btn_SpecialBurn' if action['Choice']=='burn' else 'Btn_SpecialNormal'
    if '●' not in available(obs,mode)[0]['text']:obs=click(demo,obs,mode,rows)
    card=action['Card'];burn=action['Choice']=='burn'
    if card in ('items_013','items_021','items_023'):
        obs=click(demo,obs,'Btn_ConfirmSpecial',rows)
        route=wisdom_artifact if card=='items_013' else staff_artifact if card=='items_021' else tome_artifact
        return route(demo,obs,action,rows,physical_card='items_024',opened=True)
    if card=='items_017':
        fields=dict(p.split('=',1) for p in action.get('Input','').split(';') if p)
        if fields.keys()-{'offer'} or action['Parts'] or action['Option'] or bool(fields)!=burn:
            raise ValueError('Unbound original borrowed free-unit parameters')
        if burn:
            offer=native['unitOffer'].index(fields['offer'])
            for _ in range(offer//3):obs=click(demo,obs,'Btn_FreeOfferNext',rows)
            obs=click(demo,obs,'Btn_FreeOffer_'+str(offer),rows)
            if 0<=action['Targets']<len(native['units']):obs=click(demo,obs,'Btn_SpecialUnit_'+str(action['Targets']),rows)
        return click(demo,obs,'Btn_ConfirmSpecial',rows)
    if card in ('items_013','items_021','items_023','items_017','items_022','items_024'):
        raise ValueError('Borrowed artifact needs its original material/supply route')
    battle=native['phase']!='Outside'
    if battle and (card in ('items_008','items_009','items_016','items_019','items_020') or card=='items_001' and burn):
        obs=click(demo,obs,'Btn_ConfirmSpecial',rows)
        return artifact(demo,obs,action,rows,physical_card='items_024',selected=True)
    fields=dict(p.split('=',1) for p in action.get('Input','').split(';') if p)
    if fields.keys()-{'colors'} or action['Parts'] or action['Option']:raise ValueError('Unbound special box parameters')
    labels={'Red':'红色魔力','Blue':'蓝色魔力','Green':'绿色魔力','White':'白色魔力','Gold':'金色魔力','Black':'黑色魔力'}
    for i,color in enumerate(fields.get('colors','').split(',') if fields.get('colors') else []):
        name='Btn_ArtifactColor_'+str(i)
        for _ in range(7):
            controls=available(obs,name)
            if not controls:raise ValueError('Actual borrowed artifact color missing')
            if labels[color] in controls[0]['text']:break
            obs=click(demo,obs,name,rows)
        else:raise ValueError('Borrowed artifact color cycle failed')
    if card in ('items_000','items_001','items_002','items_003','items_018') and not burn:
        obs=click(demo,obs,'Btn_SpecialUnit_'+str(action['Targets']),rows)
    return click(demo,obs,'Btn_ConfirmSpecial',rows)


def wisdom_artifact(demo,obs,action,rows,physical_card=None,opened=False):
    fields=dict(p.split('=',1) for p in action['Input'].split(';') if p)
    burn=action['Choice']=='burn'
    if action['Choice'] not in ('normal','burn') or fields.keys()!={'materialSlot','spells' if burn else 'advanced'} or any(action[k] for k in ('Targets','Parts','Option')):
        raise ValueError('Unbound original wisdom parameters')
    native=json.loads(state(obs)['audit.costNativeState']);physical_card=physical_card or action['Card']
    if not opened:
        obs=select_card(demo,obs,physical_card,rows)
        obs=click(demo,obs,'Btn_PlayOutside' if native['phase']=='Outside' else 'Btn_OpenMaterialArtifact',rows)
    mode='Btn_Burn' if burn else 'Btn_Normal'
    if '●' not in available(obs,mode)[0]['text']:obs=click(demo,obs,mode,rows)
    host=artifact_hand_slot(native,physical_card);others=[i for i in range(len(native['hand'])) if i!=host]
    physical=others[int(fields['materialSlot'])]
    bindings=json.loads(state(obs)['audit.costFixture'])['cardChoices']
    materials=[x['index'] for x in bindings if x['index']!=host and x['type']=='Action'];slot=materials.index(physical)
    for _ in range(slot//4):obs=click(demo,obs,'Btn_MaterialNext',rows)
    obs=click(demo,obs,'Btn_Material_'+str(slot),rows)
    offer=native['spellOffer' if burn else 'advancedOffer'].index(fields['spells' if burn else 'advanced'])
    obs=click(demo,obs,'Btn_ArtifactOffer_'+str(offer),rows)
    return click(demo,obs,'Btn_ConfirmMaterial',rows)


def learning_purchase(demo,obs,action,rows):
    if action['Choice'] or action['Input'] or action['Targets'] or action['Parts'] or action['Option'] not in (0,1):
        raise ValueError('Unbound original learning parameters')
    native=json.loads(state(obs)['audit.costNativeState'])
    if native['phase']=='Outside':
        if not available(obs,'Btn_CloseHand'):obs=click(demo,obs,'Btn_JourneyHand',rows)
    elif not available(obs,'Btn_LearningSupply'):
        obs=click(demo,obs,'Btn_Roster',rows)
    obs=click(demo,obs,'Btn_LearningSupply',rows)
    mode='Btn_LearnStrong' if action['Option']==1 else 'Btn_LearnBase'
    if '●' not in available(obs,mode)[0]['text']:obs=click(demo,obs,mode,rows)
    obs=click(demo,obs,'Btn_InputOffer_'+str(native['advancedOffer'].index(action['Card'])),rows)
    return click(demo,obs,'Btn_BuyLearning',rows)


def free_unit_artifact(demo,obs,action,rows):
    fields=dict(p.split('=',1) for p in action.get('Input','').split(';') if p)
    burn=action['Choice']=='burn'
    if fields.keys()-{'offer'} or action['Parts'] or action['Option']:
        raise ValueError('Unbound original free unit artifact parameters')
    if bool(fields)!=burn:raise ValueError('Original free unit offer is required only for burn')
    native=json.loads(state(obs)['audit.costNativeState'])
    obs=select_card(demo,obs,action['Card'],rows)
    obs=click(demo,obs,'Btn_PlayOutside' if native['phase']=='Outside' else 'Btn_OpenSpecialArtifact',rows)
    if burn:
        obs=click(demo,obs,'Btn_SpecialBurn',rows)
        offer=native['unitOffer'].index(fields['offer'])
        obs=click(demo,obs,'Btn_FreeOffer_'+str(offer),rows)
        if 0<=action['Targets']<len(native['units']):
            obs=click(demo,obs,'Btn_SpecialUnit_'+str(action['Targets']),rows)
    return click(demo,obs,'Btn_ConfirmSpecial',rows)

def staff_artifact(demo,obs,action,rows,physical_card=None,opened=False):
    fields=dict(p.split('=',1) for p in action.get('Input','').split(';') if p)
    if fields.keys()-{'materialSlot','first','second','color'}:raise ValueError('Unbound original staff fields')
    native=json.loads(state(obs)['audit.costNativeState']);outside=native['phase']=='Outside';burn=action['Choice']=='burn'
    physical_card=physical_card or action['Card']
    if not opened:
        obs=select_card(demo,obs,physical_card,rows)
        obs=click(demo,obs,'Btn_PlayOutside' if outside else 'Btn_OpenMaterialArtifact',rows)
    mode='Btn_Burn' if burn else 'Btn_Normal'
    if '●' not in available(obs,mode)[0]['text']:obs=click(demo,obs,mode,rows)
    first=int(fields['first']);second=int(fields['second'])
    if not burn:
        host=artifact_hand_slot(native,physical_card);others=[i for i in range(len(native['hand'])) if i!=host]
        physical=others[int(fields['materialSlot'])]
        bindings=json.loads(state(obs)['audit.costFixture'])['cardChoices']
        materials=[x['index'] for x in bindings if x['index']!=host and x['type'] in ('Action','Spell')]
        slot=materials.index(physical)
        for _ in range(slot//4):obs=click(demo,obs,'Btn_MaterialNext',rows)
        obs=click(demo,obs,'Btn_Material_'+str(slot),rows)
    if first!=1:obs=click(demo,obs,'Btn_StaffFirst_'+str(first),rows)
    if burn and second!=3:obs=click(demo,obs,'Btn_StaffSecond_'+str(second),rows)
    if first==1 or burn and second==1:
        color=('Red','Blue','Green','White','Gold','Black')[int(fields['color'])]
        if color!='Red':obs=click(demo,obs,'Btn_StaffCrystal_'+color,rows)
    if first==2 or burn and second==2:obs=click(demo,obs,'Btn_MaterialUnit_'+str(action['Targets']),rows)
    # Native task definition provides no path, so the native default is an
    # empty staff path. The UI initial state is the same empty path.
    return click(demo,obs,'Btn_ConfirmMaterial',rows)

def tome_artifact(demo,obs,action,rows,physical_card=None,opened=False):
    fields=dict(p.split('=',1) for p in action.get('Input','').split(';') if p)
    if fields.keys()-{'materialSlot','spellId','spellChoice','colors','dice','crystal','offeredUnit','disband'}:raise ValueError('Unbound original tome fields')
    if ('offeredUnit' in fields or 'disband' in fields) and (fields.get('spellId')!='magic_020' or fields.get('spellChoice')!='strong'):
        raise ValueError('Tome unit recruitment input requires original strong Honor Summons')
    native=json.loads(state(obs)['audit.costNativeState']);outside=native['phase']=='Outside'
    physical_card=physical_card or action['Card']
    if not opened:
        obs=select_card(demo,obs,physical_card,rows)
        obs=click(demo,obs,'Btn_PlayOutside' if outside else 'Btn_OpenMaterialArtifact',rows)
    mode='Btn_Burn' if action['Choice']=='burn' else 'Btn_Normal'
    if '●' not in available(obs,mode)[0]['text']:obs=click(demo,obs,mode,rows)
    host=artifact_hand_slot(native,physical_card);others=[i for i in range(len(native['hand'])) if i!=host]
    physical=others[int(fields['materialSlot'])]
    bindings=json.loads(state(obs)['audit.costFixture'])['cardChoices']
    materials=[x['index'] for x in bindings if x['index']!=host and x['type'] in ('Action','Spell')]
    slot=materials.index(physical)
    for _ in range(slot//4):obs=click(demo,obs,'Btn_MaterialNext',rows)
    obs=click(demo,obs,'Btn_Material_'+str(slot),rows)
    offer=native['spellOffer'].index(fields['spellId']);obs=click(demo,obs,'Btn_ArtifactOffer_'+str(offer),rows)
    obs=click(demo,obs,'Btn_TomeChoice_'+fields['spellChoice'],rows)
    if available(obs,'Btn_MaterialEnemy_'+str(action['Targets'])):obs=click(demo,obs,'Btn_MaterialEnemy_'+str(action['Targets']),rows)
    elif available(obs,'Btn_MaterialUnit_'+str(action['Parts'])):obs=click(demo,obs,'Btn_MaterialUnit_'+str(action['Parts']),rows)
    obs=click(demo,obs,'Btn_ConfirmMaterial',rows)
    if available(obs,'Btn_ConfirmInputs'):
        for i,color in enumerate(fields.get('colors','').split(',') if fields.get('colors') else []):
            control=available(obs,f'Btn_InputColor_{i}_{color}')
            if not control:raise ValueError('Original tome input color unavailable')
            if '●' not in control[0]['text']:obs=click(demo,obs,f'Btn_InputColor_{i}_{color}',rows)
        used=set()
        for color in fields.get('dice','').split(',') if fields.get('dice') else []:
            index=next(i for i,c in enumerate(native['auditSourceOrder']) if c==color and i not in used);used.add(index)
            obs=click(demo,obs,'Btn_InputDie_'+str(index),rows)
        if fields.get('crystal'):obs=click(demo,obs,'Btn_InputCrystal_'+fields['crystal'],rows)
        if fields.get('offeredUnit'):
            offer=native['unitOffer'].index(fields['offeredUnit'])
            for _ in range(offer//3):obs=click(demo,obs,'Btn_HonorNext',rows)
            obs=click(demo,obs,'Btn_HonorOffer_'+str(offer),rows)
        if int(fields.get('disband','-1'))>=0:obs=click(demo,obs,'Btn_HonorDisband_'+fields['disband'],rows)
        obs=click(demo,obs,'Btn_ConfirmInputs',rows)
    return obs

def payment_action(demo,obs,action,rows):
    native=json.loads(state(obs)['audit.costNativeState'])
    audit=json.loads(state(obs)['audit.costFixture'])
    consumer=audit.get('uiPaymentConsumer') or getattr(demo,'payment_consumer',None)
    # A play may have consumed this physical card before B opens a payment
    # picker. Bind B to an actual remaining consumer, never the spent card.
    if consumer and consumer['Kind']=='play' and consumer['Card'] not in native['hand']:
        consumer=None
    if not consumer:
        excluded=action['Parts'] if action['Kind']=='payment' and action['Choice']=='material' else -1
        eligible={c['index'] for c in audit['cardChoices'] if c['type'] not in ('Artifact','Wound')}
        slot=next((i for i in range(len(native['hand'])) if i!=excluded and i in eligible),None)
        if slot is not None:consumer=dict(Kind='play',Card=native['hand'][slot])
        else:
            unit=next((u for u in native['units'] if not u['destroyed']),None)
            if unit is None:raise ValueError('No actual eligible payment consumer in hand or units')
            consumer=dict(Kind='unit',Card=unit['id'])
    slot=-1
    if consumer['Kind']=='play':
        slot=native['hand'].index(consumer['Card'])
        obs=select_card(demo,obs,consumer['Card'],rows,physical_slot=slot)
    elif consumer['Kind']=='unit':
        binding=next(x for x in audit['unitAbilities'] if x['id']==consumer['Card'])
        unit_slot=binding['index']
        if native['phase']=='Outside':
            if not any('/OwnedUnits/' in c['path'] for c in obs['controls']):
                if not available(obs,'Btn_OwnedUnits'):obs=click(demo,obs,'Btn_JourneyHand',rows)
                obs=click(demo,obs,'Btn_OwnedUnits',rows)
            for _ in range(32):
                if available(obs,'Btn_Unit_'+str(unit_slot)):break
                visible=[int(c['name'][9:]) for c in obs['controls'] if c['name'].startswith('Btn_Unit_') and c['clickable']]
                if not visible:raise ValueError('No actual payment consumer unit slots')
                obs=click(demo,obs,'Btn_Next' if unit_slot>max(visible) else 'Btn_Previous',rows)
        elif not available(obs,'Btn_Unit_'+str(unit_slot)):obs=click(demo,obs,'Btn_Roster',rows)
        obs=click(demo,obs,'Btn_Unit_'+str(unit_slot),rows)
    else:raise ValueError('Unknown payment consumer kind')
    obs=click(demo,obs,'Btn_ManaPayment',rows)
    if action['Kind']=='cancelPayment':return click(demo,obs,'Btn_Cancel',rows,'/ManaPayment/')
    if action.get('Input') or action.get('Option'):raise ValueError('Payment carries unbound input')
    skills=[s for s in audit['skills'] if s in ('hero_skill_pool_000:06','hero_skill_pool_000:07')]
    obs=click(demo,obs,'Btn_Skill_'+str(skills.index(action['Card'])),rows,'/ManaPayment/')
    colors=('Red','Blue','Green','White','Gold','Black')
    obs=click(demo,obs,'Btn_Required_'+colors[action['Targets']],rows)
    if action['Choice']=='material':
        material=action['Parts']
        if material==slot:raise ValueError('Payment material equals actual consumer')
        index=material-(slot>=0 and material>slot)
        for _ in range(index//6):obs=click(demo,obs,'Btn_Next',rows,'/ManaPayment/')
        obs=click(demo,obs,'Btn_Material_'+str(index),rows)
    elif action['Choice'] in ('stock','die','extra'):
        source=colors[action['Parts']]
        if action['Choice']=='stock':obs=click(demo,obs,'Btn_Source_'+source,rows)
        else:
            die=native['auditSourceOrder'].index(source)
            obs=click(demo,obs,'Btn_Die_'+str(die),rows)
            if action['Choice']=='extra':obs=click(demo,obs,'Btn_ExtraDie',rows)
    else:raise ValueError('Unknown payment choice')
    return click(demo,obs,'Btn_Select',rows,'/ManaPayment/')


def turn_action(demo,obs,action,rows):
    if any(action.get(k) for k in ('Card','Choice','Input','Targets','Parts','Option')):
        raise ValueError('Turn boundary carries unbound parameters')
    native=json.loads(state(obs)['audit.costNativeState'])
    if action['Kind']=='nextTurn':
        ordinary=[c['index'] for c in json.loads(state(obs)['audit.costFixture'])['cardChoices'] if c['type']!='Wound']
        if ordinary:
            if not available(obs,'Btn_DiscardAtEnd'):
                obs=click(demo,obs,'Btn_JourneyHand',rows)
            for slot in ordinary:
                chosen=json.loads(state(obs)['audit.costFixture'])['turnFlow']['discardSlots']
                if slot in chosen:continue
                for _ in range(32):
                    if available(obs,'Btn_Hand_'+str(slot)):break
                    visible=[int(c['name'][9:]) for c in obs['controls'] if c['name'].startswith('Btn_Hand_') and c['clickable']]
                    if not visible:raise ValueError('No actual turn-end hand slots')
                    obs=click(demo,obs,'Btn_NextHand' if slot>max(visible) else 'Btn_PreviousHand',rows)
                obs=click(demo,obs,'Btn_Hand_'+str(slot),rows)
                obs=click(demo,obs,'Btn_DiscardAtEnd',rows)
            obs=click(demo,obs,'Btn_CloseHand',rows)
        if available(obs,'Btn_CloseBattleMenu'):obs=click(demo,obs,'Btn_CloseBattleMenu',rows)
        obs=click(demo,obs,'Btn_EndTurn',rows)
    elif action['Kind']=='roundFinish':
        if not native['roundWindow']:raise ValueError('No actual round-end banner window')
        obs=click(demo,obs,'Btn_EndRound',rows)
    for _ in range(128):
        native=json.loads(state(obs)['audit.costNativeState'])
        flow=json.loads(state(obs)['audit.costFixture'])['turnFlow']
        if available(obs,'Btn_ChaosKeep'):
            obs=click(demo,obs,'Btn_ChaosKeep',rows);continue
        if flow['done']:return obs
        if any(c['name']=='Btn_ConfirmReward' for c in obs['controls']):
            choices=sorted(flow['rewardChoices']);seed=flow['progressionSeed']+native['turns']
            skill=choices[seed%len(choices)];index=flow['rewardUiSkills'].index(skill)
            obs=click(demo,obs,'Btn_RewardSkill_'+str(index),rows)
            obs=click(demo,obs,'Btn_RewardAction_'+str(seed%len(flow['rewardActions'])),rows)
            obs=click(demo,obs,'Btn_ConfirmReward',rows)
        elif native['roundWindow'] and action['Kind']=='nextTurn':return obs
        elif flow['roundEnded'] and available(obs,'Btn_EndRound'):obs=click(demo,obs,'Btn_EndRound',rows)
        elif available(obs,'Btn_ReleaseFamiliar'):
            color=next((c for c in ('Red','Blue','Green','White') if native['crystals'].get(c,0)>0),None)
            obs=click(demo,obs,'Btn_Upkeep_'+color if color else 'Btn_ReleaseFamiliar',rows)
        elif available(obs,'Btn_PrepareRound'):obs=click(demo,obs,'Btn_PrepareRound',rows)
        elif available(obs,'Btn_Tactic_night_1'):obs=click(demo,obs,'Btn_Tactic_night_1',rows)
        elif flow['active']:return obs
        elif flow['canAnnounce']:obs=click(demo,obs,'Btn_Announce',rows)
        elif available(obs,'Btn_BeginTurn'):obs=click(demo,obs,'Btn_BeginTurn',rows)
        else:raise ValueError('Unmapped actual turn boundary obligation')
    raise ValueError('Turn boundary exceeded actual control path limit')


def apply(demo,obs,action,rows):
    if action['Kind']!='worldMove' and available(obs,'Btn_CloseMap'):
        obs=click(demo,obs,'Btn_CloseMap',rows)
    if action['Kind'] not in ('worldInteract','worldInteractEnd') and available(obs,'Btn_CloseInteraction'):
        obs=click(demo,obs,'Btn_CloseInteraction',rows)
    if action['Kind'] not in SHARED_KINDS|{'cumbersome'} and any('/SharedSkills/' in c['path'] and c['clickable'] for c in obs['controls']):
        obs=click(demo,obs,'Btn_Back',rows)
    if action['Kind'] not in ('boxReveal','boxUse') and available(obs,'Btn_CloseSpecial'):
        obs=click(demo,obs,'Btn_CloseSpecial',rows)
    if action['Kind']!='bowChoice' and available(obs,'Btn_BowContribution'):
        obs=click(demo,obs,'Btn_BowChoices',rows)
    if action['Kind']=='advance' and any('/EnemyDefend/' in c['path'] and c['clickable'] for c in obs['controls']):
        native=json.loads(state(obs)['audit.costNativeState'])
        if native['phase'] not in ('Melee','AssignDamage'):raise ValueError('Defense preview phase handoff preservation is not mapped for this phase')
        mask=sum(1<<i for i in json.loads(state(obs)['audit.costFixture'])['uiTargets'])
        indices={e['instance']:i for i,e in enumerate(native['enemies'])}
        selected=[(indices[s['source']],indices[s['target']]) for s in native['defenderSelected']]
        obs=click(demo,obs,'Btn_Cancel',rows)
        obs=click(demo,obs,'Btn_Next',rows)
        if native['phase']=='AssignDamage' and selected:
            for source,target in selected:
                obs=defend(demo,obs,dict(Kind='defend',Card='',Choice='',Input='',Targets=mask,Parts=source,Option=target),rows)
            if json.loads(state(obs)['audit.costNativeState'])['defenderSelected']!=native['defenderSelected']:
                raise ValueError('Actual phase handoff reconstruction changed the preserved defense assignment')
        return obs
    if action['Kind'] in SHARED_KINDS|{'play','sideways','source','extraSource','unit','unitLeadership','offeredUnit','skill','skillSideways','puppetKeep','puppetUse','convertMovement','convertInfluence','bannerUse'} and any('/EnemyDefend/' in c['path'] and c['clickable'] for c in obs['controls']):
        before=json.loads(state(obs)['audit.costNativeState'])
        targets=set(json.loads(state(obs)['audit.costFixture'])['uiTargets'])
        mask=sum(1<<i for i in targets)
        indices={e['instance']:i for i,e in enumerate(before['enemies'])}
        selected=[(indices[s['source']],indices[s['target']],s['value']) for s in before['defenderSelected']]
        obs=click(demo,obs,'Btn_Cancel',rows)
        obs=apply(demo,obs,action,rows)
        if selected:
            if any('/SharedSkills/' in c['path'] and c['clickable'] for c in obs['controls']):obs=click(demo,obs,'Btn_Back',rows)
            if available(obs,'Btn_CloseTools'):obs=click(demo,obs,'Btn_CloseTools',rows)
            if available(obs,'Btn_CloseConversions'):obs=click(demo,obs,'Btn_CloseConversions',rows)
            if json.loads(state(obs)['audit.costFixture']).get('uiUnit') is not None:obs=click(demo,obs,'Btn_Roster',rows)
            if any('/Skills/' in c['path'] and c['clickable'] for c in obs['controls']):obs=click(demo,obs,'Btn_Skills',rows)
            for source,target,_ in selected:
                obs=defend(demo,obs,dict(Kind='defend',Card='',Choice='',Input='',Targets=mask,Parts=source,Option=target),rows)
            after=json.loads(state(obs)['audit.costNativeState'])
            if after['defenderSelected']!=before['defenderSelected']:
                raise ValueError('Actual UI reconstruction changed the preserved defense assignment')
        return obs
    if action['Kind']=='attack' and any('/EnemyDefend/' in c['path'] and c['clickable'] for c in obs['controls']):
        wanted={i for i in range(len(json.loads(state(obs)['audit.costNativeState'])['enemies'])) if action['Targets']&(1<<i)}
        if set(json.loads(state(obs)['audit.costFixture'])['uiTargets'])!=wanted:
            obs=click(demo,obs,'Btn_Cancel',rows)
            return combat_commit(demo,obs,action,rows)
        return click(demo,obs,'Btn_Confirm',rows)
    if action['Kind']=='clearDefend':
        if any(action.get(k) for k in ('Card','Choice','Input','Targets','Parts','Option')):raise ValueError('Defense clear carries unbound parameters')
        if not any('/EnemyDefend/' in c['path'] and c['clickable'] for c in obs['controls']):
            native=json.loads(state(obs)['audit.costNativeState'])
            if not native['defenderSelected'] and state(obs).get('enemies.defendSelected')=='':return obs
            raise ValueError('Native defense clear has no open UI preview')
        return click(demo,obs,'Btn_Cancel',rows)
    if action['Kind'] not in ('convertMovement','convertInfluence') and available(obs,'Btn_CloseConversions'):
        obs=click(demo,obs,'Btn_CloseConversions',rows)
    if action['Kind'] not in ('skill','skillSideways','rest') and available(obs,'Btn_BackSupport'):
        obs=click(demo,obs,'Btn_BackSupport',rows)
    if action['Kind'] not in ('skill','skillSideways','skillBullseye','soulGuideBlock','play') and any('/Skills/' in c['path'] and c['clickable'] for c in obs['controls']) and available(obs,'Btn_Skills'):
        obs=click(demo,obs,'Btn_Skills',rows)
    if action['Kind']!='unit' and any('/OwnedUnits/' in c['path'] for c in obs['controls']) and available(obs,'Btn_Close'):
        obs=click(demo,obs,'Btn_Close',rows)
    if action['Kind'] not in ('play','heal','worldFly','protectionCleanup','payment','cancelPayment') and available(obs,'Btn_CloseHand'):
        obs=click(demo,obs,'Btn_CloseHand',rows)
    if action['Kind'] not in SHARED_KINDS|{'source','extraSource','cumbersome'} and any(c['name']=='Btn_CloseTools' and c['clickable'] for c in obs['controls']):
        obs=click(demo,obs,'Btn_CloseTools',rows)
    if action['Kind'] in ('payment','cancelPayment'):return payment_action(demo,obs,action,rows)
    if action['Kind'] in ('nextTurn','roundFinish'):return turn_action(demo,obs,action,rows)
    if action['Kind']=='defend':return defend(demo,obs,action,rows)
    if action['Kind']=='repeat':return repeated_effect(demo,obs,action,rows)
    if action['Kind']=='skillFork':return fork_skill(demo,obs,action,rows)
    if action['Kind']=='skillShapeshift':return shapeshift_skill(demo,obs,action,rows)
    if action['Kind'] in ('puppetKeep','puppetUse'):return puppet_skill(demo,obs,action,rows)
    if action['Kind']=='offeredUnit':return offered_unit(demo,obs,action,rows)
    if action['Kind']=='unitLeadership':return leadership_unit(demo,obs,action,rows)
    if action['Kind']=='learn':return learning_purchase(demo,obs,action,rows)
    if action['Kind']=='boxUse':return box_use(demo,obs,action,rows)
    if action['Kind'] in ('protectionCleanup','roundReturn'):return cleanup_choice(demo,obs,action,rows)
    if action['Kind'] in WORLD_KINDS:return world_action(demo,obs,action,rows)
    if action['Kind']=='fight':
        if action.get('Card') or action.get('Choice') or action.get('Input') or any(action.get(k,0) for k in ('Targets','Parts','Option')):
            raise ValueError('Fight carries unbound parameters')
        native=json.loads(state(obs)['audit.costNativeState'])
        if native['phase']!='Outside':raise ValueError('Fight requires an actual outside-battle turn')
        index=native['taskProgress']['encounterIndex']
        return click(demo,obs,'Btn_JourneyEncounter_'+str(index),rows)
    if action['Kind'] in SHARED_KINDS:return shared_action(demo,obs,action,rows)
    if action['Kind']=='cumbersome':
        if action.get('Card') or action.get('Choice') or action.get('Input') or action.get('Parts'):
            raise ValueError('Cumbersome carries unbound parameters')
        newly_opened=False
        if any('/SharedSkills/' in c['path'] and c['clickable'] for c in obs['controls']) and not available(obs,'Btn_Cumbersome'):
            current=json.loads(state(obs)['audit.costNativeState'])['sharedSkills']
            if not current['sourceReturns'] and not current['howls'] and not current['overload']:
                obs=click(demo,obs,'Btn_Back',rows)
        if not any('/SharedSkills/' in c['path'] and c['clickable'] for c in obs['controls']):
            if not available(obs,'Btn_SharedSkills'):
                if not available(obs,'Btn_JourneyTools'):obs=click(demo,obs,'Btn_Menu',rows)
                obs=click(demo,obs,'Btn_JourneyTools',rows)
            obs=click(demo,obs,'Btn_SharedSkills',rows);newly_opened=True
        pose=getattr(demo,'_cumbersome_pose',None)
        same=not newly_opened and pose==(obs['version'],action['Targets'],action['Option'])
        if not same:
            obs=click(demo,obs,'Btn_Enemy_'+str(action['Targets']),rows)
            for _ in range(action['Option']):obs=click(demo,obs,'Btn_AttackSlot',rows)
        obs=click(demo,obs,'Btn_Cumbersome',rows)
        demo._cumbersome_pose=(obs['version'],action['Targets'],action['Option'])
        return obs
    if action['Kind'] in ('source','extraSource'):
        extra=action['Kind']=='extraSource'
        if action.get('Input') or action.get('Card') or action.get('Choice') or action.get('Parts') or (not extra and action.get('Targets')):
            raise ValueError('Source carries unbound parameters')
        colors=('Red','Blue','Green','White','Gold','Black')
        color=colors[action['Option']]
        native=json.loads(state(obs)['audit.costNativeState'])
        face=colors[action['Targets']] if extra else color
        physical=native['auditSourceOrder'].index(face)
        if face=='Gold' and color!='Gold':raise ValueError('Gold source UI preserves Gold; original extraSource requests a basic-colored token')
        if not any(c['name']=='Btn_UseSource' and c['clickable'] for c in obs['controls']):
            if not any(c['name']=='Btn_JourneyTools' and c['clickable'] for c in obs['controls']):
                obs=click(demo,obs,'Btn_Menu',rows)
            obs=click(demo,obs,'Btn_JourneyTools',rows)
        obs=click(demo,obs,'Btn_Source_'+str(physical),rows)
        wild=json.loads(state(obs)['audit.costFixture']).get('blackManaWild',False) or (extra and native['extraBlackDice']>0)
        if face=='Black' and wild and available(obs,'Btn_SourceColor_'+color):
            controls=available(obs,'Btn_SourceColor_'+color)
            if '●' not in controls[0]['text']:obs=click(demo,obs,'Btn_SourceColor_'+color,rows)
        # End A at the actual native commit. Returning from this panel is part
        # of the following action's approach, so it is counted exactly once.
        return click(demo,obs,'Btn_UseExtraSource' if extra else 'Btn_UseSource',rows)
    if action['Kind']=='advance':
        if action.get('Input') or any(action.get(k,0) for k in ('Targets','Parts','Option')):
            raise ValueError('Advance carries unbound parameters')
        return click(demo,obs,'Btn_Next',rows)
    if action['Kind']=='play':return play(demo,obs,action,rows)
    if action['Kind']=='unit':return unit_action(demo,obs,action,rows)
    if action['Kind']=='bannerUse':return banner_use(demo,obs,action,rows)
    if action['Kind']=='unitDamage':return damage_plan(demo,obs,action,rows)
    if action['Kind']=='skill':return skill_action(demo,obs,action,rows)
    if action['Kind']=='skillSideways':return sideways_skill(demo,obs,action,rows)
    if action['Kind'] in ('convertMovement','convertInfluence'):return conversion(demo,obs,action,rows)
    if action['Kind']=='heal':return heal(demo,obs,action,rows)
    if action['Kind'] in ('explosiveArmor','killCrystal'):return reward_choice(demo,obs,action,rows)
    if action['Kind']=='bowChoice':return bow_choice(demo,obs,action,rows)
    if action['Kind']=='artifact':return artifact(demo,obs,action,rows)
    if action['Kind']=='boxReveal':
        if action.get('Input') or action.get('Choice') or any(action.get(k,0) for k in ('Targets','Parts','Option')):raise ValueError('Unbound box reveal parameters')
        native=json.loads(state(obs)['audit.costNativeState'])
        obs=select_card(demo,obs,'items_024',rows)
        obs=click(demo,obs,'Btn_PlayOutside' if native['phase']=='Outside' else 'Btn_OpenSpecialArtifact',rows)
        return click(demo,obs,'Btn_ConfirmSpecial',rows)
    if action['Kind']=='rest':return rest(demo,obs,action,rows)
    if action['Kind'] in ('skillBullseye','soulGuideBlock'):return contribution(demo,obs,action,rows)
    if action['Kind'] in ('attack','block','blockSelected'):return combat_commit(demo,obs,action,rows)
    if action['Kind']=='sideways':
        if action.get('Input') or any(action.get(k,0) for k in ('Targets','Parts','Option')):
            raise ValueError('Sideways carries unbound parameters')
        obs=select_card(demo,obs,action['Card'],rows);return click(demo,obs,'Btn_Sideways',rows)
    raise ValueError('UI adapter not yet implemented for '+action['Kind'])


def game_difference(expected,actual):
    # These are protocol accounting, not mutations by the UI controller.
    bookkeeping={'activity','planningRun'}
    if expected.get('stopReason') in ('no_legal_continuation','step_limit'):
        bookkeeping.add('stopReason')  # training protocol mask/limit, not a UI mutation
    actual,_=align(expected,actual)
    return differences({k:v for k,v in expected.items() if k not in bookkeeping},
                       {k:v for k,v in actual.items() if k not in bookkeeping})


def main():
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True)
    p.add_argument('--pending-only',action='store_true',help='Replay unchanged selected IDs without an accepted measurement')
    p.add_argument('--plan',type=Path,default=ROOT/'AutomationOutputs/UiFluency/20261012_sample5_selected')
    p.add_argument('--fast',action='store_true')
    p.add_argument('--replay-order',choices=('episode','group'),default='episode',help='Group exact selected cases by archive/episode for decoded-cache reuse; sample selection and per-case reset stay unchanged')
    p.add_argument('--attach-live',type=Path,help='Reuse the current pilot after a launch transport timeout; requires a fresh successful observe')
    p.add_argument('--keep-live',action='store_true',help='Leave this owned pilot available for a subsequent fresh-observe attachment')
    p.add_argument('--limit',type=int)
    p.add_argument('--retry-results',type=Path,help='Restrict to failed fixed sample IDs in a previous immutable result; never replace samples')
    p.add_argument('--all-mapped-groups',action='store_true',help='All frozen pair groups whose kinds have current dispatch branches')
    p.add_argument('--with-kinds',nargs='+',help='Restrict selected groups to pairs containing at least one of these kinds')
    p.add_argument('--with-cards',nargs='+',help='Filter frozen cases by original Card parameter on either intent, before limit/order')
    p.add_argument('--groups',nargs='+',default=['advance→advance','sideways→advance']);args=p.parse_args()
    out=args.out.resolve();out.mkdir(parents=True,exist_ok=False)
    sources=[Path(__file__),ROOT/'tools/BattleRl/cost_entity_identity.py',ROOT/'tools/BattleRl/gate_unity_cost_prefix.py',
        ROOT/'Assets/Scripts/Battle/CostPrefixAdapter.cs',ROOT/'Assets/Scripts/Battle/PveBattleController.CostFixture.cs',
        ROOT/'Assets/Scripts/SceneAutomation/StructuredUiPilot.cs']
    sources+=list((ROOT/'Assets/Scripts/Battle').glob('*.cs'))+list((ROOT/'Assets/Logic').rglob('*.cs'))
    sources+=[ROOT/'tools/BattleRl'/name for name in ('measure_ui_fluency.py','gate_unity_cost_task_reset.py','structured_ui_ppo.py')]
    sources.append(ROOT/'tools/BattleRl/ui_cost_views.py')
    sources.append(ROOT/'tools/BattleRl/cost_result_io.py')
    sources += [ROOT/'resources/text_json'/name for name in ('basic_card.json','advanced_card.json','magic.json')]
    sources=sorted(set(sources))
    write_json(out/'source_manifest.json',dict(source_sha256={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sources},
        sample_plan=str(args.plan.resolve()),fast_capture=args.fast,compact_result_json=args.fast,replay_order=args.replay_order,result_bundle_reuses_case_json=args.fast,view_metric_version=VIEW_METRIC_VERSION,initial_ui_pose='neutral selections, first hand page after native setup; native pending defense panel restored when required; prefix is not measured pointer replay'))
    selected=args.plan.resolve()/'selected.sqlite'
    db=sqlite3.connect('file:'+str(selected).replace('\\','/')+'?mode=ro',uri=True)
    if args.all_mapped_groups:
        mapped={'advance','source','extraSource','sideways','play','attack','block','blockSelected','unit','unitLeadership','offeredUnit','learn','unitDamage','skill','skillSideways','skillFork','skillShapeshift','puppetKeep','puppetUse','convertMovement','convertInfluence','heal','rest','skillBullseye','soulGuideBlock','defend','clearDefend','explosiveArmor','killCrystal','bowChoice','artifact','boxReveal','boxUse','cumbersome','bannerUse','fight','repeat','protectionCleanup','roundReturn'}
        mapped|=SHARED_KINDS
        mapped|=WORLD_KINDS
        mapped|={'payment','cancelPayment'}
        mapped|={'nextTurn','roundFinish'}
        args.groups=[r[0] for r in db.execute('SELECT DISTINCT group_id FROM samples ORDER BY group_id') if set(r[0].split('→'))<=mapped]
    if args.with_kinds:args.groups=[g for g in args.groups if set(g.split('→'))&set(args.with_kinds)]
    db.execute('ATTACH DATABASE ? AS work',(str(ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite'),))
    query='''SELECT s.group_id,s.episode,s.a_step,s.estimator_weight,e.path,e.volume,e.sha256,e.reset_json,e.prefix_json
      FROM samples s JOIN work.episodes e ON e.id=s.episode JOIN work.actions aa ON aa.id=s.a JOIN work.actions bb ON bb.id=s.b WHERE s.group_id IN ('''+','.join('?' for _ in args.groups)+')'+(" AND s.status NOT IN ('complete','verified_incompatible')" if args.pending_only else '')
    query_args=list(args.groups)
    if args.with_cards:
        marks=','.join('?' for _ in args.with_cards)
        query+=' AND (json_extract(aa.arguments_json,\'$.Card\') IN ('+marks+') OR json_extract(bb.arguments_json,\'$.Card\') IN ('+marks+'))'
        query_args+=args.with_cards+args.with_cards
    query+=' ORDER BY s.group_id,s.episode,s.a_step'
    samples=db.execute(query,query_args).fetchall();db.close()
    if args.retry_results:
        retry=json.loads(args.retry_results.read_text(encoding='utf-8'))
        keys={(r['group'],r['episode'],r['a_step']) for r in retry['samples'] if r['status']!='complete'}
        samples=[r for r in samples if tuple(r[:3]) in keys]
    if args.limit is not None:samples=samples[:args.limit]
    # Apply limits before reordering so this never substitutes selected cases.
    if args.replay_order=='episode':samples.sort(key=lambda r:(r[5],r[1],r[2],r[0]))
    if not samples:
        write_result(out/'results.json',dict(samples=[],requested_samples=0,elapsed_seconds=0,scope='No frozen cases match the selection; no UI measurements'),compact=args.fast)
        write_json(out/'runtime.json',dict(status='no_selected_cases',pilot_started=False))
        print(json.dumps(dict(requested_samples=0,pilot_started=False)),flush=True)
        return
    demo=ReplayDemo.attach_current(args.attach_live) if args.attach_live else ReplayDemo(out/'live')
    demo.fast_capture=args.fast;results=[];case_paths=[];archives={};documents=OrderedDict();cache_hits=0;started=time.monotonic()
    runtime=dict(live_directory=str(demo.directory),attached=bool(args.attach_live),status='running')
    write_json(out/'runtime.json',runtime);transport_failed=False
    try:
        for num,(group,eid,pos,weight,path,volume,source_sha,reset,initial) in enumerate(samples):
            row=dict(group=group,episode=eid,a_step=pos,estimator_weight=weight,source=path,
                     source_sha256=source_sha,status='started',pointer_steps=[])
            results.append(row)
            case_started=time.monotonic();timings={};row['stage_seconds']=timings;case_observation=None
            try:
                task=json.loads(reset).get('task')
                if task is None:raise ValueError('Exact original initialization definition not recovered; generator unclassified')
                if volume not in archives:archives[volume]=zipfile.ZipFile('H:/MageKnight_RL_Backups/20261012_before_reward_v2/'+volume)
                loading=time.monotonic();key=(volume,path,source_sha)
                if key in documents:
                    doc=documents.pop(key);cache_hits+=1
                else:
                    raw=archives[volume].read(path)
                    assert hashlib.sha256(raw).hexdigest()==source_sha
                    doc=json.loads(raw)
                documents[key]=doc
                if len(documents)>8:documents.popitem(last=False)
                timings['archive_decode']=time.monotonic()-loading;trace=doc['trace']
                setup=time.monotonic()
                obs=demo.reset_task(task)
                prefix=[x['action'] for x in doc.get('curriculum_prefix',[])]
                prefix += [x['action'] for x in trace[:pos]]
                demo.payment_consumer=next((x['action'] for x in trace[pos:pos+2] if x['action']['Kind'] in ('play','unit')),None)
                row['payment_binding_policy']='measured play/unit consumer if still present; otherwise first actual non-material eligible hand card or non-destroyed unit'
                obs=demo.prepare_prefix(prefix,demo.payment_consumer)
                case_observation=obs
                timings['native_setup']=time.monotonic()-setup
                actual,identity=align(trace[pos]['before'],json.loads(state(obs)['audit.costNativeState']))
                row['identity_alignment']={'before_a':identity}
                error=differences(trace[pos]['before'],actual)
                if error:raise AssertionError(dict(stage='prefix',differences=error[:30]))
                a_rows=[];pointer_started=time.monotonic()
                try:obs=apply(demo,obs,trace[pos]['action'],a_rows)
                finally:row['pointer_steps']+=a_rows
                if not a_rows:raise ValueError('First intent has no measured pointer endpoint; setup cursor cannot supply transition distance')
                _,row['identity_alignment']['after_a']=align(trace[pos]['after'],json.loads(state(obs)['audit.costNativeState']))
                error=game_difference(trace[pos]['after'],json.loads(state(obs)['audit.costNativeState']))
                if error:raise AssertionError(dict(stage='A',differences=error[:30]))
                b_rows=[]
                try:obs=apply(demo,obs,trace[pos+1]['action'],b_rows)
                finally:row['pointer_steps']+=b_rows
                if not b_rows:
                    row['zero_click_after_b']=obs
                    row['b_completion_kind']='Existing visible native goal already fulfils the requested intent; no pointer movement or synthetic commit'
                _,row['identity_alignment']['after_b']=align(trace[pos+1]['after'],json.loads(state(obs)['audit.costNativeState']))
                error=game_difference(trace[pos+1]['after'],json.loads(state(obs)['audit.costNativeState']))
                if error:raise AssertionError(dict(stage='B',differences=error[:30]))
                timings['pointer_and_checks']=time.monotonic()-pointer_started
                points=[(r['actual'].get('controlCenterX',r['actual']['click']['clickX']),r['actual'].get('controlCenterY',r['actual']['click']['clickY'])) for r in a_rows+b_rows]
                bridge=[points[len(a_rows)-1]]+points[len(a_rows):]
                actual_points=[(r['actual']['click']['clickX'],r['actual']['click']['clickY']) for r in a_rows+b_rows]
                actual_bridge=[actual_points[len(a_rows)-1]]+actual_points[len(a_rows):]
                row.update(status='complete',a_clicks=len(a_rows),b_clicks=len(b_rows),
                    pair_clicks=len(points),distance_internal_a_px=sum(math.dist(a,b) for a,b in zip(points[:len(a_rows)],points[1:len(a_rows)])),
                    distance_a_commit_to_b_commit_px=sum(math.dist(a,b) for a,b in zip(bridge,bridge[1:])),
                    actual_pointer_internal_a_px=sum(math.dist(a,b) for a,b in zip(actual_points[:len(a_rows)],actual_points[1:len(a_rows)])),
                    actual_pointer_a_commit_to_b_commit_px=sum(math.dist(a,b) for a,b in zip(actual_bridge,actual_bridge[1:])),
                    panels=sum(panel(r['actual']['before'])!=panel(r['actual']['observation']) for r in a_rows+b_rows),
                    b_first_frame_ms=b_rows[-1]['actual']['timing']['firstRenderedFrameMilliseconds'] if b_rows else None)
            except Exception as error:
                row.update(status='failed',error=repr(error))
                row['failure_observation']=row['pointer_steps'][-1]['actual']['observation'] if row['pointer_steps'] else case_observation
                transport_failed=isinstance(error,TimeoutError)
            timings['total']=time.monotonic()-case_started
            case_path=out/'cases'/f'{num:05d}.json'
            write_result(case_path,row,compact=args.fast);case_paths.append(case_path)
            if transport_failed or not args.fast or (num+1)%10==0 or num+1==len(samples):
                metadata=dict(requested_samples=len(samples),elapsed_seconds=time.monotonic()-started,episode_cache_hits=cache_hits,episode_cache_limit=8,scope='frozen sampled original pair actual pointer measurement; failed cases stay')
                if args.fast:write_case_bundle(out/'results.json',case_paths,metadata)
                else:write_result(out/'results.json',dict(samples=results,**metadata))
            print(json.dumps(dict(index=num+1,total=len(samples),group=group,episode=eid,step=pos,status=row['status'],error=row.get('error','')[:500])),flush=True)
            if transport_failed:break
    finally:
        for archive in archives.values():archive.close()
        if transport_failed:
            # The last request may still execute. Keep that pilot and its
            # request intact for an explicit fresh-observe attachment.
            runtime.update(status='transport_timeout',attempted_samples=len(results),unattempted_samples=len(samples)-len(results),pilot_left_for_recovery=True)
        elif args.keep_live:
            runtime.update(status='idle_for_next_batch',attempted_samples=len(results),unattempted_samples=len(samples)-len(results),pilot_left_for_recovery=True)
        else:
            demo.close();runtime.update(status='closed',attempted_samples=len(results),unattempted_samples=len(samples)-len(results))
        write_json(out/'runtime.json',runtime)


if __name__=='__main__':main()
