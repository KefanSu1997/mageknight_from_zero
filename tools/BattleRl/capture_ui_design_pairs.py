"""Same archived rule state, two actual UI profiles; no training/timing run."""
import hashlib
import argparse
import json
import shutil
import sqlite3
import time
import uuid
import zipfile
from pathlib import Path
from gate_unity_cost_task_reset import CostDemo
from gate_unity_cost_prefix import differences
from cost_entity_identity import align
from structured_ui_ppo import ROOT, state, write_json

OUT=ROOT/'AutomationOutputs/UiFluency/design_review/v2_context_preserving'


def editor_call(operation, *, timeout=90):
    root=ROOT/'AutomationOutputs/UiFluency/design_review'
    rid=uuid.uuid4().hex
    write_json(root/'editor_request.json',dict(id=rid,operation=operation))
    start=time.monotonic()
    while time.monotonic()-start<timeout:
        p=root/'editor_responses'/(rid+'.json')
        if p.exists():
            try:r=json.loads(p.read_text(encoding='utf8'))
            except (OSError,json.JSONDecodeError):time.sleep(.05);continue
            if not r['success']:raise RuntimeError(r['error'])
            return r
        time.sleep(.1)
    raise TimeoutError('Editor capture bridge did not respond')


class CaptureDemo(CostDemo):
    def __init__(self,directory,attach=False):
        self.directory=Path(directory).resolve();self.directory.mkdir(parents=True,exist_ok=attach);self.calls=0
        (ROOT/'AutomationOutputs/structured_ui_pilot_directory.txt').write_text(str(self.directory),encoding='utf8')
        if not attach:write_json(self.directory/'launch.json',editor_call('start_pilot'))
        self.initial_observation=self.call('observe',timeout=60)['observation']
    def design(self,optimized):
        return self.native_call('set_ui_design','unused',{},optimizedUi=optimized)


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--attach-live',type=Path);parser.add_argument('--out',type=Path);args=parser.parse_args()
    global OUT
    if args.out:OUT=args.out.resolve()
    OUT.mkdir(parents=True,exist_ok=True)
    db=sqlite3.connect('file:'+str(ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite').replace('\\','/')+'?mode=ro',uri=True)
    fixtures=[('outside',5131,0),('ranged',1959,1),('block',14407,2),('source_play',14407,4),('assign_damage',6803,4),('damage_toggle',14407,8),('melee_two_enemies',14617,5),('won_result',6803,17),('end',5092,13),('end_mixed',1959,36)]
    demo=CaptureDemo(args.attach_live or OUT/('live_'+uuid.uuid4().hex[:8]),attach=bool(args.attach_live));pairs=[];errors=[]
    def snap(obs,label,version,context,hover=False):
        result=demo.call('observe',screenshot=True)
        destination=OUT/version/(label+'.png');destination.parent.mkdir(exist_ok=True)
        shutil.copyfile(result['screenshotPath'],destination)
        observation=result['observation']
        if state(observation).get('ui.hoverPreviewVisible')!=str(hover):
            raise AssertionError('Screenshot hover state differs from its declared primary/hover role')
        native=json.loads(state(observation)['audit.costNativeState'])
        fixture=json.loads(state(observation)['audit.costFixture'])
        record=dict(label=label,version=version,context=context,screenshot=str(destination),screenshot_sha256=hashlib.sha256(destination.read_bytes()).hexdigest(),native_state=native,discard_slots=fixture.get('turnFlow',{}).get('discardSlots',[]),hover_preview=hover,observation=observation)
        write_json(destination.with_suffix('.json'),record)
        return observation,record
    def click(obs,name):
        matches=[c for c in obs['controls'] if c['name']==name and c['clickable']]
        if len(matches)!=1:raise ValueError((name,len(matches)))
        return demo.call('click',obs,matches[0])['observation']
    try:
        for label,eid,pos in fixtures:
            path,volume,sha,reset=db.execute('SELECT path,volume,sha256,reset_json FROM episodes WHERE id=?',(eid,)).fetchone()
            with zipfile.ZipFile('H:/MageKnight_RL_Backups/20261012_before_reward_v2/'+volume) as z:raw=z.read(path)
            assert hashlib.sha256(raw).hexdigest()==sha
            doc=json.loads(raw);task=json.loads(reset)['task'];trace=doc['trace'];prefix=[r['action'] for r in doc.get('curriculum_prefix',[])]+[r['action'] for r in trace[:pos]]
            context=dict(episode=eid,step=pos,archive=volume,path=path,source_sha256=sha,expected_phase=trace[pos]['before']['phase'])
            per_version={}
            for version,optimized in [('baseline',False),('candidate',True)]:
                try:
                    demo.reset_task(task);obs=demo.prepare_prefix(prefix)
                    actual,_=align(trace[pos]['before'],json.loads(state(obs)['audit.costNativeState']))
                    diffs=differences(trace[pos]['before'],actual)
                    if diffs:raise AssertionError(diffs[:6])
                    obs=demo.design(optimized);records={}
                    obs,r=snap(obs,label,version,context);records[label]=r
                    if label=='source_play':
                        color=['Red','Blue','Green','White','Gold','Black'][trace[pos]['action']['Option']]
                        faces=state(obs)['source.faces'].split(',');index=faces.index(color)
                        if optimized:obs=click(obs,'Btn_TakeSource_'+str(index))
                        else:
                            obs=click(click(obs,'Btn_Menu'),'Btn_JourneyTools');obs=click(obs,'Btn_Source_'+str(index));obs=click(click(obs,'Btn_UseSource'),'Btn_CloseTools')
                        actual,_=align(trace[pos+1]['before'],json.loads(state(obs)['audit.costNativeState']))
                        if differences(trace[pos+1]['before'],actual):raise AssertionError('source result differs from archived formal action')
                        obs,r=snap(obs,'source_play_taken',version,context);records[r['label']]=r
                        action=trace[pos+1]['action'];obs=click(obs,'Btn_Card_'+action['Card']+'_0')
                        obs=click(obs,'Btn_Choice_'+action['Choice']);obs,r=snap(obs,'source_play_strong_selected',version,context);records[r['label']]=r
                        obs=click(obs,'Btn_Play')
                        actual,_=align(trace[pos+2]['before'],json.loads(state(obs)['audit.costNativeState']))
                        diff=differences(trace[pos+2]['before'],actual)
                        if diff:raise AssertionError(dict(source_play_result=diff[:4]))
                        obs,r=snap(obs,'source_play_consumed',version,context);records[r['label']]=r
                    if label=='ranged':
                        cards=[c for c in obs['controls'] if c['clickable'] and c['name'].startswith('Btn_Card_') and '_w_' not in c['name']]
                        if cards:
                            obs=click(obs,cards[0]['name']);obs,r=snap(obs,'ranged_selected_card',version,context);records[r['label']]=r
                            control=next(c for c in obs['controls'] if c['name']==cards[0]['name'])
                            obs=demo.call('hover',obs,control)['observation'];obs,r=snap(obs,'ranged_hover_card',version,context,hover=True);records[r['label']]=r
                            control=next(c for c in obs['controls'] if c['name']==cards[0]['name'])
                            obs=demo.call('unhover',obs,control)['observation']
                        if optimized:
                            if not any(c['name']=='Btn_CloseInlineSource' for c in obs['controls']):obs=click(obs,'Btn_QuickMana')
                        else:obs=click(click(obs,'Btn_Menu'),'Btn_JourneyTools')
                        obs,r=snap(obs,'ranged_mana_open',version,context);records[r['label']]=r
                        if optimized:obs=click(obs,'Btn_SourceTacticDetails')
                        obs,r=snap(obs,'ranged_tactic_details',version,context);records[r['label']]=r
                        if optimized:obs=click(obs,'Btn_CloseTools')
                        dice=[c for c in obs['controls'] if c['clickable'] and c['name'].startswith('Btn_TakeSource_' if optimized else 'Btn_Source_')]
                        if dice:
                            obs=click(obs,dice[0]['name'])
                            if not optimized:obs=click(click(obs,'Btn_UseSource'),'Btn_CloseTools')
                            obs,r=snap(obs,'ranged_mana_taken',version,context);records[r['label']]=r
                            if any(c['name']=='Btn_Skills' and c['clickable'] for c in obs['controls']):
                                obs=click(obs,'Btn_Skills');obs,r=snap(obs,'ranged_skills',version,context);records[r['label']]=r
                    if label=='block' and any(c['name']=='Btn_Roster' and c['clickable'] for c in obs['controls']):
                        obs=click(obs,'Btn_Roster');obs,r=snap(obs,'block_units',version,context);records[r['label']]=r
                    if label=='assign_damage' and any(c['name']=='Btn_Roster' and c['clickable'] for c in obs['controls']):
                        obs=click(obs,'Btn_Roster');obs,r=snap(obs,'damage_units',version,context);records[r['label']]=r
                    if label=='damage_toggle':
                        obs=click(obs,'Btn_Roster')
                        targets=[c for c in obs['controls'] if c['name']=='Btn_Target' and c['clickable']]
                        obs=demo.call('click',obs,targets[0])['observation']
                        if optimized:obs=click(obs,'Btn_QuickAssignUnit_0')
                        else:obs=click(click(obs,'Btn_Unit_0'),'Btn_AssignUnit')
                        obs,r=snap(obs,'damage_plan_add',version,context);records[r['label']]=r
                        obs=click(obs,'Btn_QuickAssignUnit_0' if optimized else 'Btn_AssignUnit')
                        obs,r=snap(obs,'damage_plan_remove',version,context);records[r['label']]=r
                    if label=='melee_two_enemies':
                        targets=[c for c in obs['controls'] if c['name']=='Btn_Target' and c['clickable']]
                        if targets:
                            obs=demo.call('click',obs,targets[0])['observation'];obs,r=snap(obs,'melee_target',version,context);records[r['label']]=r
                    if label in ('end','end_mixed'):
                        if optimized:obs=click(obs,'Btn_PlanEndTurnDiscards')
                        else:obs=click(obs,'Btn_JourneyHand')
                        plan_label=label+'_discard_plan'
                        obs,r=snap(obs,plan_label,version,context);records[r['label']]=r
                        if optimized:
                            obs=click(obs,'Btn_SelectAllTurnDiscards')
                        else:
                            for c in [c for c in obs['controls'] if c['clickable'] and c['name'].startswith('Btn_Hand_')]:
                                obs=click(obs,c['name'])
                                if any(c['name']=='Btn_DiscardAtEnd' and c['clickable'] for c in obs['controls']):obs=click(obs,'Btn_DiscardAtEnd')
                        obs,r=snap(obs,label+'_discard_selected',version,context);records[r['label']]=r
                        if optimized:
                            obs=click(obs,'Btn_CancelTurnDiscards');obs=click(obs,'Btn_EndTurnDiscardOrdinary')
                        else:obs=click(click(obs,'Btn_CloseHand'),'Btn_EndTurn')
                        obs,r=snap(obs,label+'_after_discard_end',version,context);records[r['label']]=r
                    if label=='outside':
                        if not optimized:obs=click(obs,'Btn_JourneyTools')
                        obs,r=snap(obs,'outside_mana',version,context);records[r['label']]=r
                        obs=click(obs,'Btn_CloseInlineSource' if any(c['name']=='Btn_CloseInlineSource' for c in obs['controls']) else 'Btn_CloseTools')
                        if optimized:obs=click(obs,'Btn_QuickSupport')
                        else:obs=click(click(obs,'Btn_JourneyHand'),'Btn_Support')
                        obs,r=snap(obs,'outside_support',version,context);records[r['label']]=r
                        obs=click(obs,'Btn_BackSupport');obs,r=snap(obs,'outside_hand',version,context);records[r['label']]=r
                        if optimized:obs=click(obs,'Btn_SelectAllTurnDiscards')
                        else:
                            for c in [c for c in obs['controls'] if c['clickable'] and c['name'].startswith('Btn_Hand_')]:
                                obs=click(obs,c['name'])
                                if any(c['name']=='Btn_DiscardAtEnd' and c['clickable'] for c in obs['controls']):obs=click(obs,'Btn_DiscardAtEnd')
                        obs,r=snap(obs,'outside_hand_discard_selected',version,context);records[r['label']]=r
                        if optimized:obs=click(obs,'Btn_EndTurnFromHand')
                        else:obs=click(click(obs,'Btn_CloseHand'),'Btn_EndTurn')
                        obs,r=snap(obs,'outside_after_discard_end',version,context);records[r['label']]=r
                    per_version[version]=records
                except Exception as error:
                    errors.append(dict(label=label,version=version,error=repr(error)));write_json(OUT/'capture_errors.json',errors)
                    print('FAILED',label,version,repr(error),flush=True)
            if len(per_version)==2:
                for name in sorted(set(per_version['baseline'])&set(per_version['candidate'])):
                    a,b=per_version['baseline'][name],per_version['candidate'][name]
                    aa,_=align(a['native_state'],b['native_state']);diffs=differences(a['native_state'],aa)
                    if diffs:raise AssertionError(dict(label=name,paired_state_differences=diffs[:5]))
                    if a['discard_slots']!=b['discard_slots']:raise AssertionError(dict(label=name,discard_slots=(a['discard_slots'],b['discard_slots'])))
                    pairs.append(dict(label=name,context=context,baseline=a['screenshot'],candidate=b['screenshot'],same_native_state=True,hover_preview=a['hover_preview']))
            write_json(OUT/'paired_manifest.json',dict(canvas=[1920,1080],pairs=pairs,errors=errors,scope='Same-state actual Unity screenshots, not new rollout costs'))
            print('Captured',label,'pairs',len(pairs),flush=True)
    finally:demo.close()
    print(json.dumps(dict(pairs=len(pairs),errors=errors),ensure_ascii=False),flush=True)


if __name__=='__main__':main()
