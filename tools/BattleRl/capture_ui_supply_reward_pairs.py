"""Actual unit supply and mandatory level reward views, unchanged game state."""
import argparse,hashlib,json,shutil,sqlite3,uuid,zipfile
from pathlib import Path
from capture_ui_design_pairs import CaptureDemo
from cost_entity_identity import align
from gate_unity_cost_prefix import differences
from structured_ui_ppo import ROOT,state,write_json

def main():
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);a=p.parse_args();out=a.out.resolve()
    d=sqlite3.connect('file:'+str(ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite').replace('\\','/')+'?mode=ro',uri=True)
    demo=CaptureDemo(out/('supply_reward_live_'+uuid.uuid4().hex[:8]));records={};events=[]
    def click(obs,name):
        c=[c for c in obs['controls'] if c['name']==name and c['clickable']];assert len(c)==1,(name,len(c))
        events.append(dict(version=version,label=label,name=name));return demo.call('click',obs,c[0])['observation']
    def snap(obs,name):
        r=demo.call('observe',screenshot=True);obs=r['observation'];s=state(obs);assert s['ui.hoverPreviewVisible']=='False'
        target=out/version/(name+'.png');assert not target.exists();shutil.copy2(r['screenshotPath'],target)
        rec=dict(label=name,version=version,context=context,screenshot=str(target),screenshot_sha256=hashlib.sha256(target.read_bytes()).hexdigest(),native_state=json.loads(s['audit.costNativeState']),discard_slots=json.loads(s['audit.costFixture']).get('turnFlow',{}).get('discardSlots',[]),hover_preview=False,observation=obs)
        write_json(target.with_suffix('.json'),rec);records.setdefault(version,{})[name]=rec;return obs
    try:
        for label,eid,pos in [('supply',5131,0),('level_reward',35,11)]:
            path,volume,sha,reset=d.execute('select path,volume,sha256,reset_json from episodes where id=?',(eid,)).fetchone()
            with zipfile.ZipFile('H:/MageKnight_RL_Backups/20261012_before_reward_v2/'+volume) as z:raw=z.read(path)
            assert hashlib.sha256(raw).hexdigest()==sha
            doc=json.loads(raw);expected=doc['trace'][pos]['before'];prefix=[r['action'] for r in doc.get('curriculum_prefix',[])]+[r['action'] for r in doc['trace'][:pos]]
            context=dict(episode=eid,step=pos,archive=volume,path=path,source_sha256=sha)
            for version,opt in [('baseline',False),('candidate',True)]:
                demo.reset_task(json.loads(reset)['task']);obs=demo.prepare_prefix(prefix);actual,_=align(expected,json.loads(state(obs)['audit.costNativeState']));diff=differences(expected,actual)
                if diff:raise AssertionError(dict(episode=eid,archived_differences=diff[:5]))
                obs=demo.design(opt)
                if label=='supply':
                    obs=click(obs,'Btn_JourneyInteraction');obs=snap(obs,'unit_supply')
                    offers=[c['name'] for c in obs['controls'] if c['name'].startswith('Btn_Offer_') and c['clickable']]
                    assert offers;obs=click(obs,offers[0]);obs=snap(obs,'unit_supply_selected')
                    if any(c['name']=='Btn_NextOffer' and c['clickable'] for c in obs['controls']):obs=click(obs,'Btn_NextOffer');obs=snap(obs,'unit_supply_page_2')
                else:
                    obs=click(obs,'Btn_EndTurn');assert state(obs)['journey.endingTurn']=='True';obs=snap(obs,'level_reward_required')
                    obs=click(obs,'Btn_RewardSkill_0');obs=click(obs,'Btn_RewardAction_0');obs=snap(obs,'level_reward_selected')
                    obs=click(obs,'Btn_ConfirmReward');obs=snap(obs,'level_reward_received')
    finally:demo.close();write_json(out/'supply_reward_events.json',events)
    mpath=out/'paired_manifest.json';m=json.loads(mpath.read_text(encoding='utf8'));existing={r['label'] for r in m['pairs']}
    assert records['baseline'].keys()==records['candidate'].keys()
    for name,x in records['baseline'].items():
        y=records['candidate'][name];actual,_=align(x['native_state'],y['native_state']);diff=differences(x['native_state'],actual)
        if diff or x['discard_slots']!=y['discard_slots']:raise AssertionError((name,diff[:5]))
        assert name not in existing;m['pairs'].append(dict(label=name,context=x['context'],baseline=x['screenshot'],candidate=y['screenshot'],same_native_state=True,hover_preview=False))
    write_json(mpath,m);print(json.dumps(dict(added_pairs=len(records['baseline']),clicks=len(events))))
if __name__=='__main__':main()
