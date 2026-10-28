"""Prove missing UI affordances against a separate current native replay.

Keep original fixed IDs, failure evidence and effect checks. No completion cost
is invented for an intent with no working current UI route.
"""
import argparse
import hashlib
import re
import json
from pathlib import Path
import sqlite3
import zipfile
from measure_sampled_ui_pairs import game_difference,ReplayDemo
from structured_ui_ppo import ROOT,state,write_json

def main():
    p=argparse.ArgumentParser();p.add_argument('results',type=Path);p.add_argument('--out',type=Path,required=True);p.add_argument('--attach-live',type=Path);p.add_argument('--keep-live',action='store_true',help='Keep this owned pilot for a subsequent fresh-observe attachment');args=p.parse_args()
    raw=args.results.read_bytes();cases=json.loads(raw)['samples'];out=args.out.resolve();out.mkdir(parents=True,exist_ok=False)
    db=sqlite3.connect('file:'+str(ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite').replace('\\','/')+'?mode=ro',uri=True)
    candidates=[]
    for row in cases:
        if row['status']=='complete':continue
        if row['pointer_steps']:pointer=row['pointer_steps'][-1]
        elif row.get('failure_observation'):pointer=dict(button='',actual=dict(observation=row['failure_observation']))
        else:continue
        obs=pointer['actual']['observation'];native=json.loads(state(obs)['audit.costNativeState'])
        kind=None
        clickable=[c for c in obs['controls'] if c['clickable']]
        flow=json.loads(state(obs)['audit.costFixture']).get('turnFlow',{})
        if 'formal_end_turn_denied:' in row.get('error','') and native.get('sharedSkills',{}).get('sourceReturns') and pointer['button']=='Btn_EndTurn' and any(c['name'].startswith('Btn_ReturnKeep_') for c in clickable) and any(c['name'].startswith('Btn_ReturnRoll_') for c in clickable):
            kind='native_end_turn_refused_pending_source_return'
        elif 'actual control Btn_EndRound' in row.get('error','') and native['roundWindow'] and flow.get('roundEnded') and not flow.get('active') and not flow.get('prepared') and {c['name'] for c in clickable}=={'Btn_PrepareRound'} and not any(c['name']=='Btn_EndRound' for c in obs['controls']):
            kind='round_finish_hidden_by_prepare_round'
        elif 'actual control Btn_LearningSupply' in row.get('error','') and native['phase']=='Block' and native['learning'] and any(c['name']=='Btn_ReduceAttack' and c['clickable'] for c in obs['controls']) and not any(c['name']=='Btn_LearningSupply' for c in obs['controls']):
            kind='attack_reduction_button_hides_learning_supply'
        elif 'Protection cleanup outside control not available in this actual phase' in row.get('error','') and native['phase']!='Outside' and native['artifactFlags']['protectionBurned'] and not any(c['name'] in ('Btn_ProtectionCleanup','Btn_JourneyHand') for c in obs['controls']):
            kind='protection_cleanup_battle_control_missing'
        elif 'Missing or ambiguous actual control' in row.get('error','') and (native.get('repeated') or {}).get('remaining',0)>0 and len(clickable)==1 and clickable[0]['name']=='Btn_ContinueOriginal' and '/PendingCopy/' in clickable[0]['path']:
            kind='mandatory_repeat_overlay_blocks_interleaved_intent'
        elif 'Btn_Enemy_' in row.get('error','') and native.get('sharedSkills',{}).get('sourceReturns') and any(c['clickable'] and c['name'].startswith('Btn_Return') for c in obs['controls']) and not any(c['name']=='Btn_Cumbersome' for c in obs['controls']) and all('/SharedSkills/' in c['path'] for c in obs['controls'] if c['clickable']):
            kind='source_return_choice_hides_cumbersome_order'
        elif 'Btn_Back' in row.get('error','') and native.get('sharedSkills',{}).get('howls',0)>0 and any(c['name']=='Btn_Back' and not c['interactable'] and '/SharedSkills/' in c['path'] for c in obs['controls']) and all('/SharedSkills/' in c['path'] for c in obs['controls'] if c['clickable']):
            kind='mandatory_howl_resolution_blocks_banner_order'
        elif 'Original courage banner use control is unavailable outside battle' in row.get('error','') and native['phase']=='Outside':
            kind='courage_banner_outside_control_missing'
        elif 'actual control Btn_Courage' in row.get('error','') and native['phase']=='End' and pointer['button'].startswith('Btn_Unit_') and not any(c['name']=='Btn_Courage' for c in obs['controls']) and any(c['clickable'] and '/Inspector/' in c['path'] and c['name'].startswith('Btn_UnitAbility_') for c in obs['controls']):
            kind='courage_banner_end_control_missing'
        elif 'Original fortitude checkbox is unavailable in this actual UI phase' in row.get('error',''):
            if not any(c['name']=='Btn_Fortitude' for c in obs['controls']) and any(c['clickable'] and '/Inspector/' in c['path'] and c['name'].startswith(('Btn_UnitMode_','Btn_UnitAbility_')) for c in obs['controls']):
                kind='fortitude_banner_unit_control_missing'
        elif 'Original bow retention mode has no actual current UI control' in row.get('error',''):
            kind='bow_original_ranged_retention_control_missing'
        elif 'Exact atomic conversion amount has no UI control' in row.get('error',''):
            kind='atomic_conversion_amount_control_missing'
        elif 'Original choice mode unavailable' in row.get('error','') and native['phase']=='AssignDamage':kind='play_hidden_in_assign_damage'
        elif 'Btn_Sideways' in row.get('error','') and pointer['button'].startswith('Btn_Card_items_'):kind='artifact_sideways_button_missing'
        elif 'Btn_UnitAbility_' in row.get('error','') and native['phase']=='AssignDamage' and pointer['button'].startswith('Btn_Unit_'):
            kind='unit_ability_hidden_in_assign_damage'
        elif 'Btn_UnitAbility_' in row.get('error','') and native['phase'] in ('Ranged','Block','Melee') and pointer['button'].startswith('Btn_Unit_'):
            audit=json.loads(state(obs)['audit.costFixture']);match=re.search(r'Btn_UnitAbility_(\d+)',row.get('error',''))
            binding=next((u for u in audit['unitAbilities'] if u['id']==audit['uiUnit']),None)
            ability=next((a for a in binding['abilities'] if match and a['slot']==int(match[1])),None) if binding else None
            if ability and ability['kind'] in ('Movement','Influence','MovementOrInfluence') and any(c['name'].startswith('Btn_UnitAbility_') and '/Inspector/' in c['path'] for c in clickable) and not any(c['name']=='Btn_UnitAbility_'+match[1] for c in obs['controls']):
                kind='unit_movement_influence_phase_scope_mismatch'
        elif pointer['button']=='Btn_Resolve' and native['phase']=='Block' and not native['blocks']:
            before_values=state(pointer['actual']['before']);after_values=state(obs)
            if int(after_values.get('rejections',0))>int(before_values.get('rejections',0)):
                kind='empty_source_zero_block_rejected_by_ui'
        if kind is None:continue
        volume,reset=db.execute('SELECT volume,reset_json FROM episodes WHERE id=?',(row['episode'],)).fetchone()
        with zipfile.ZipFile('H:/MageKnight_RL_Backups/20261012_before_reward_v2/'+volume) as archive:source=archive.read(row['source'])
        assert hashlib.sha256(source).hexdigest()==row['source_sha256'];doc=json.loads(source)
        possible=[]
        for pos in (row['a_step'],row['a_step']+1):
            action=doc['trace'][pos]['action']
            comparison=native
            if kind.startswith('native_end_turn_refused'):
                record=doc['trace'][pos]
                if action['Kind']!='nextTurn' or not record['after'].get('stopReason','').startswith('formal_end_turn_denied:'):continue
                before_rule={k:v for k,v in record['before'].items() if k!='stopReason'}
                after_rule={k:v for k,v in record['after'].items() if k!='stopReason'}
                if game_difference(before_rule,after_rule):continue
            elif kind.startswith('round_finish_hidden'):
                if action['Kind']!='roundFinish' or not comparison['roundWindow']:continue
            elif kind.startswith('attack_reduction_button'):
                if action['Kind']!='learn':continue
            elif kind.startswith('protection_cleanup'):
                if action['Kind']!='protectionCleanup' or action['Card']!='items_002' or action['Option'] not in (0,1):continue
            elif kind.startswith('mandatory_repeat'):
                if action['Kind'] not in ('block','blockSelected','unitDamage','unit','skill','learn'):continue
            elif kind.startswith('source_return_choice'):
                if action['Kind']!='cumbersome':continue
            elif kind.startswith('mandatory_howl'):
                if action['Kind'] not in ('bannerUse','skill','unit'):continue
            elif kind.startswith('fortitude_banner'):
                if action['Kind']!='bannerUse' or action['Card']!='items_018':continue
                if comparison['units'][action['Targets']]['bannerEffect']!='items_018':continue
                if json.loads(state(obs)['audit.costFixture'])['uiUnit']!=comparison['units'][action['Targets']]['id']:continue
                if any(c['name']=='Btn_Fortitude' for c in obs['controls']):continue
            elif kind.startswith('courage_banner'):
                if action['Kind']!='bannerUse' or action['Card']!='items_003':continue
                if pointer['button']!='Btn_Unit_'+str(action['Targets']):continue
                if comparison['units'][action['Targets']]['bannerEffect']!='items_003':continue
                if kind=='courage_banner_end_control_missing' and json.loads(state(obs)['audit.costFixture'])['uiUnit']!=comparison['units'][action['Targets']]['id']:continue
                if any(c['name']=='Btn_Courage' and c['clickable'] for c in obs['controls']):continue
            elif kind.startswith('bow_original'):
                if action['Kind']!='bowChoice' or action['Option']!=0:continue
                if not any(c['name']=='Btn_BowContribution' and c['clickable'] for c in obs['controls']):continue
                if comparison['attacks'][action['Targets']]['type']!='Ranged':continue
            elif kind.startswith('atomic_conversion'):
                if action['Kind'] not in ('convertMovement','convertInfluence') or action['Option']<=1:continue
                if not any('/Conversions/' in c['path'] and c['clickable'] for c in obs['controls']):continue
            elif kind.startswith('empty_source'):
                if action['Kind']!='block':continue
                comparison=json.loads(state(pointer['actual']['before'])['audit.costNativeState'])
                if comparison['enemies'][action['Targets']]['attacks'][action['Option']]['effectiveAttack']!=0:continue
            elif kind.startswith('unit'):
                audit=json.loads(state(obs)['audit.costFixture'])
                if action['Kind']!='unit' or action['Card']!=audit['uiUnit']:continue
                if any(c['name']=='Btn_UnitAbility_'+action['Choice'] and c['clickable'] for c in obs['controls']):continue
            else:
                if pointer['button']!='Btn_Card_'+action['Card']+'_0':continue
                if action['Kind']!=('play' if kind.startswith('play') else 'sideways'):continue
            if not game_difference(doc['trace'][pos]['before'],comparison):possible.append(pos)
        if len(possible)!=1:continue
        candidates.append((row,kind,possible[0],doc,json.loads(reset)['task']))
    if not candidates:
        write_json(out/'results.json',dict(cases=[],candidates=0,pointer_results_path=str(args.results.resolve()),scope='No independently provable candidates; no UI proof run'))
        write_json(out/'runtime.json',dict(status='no_candidates',pilot_started=False))
        print(json.dumps(dict(candidates=0,pilot_started=False)),flush=True);db.close();return
    results=[];demo=ReplayDemo.attach_current(args.attach_live) if args.attach_live else ReplayDemo(out/'live');demo.fast_capture=True;transport_failed=False
    write_json(out/'runtime.json',dict(live_directory=str(demo.directory),attached=bool(args.attach_live)))
    try:
        for row,kind,pos,doc,task in candidates:
            result=dict(group=row['group'],episode=row['episode'],a_step=row['a_step'],failing_step=pos,category=kind,
                source_sha256=row['source_sha256'],pointer_failure_result_sha256=hashlib.sha256(raw).hexdigest(),status='unverified')
            results.append(result)
            try:
                demo.reset_task(task)
                prefix=[x['action'] for x in doc.get('curriculum_prefix',[])]+[x['action'] for x in doc['trace'][:pos]]
                before=demo.prepare_prefix(prefix);differences=game_difference(doc['trace'][pos]['before'],json.loads(state(before)['audit.costNativeState']))
                if differences:raise AssertionError(dict(stage='native_before',differences=differences))
                after=demo.prepare_prefix([doc['trace'][pos]['action']]);differences=game_difference(doc['trace'][pos]['after'],json.loads(state(after)['audit.costNativeState']))
                if differences:raise AssertionError(dict(stage='native_after',differences=differences))
                result.update(status='verified_incompatible',native_before=before,native_after=after,
                    completion_cost=None,partial_clicks=len(row['pointer_steps']),reason='Actual sampled UI cannot complete the requested operation; same formal action passes separate native replay and full effect check')
                if kind.startswith('native_end_turn_refused'):
                    result['reason']='Independent native replay confirms the archived request was refused by formal rules; actual UI presents the unresolved source-return choice. This is a refused trajectory action, not proof of a missing UI feature; partial clicks are retained and completion cost stays null.'
            except Exception as error:
                result.update(error=repr(error));transport_failed=isinstance(error,TimeoutError)
            write_json(out/'results.json',dict(cases=results,candidates=len(candidates),pointer_results_path=str(args.results.resolve()),scope='Actual UI failure plus separate exact native replay; no zero completion costs'))
            print(json.dumps({k:v for k,v in result.items() if k not in ('native_before','native_after')},ensure_ascii=False),flush=True)
            if transport_failed:break
    finally:
        if args.keep_live or transport_failed:
            write_json(out/'runtime.json',dict(live_directory=str(demo.directory),attached=bool(args.attach_live),status='transport_timeout' if transport_failed else 'idle_for_next_batch',pilot_left_for_recovery=True))
        else:demo.close()
        db.close()

if __name__=='__main__':main()
