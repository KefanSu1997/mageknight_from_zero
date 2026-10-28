"""Minimal legal UI progression for round/tactic screenshot pairs; no rollout benchmark."""
import argparse,hashlib,json,shutil,sqlite3,uuid
from pathlib import Path
from capture_ui_design_pairs import CaptureDemo
from cost_entity_identity import align
from gate_unity_cost_prefix import differences
from structured_ui_ppo import ROOT,state,write_json

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--out',type=Path,required=True);a=parser.parse_args();out=a.out.resolve();out.mkdir(parents=True,exist_ok=True)
    db=sqlite3.connect('file:'+str(ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite').replace('\\','/')+'?mode=ro',uri=True)
    reset=db.execute('select reset_json from episodes where id=5131').fetchone()[0];task=json.loads(reset)['task']
    demo=CaptureDemo(out/('round_live_'+uuid.uuid4().hex[:8]));records={};events=[]
    def click(obs,name):
        matches=[c for c in obs['controls'] if c['name']==name and c['clickable']]
        if len(matches)!=1:raise ValueError((name,len(matches)))
        events.append(dict(version=version,name=name));return demo.call('click',obs,matches[0])['observation']
    def snap(obs,label):
        r=demo.call('observe',screenshot=True);obs=r['observation'];values=state(obs)
        if values.get('ui.hoverPreviewVisible')!='False':raise AssertionError('Unexpected hover')
        target=out/version/(label+'.png');target.parent.mkdir(exist_ok=True);shutil.copy2(r['screenshotPath'],target)
        fixture=json.loads(values['audit.costFixture']);record=dict(label=label,version=version,context=dict(episode=5131,step=0,role='legal_round_progression_fixture_not_archived_cost_sample'),screenshot=str(target),screenshot_sha256=hashlib.sha256(target.read_bytes()).hexdigest(),native_state=json.loads(values['audit.costNativeState']),discard_slots=fixture.get('turnFlow',{}).get('discardSlots',[]),hover_preview=False,observation=obs)
        write_json(target.with_suffix('.json'),record);records[version][label]=record;return obs
    try:
        for version,opt in [('baseline',False),('candidate',True)]:
            records[version]={};demo.reset_task(task);obs=demo.design(opt)
            for turn in range(10):
                controls={c['name']:c for c in obs['controls'] if c['clickable']}
                if 'Btn_Announce' in controls:
                    obs=snap(obs,'round_announce');obs=click(obs,'Btn_Announce');break
                if 'Btn_BeginTurn' in controls:obs=click(obs,'Btn_BeginTurn')
                if opt:obs=click(obs,'Btn_EndTurnDiscardOrdinary')
                else:
                    obs=click(obs,'Btn_JourneyHand')
                    for name in [c['name'] for c in obs['controls'] if c['clickable'] and c['name'].startswith('Btn_Hand_')]:
                        obs=click(obs,name)
                        if any(c['name']=='Btn_DiscardAtEnd' and c['clickable'] for c in obs['controls']):obs=click(obs,'Btn_DiscardAtEnd')
                    obs=click(click(obs,'Btn_CloseHand'),'Btn_EndTurn')
            else:raise AssertionError('Round announcement did not become available within fixture budget')
            obs=snap(obs,'round_ended')
            obs=click(obs,'Btn_EndRound');obs=snap(obs,'round_prepare')
            obs=click(obs,'Btn_PrepareRound');obs=snap(obs,'tactic_picker')
            tactics=[c for c in obs['controls'] if c['name'].startswith('Btn_Tactic_') and c['clickable']]
            # Prefer a tactic with no mandatory random hand replacement, keeping
            # the comparison fixture short while preserving all legal options.
            choice=next((c for c in tactics if c['name']=='Btn_Tactic_night_1'),tactics[0])
            obs=click(obs,choice['name']);obs=snap(obs,'tactic_chosen')
    finally:
        demo.close();write_json(out/'round_capture_events.json',events)
    added=[]
    for name in records['baseline'].keys() & records['candidate'].keys():
        x,y=records['baseline'][name],records['candidate'][name];actual,_=align(x['native_state'],y['native_state']);diff=differences(x['native_state'],actual)
        if diff:raise AssertionError(dict(label=name,differences=diff[:5]))
        added.append(dict(label=name,context=x['context'],baseline=x['screenshot'],candidate=y['screenshot'],same_native_state=True,hover_preview=False))
    manifest_path=out/'paired_manifest.json';manifest=json.loads(manifest_path.read_text(encoding='utf8'))
    existing={p['label'] for p in manifest['pairs']}
    if existing & {p['label'] for p in added}:raise AssertionError('Round evidence already exists; use a new version directory')
    manifest['pairs']+=sorted(added,key=lambda p:p['label']);write_json(manifest_path,manifest)
    print(json.dumps(dict(added_pairs=len(added),legal_ui_clicks=len(events),benchmark=False)))
if __name__=='__main__':main()
