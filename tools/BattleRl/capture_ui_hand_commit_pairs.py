"""v7 hand-only delta: recapture every affected view; reuse SHA-bound unchanged v6 views."""
import argparse,hashlib,json,shutil,sqlite3,uuid,zipfile
from pathlib import Path
from capture_ui_design_pairs import CaptureDemo
from cost_entity_identity import align
from gate_unity_cost_prefix import differences
from structured_ui_ppo import ROOT,state,write_json

def main():
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);a=p.parse_args();out=a.out.resolve()
    previous=ROOT/'AutomationOutputs/UiFluency/design_review/v6_direct_actions';oldcost=json.loads((previous/'cost_comparison.json').read_text(encoding='utf8'))
    changed=[n for n,h in oldcost['design_source_sha256'].items() if hashlib.sha256((ROOT/'Assets/Scripts/Battle'/n).read_bytes()).hexdigest()!=h]
    assert changed==['PveBattleController.JourneyHand.cs'],changed
    d=sqlite3.connect('file:'+str(ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite').replace('\\','/')+'?mode=ro',uri=True)
    demo=CaptureDemo(out/('hand_commit_live_'+uuid.uuid4().hex[:8]));records={};events=[]
    def click(obs,name):
        c=[c for c in obs['controls'] if c['name']==name and c['clickable']];assert len(c)==1,(name,len(c))
        events.append(dict(version=version,name=name));return demo.call('click',obs,c[0])['observation']
    def snap(obs,label):
        r=demo.call('observe',screenshot=True);obs=r['observation'];s=state(obs);assert s['ui.hoverPreviewVisible']=='False'
        target=out/version/(label+'.png');target.parent.mkdir(exist_ok=True);assert not target.exists();shutil.copy2(r['screenshotPath'],target)
        rec=dict(label=label,version=version,context=context,screenshot=str(target),screenshot_sha256=hashlib.sha256(target.read_bytes()).hexdigest(),native_state=json.loads(s['audit.costNativeState']),discard_slots=json.loads(s['audit.costFixture']).get('turnFlow',{}).get('discardSlots',[]),hover_preview=False,observation=obs)
        write_json(target.with_suffix('.json'),rec);records.setdefault(version,{})[label]=rec;return obs
    try:
        for kind,eid,pos in [('outside',5131,0),('empty',5092,13),('long',14617,20)]:
            path,volume,sha,reset=d.execute('select path,volume,sha256,reset_json from episodes where id=?',(eid,)).fetchone()
            with zipfile.ZipFile('H:/MageKnight_RL_Backups/20261012_before_reward_v2/'+volume) as z:raw=z.read(path)
            assert hashlib.sha256(raw).hexdigest()==sha
            doc=json.loads(raw);expected=doc['trace'][pos]['before'];prefix=[r['action'] for r in doc.get('curriculum_prefix',[])]+[r['action'] for r in doc['trace'][:pos]]
            context=dict(episode=eid,step=pos,archive=volume,path=path,source_sha256=sha)
            for version,opt in [('baseline',False),('candidate',True)]:
                demo.reset_task(json.loads(reset)['task']);obs=demo.prepare_prefix(prefix);actual,_=align(expected,json.loads(state(obs)['audit.costNativeState']));assert not differences(expected,actual)
                obs=demo.design(opt)
                if kind=='outside':
                    obs=click(obs,'Btn_QuickSupport') if opt else click(click(obs,'Btn_JourneyHand'),'Btn_Support');obs=snap(obs,'outside_support');obs=click(obs,'Btn_BackSupport')
                else:obs=click(obs,'Btn_JourneyHand')
                if opt:assert any(c['name']=='Btn_DiscardOrdinaryEndFromHand' for c in obs['controls']),'New v7 assembly has not loaded'
                obs=snap(obs,{'outside':'outside_hand','empty':'empty_discard_hand','long':'long_hand_page_1'}[kind])
                if kind=='long':obs=click(obs,'Btn_NextHand');obs=snap(obs,'long_hand_page_2');continue
                if kind=='outside':
                    if opt:
                        # Capture the retained individual-selection workflow, then
                        # clear it before the explicit discard-all commit.
                        obs=click(obs,'Btn_SelectAllTurnDiscards');obs=snap(obs,'outside_hand_discard_selected');obs=click(obs,'Btn_SelectAllTurnDiscards')
                    else:
                        for name in [c['name'] for c in obs['controls'] if c['clickable'] and c['name'].startswith('Btn_Hand_')]:
                            obs=click(obs,name)
                            if any(c['name']=='Btn_DiscardAtEnd' and c['clickable'] for c in obs['controls']):obs=click(obs,'Btn_DiscardAtEnd')
                        obs=snap(obs,'outside_hand_discard_selected')
                    obs=click(obs,'Btn_DiscardOrdinaryEndFromHand') if opt else click(click(obs,'Btn_CloseHand'),'Btn_EndTurn');obs=snap(obs,'outside_after_discard_end')
                else:
                    obs=click(obs,'Btn_EndTurnFromHand') if opt else click(click(obs,'Btn_CloseHand'),'Btn_EndTurn');obs=snap(obs,'empty_discard_ended')
    finally:demo.close();write_json(out/'hand_commit_events.json',events)
    manifest=json.loads((previous/'paired_manifest.json').read_text(encoding='utf8'));pairs=[]
    for pair in manifest['pairs']:
        if pair['label'] in records['baseline']:continue
        for version in ('baseline','candidate'):
            src=Path(pair[version]);rec=json.loads(src.with_suffix('.json').read_text(encoding='utf8'))
            assert not any('/JourneyHand/' in c['path'] for c in rec['observation']['controls']) if version=='candidate' else True
            target=out/version/src.name;target.parent.mkdir(exist_ok=True);shutil.copy2(src,target);rec['screenshot']=str(target);rec['inherited_from']='v6_reward_coverage_checkpoint';write_json(target.with_suffix('.json'),rec);pair[version]=str(target)
        pair['inherited_from']='v6_reward_coverage_checkpoint';pairs.append(pair)
    for label,x in records['baseline'].items():
        y=records['candidate'][label];actual,_=align(x['native_state'],y['native_state']);diff=differences(x['native_state'],actual)
        if diff or x['discard_slots']!=y['discard_slots']:raise AssertionError((label,diff[:5]))
        pairs.append(dict(label=label,context=x['context'],baseline=x['screenshot'],candidate=y['screenshot'],same_native_state=True,hover_preview=False))
    write_json(out/'paired_manifest.json',dict(canvas=[1920,1080],pairs=pairs,errors=[],scope='v7 hand-only UI delta; all affected views recaptured; unchanged v6 views explicitly inherited',changed_source_files=changed))
    print(json.dumps(dict(pairs=len(pairs),new_pairs=len(records['baseline']),clicks=len(events))))
if __name__=='__main__':main()
