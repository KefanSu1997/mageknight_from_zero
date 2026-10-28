"""Supplement same-game-state evidence with the same selected physical card."""
import argparse,hashlib,json,shutil,sqlite3,uuid
from pathlib import Path
from capture_ui_design_pairs import CaptureDemo
from cost_entity_identity import align
from gate_unity_cost_prefix import differences
from structured_ui_ppo import ROOT,state,write_json

def main():
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);a=p.parse_args();out=a.out.resolve()
    db=sqlite3.connect('file:'+str(ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite').replace('\\','/')+'?mode=ro',uri=True)
    task=json.loads(db.execute('select reset_json from episodes where id=5131').fetchone()[0])['task']
    demo=CaptureDemo(out/('matched_selection_live_'+uuid.uuid4().hex[:8]));records={};label='outside_hand_discard_selected_matched'
    def click(obs,name):
        controls=[c for c in obs['controls'] if c['name']==name and c['clickable']];assert len(controls)==1,(name,len(controls))
        return demo.call('click',obs,controls[0])['observation']
    try:
        for version,opt in [('baseline',False),('candidate',True)]:
            demo.reset_task(task);obs=demo.design(opt);obs=click(obs,'Btn_JourneyHand')
            slots=sorted(int(c['name'][9:]) for c in obs['controls'] if c['clickable'] and c['name'].startswith('Btn_Hand_'))
            assert slots
            if opt:obs=click(obs,'Btn_SelectAllTurnDiscards')
            else:
                for slot in slots:
                    obs=click(obs,'Btn_Hand_'+str(slot))
                    if any(c['name']=='Btn_DiscardAtEnd' and c['clickable'] for c in obs['controls']):obs=click(obs,'Btn_DiscardAtEnd')
            # This UI-only selection is for a matched visual comparison, not an
            # added step in the direct discard-all route or a gameplay test.
            obs=click(obs,'Btn_Hand_'+str(slots[-1]));r=demo.call('observe',screenshot=True);obs=r['observation'];s=state(obs)
            assert s['ui.hoverPreviewVisible']=='False'
            target=out/version/(label+'.png');assert not target.exists();shutil.copy2(r['screenshotPath'],target)
            rec=dict(label=label,version=version,context=dict(episode=5131,step=0,role='matched_ui_only_selection_not_cost_sample'),screenshot=str(target),screenshot_sha256=hashlib.sha256(target.read_bytes()).hexdigest(),native_state=json.loads(s['audit.costNativeState']),discard_slots=json.loads(s['audit.costFixture']).get('turnFlow',{}).get('discardSlots',[]),hover_preview=False,observation=obs,selected_physical_hand_slot=slots[-1])
            write_json(target.with_suffix('.json'),rec);records[version]=rec
    finally:demo.close()
    x,y=records['baseline'],records['candidate'];actual,_=align(x['native_state'],y['native_state']);assert not differences(x['native_state'],actual);assert x['discard_slots']==y['discard_slots'];assert x['selected_physical_hand_slot']==y['selected_physical_hand_slot']
    mp=out/'paired_manifest.json';m=json.loads(mp.read_text(encoding='utf8'))
    for pair in m['pairs']:
        if pair['label']=='outside_hand_discard_selected':pair['comparison_limit']='Native state/discards match; selected card differs. Use outside_hand_discard_selected_matched for selected-card information comparison.'
    assert label not in {r['label'] for r in m['pairs']}
    m['pairs'].append(dict(label=label,context=x['context'],baseline=x['screenshot'],candidate=y['screenshot'],same_native_state=True,same_selected_physical_hand_slot=True,hover_preview=False));write_json(mp,m)
    print(json.dumps(dict(added_pairs=1,physical_slot=x['selected_physical_hand_slot'],cost_route_unchanged=True)))
if __name__=='__main__':main()
