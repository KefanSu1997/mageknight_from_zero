"""Actual archived eight-card hand, both pages at unchanged card dimensions."""
import argparse,hashlib,json,shutil,sqlite3,uuid,zipfile
from pathlib import Path
from capture_ui_design_pairs import CaptureDemo
from cost_entity_identity import align
from gate_unity_cost_prefix import differences
from structured_ui_ppo import ROOT,state,write_json

def main():
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);a=p.parse_args();out=a.out.resolve()
    d=sqlite3.connect('file:'+str(ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite').replace('\\','/')+'?mode=ro',uri=True)
    path,volume,sha,reset=d.execute('select path,volume,sha256,reset_json from episodes where id=14617').fetchone()
    with zipfile.ZipFile('H:/MageKnight_RL_Backups/20261012_before_reward_v2/'+volume) as z:raw=z.read(path)
    assert hashlib.sha256(raw).hexdigest()==sha
    doc=json.loads(raw);pos=20;expected=doc['trace'][pos]['before'];prefix=[r['action'] for r in doc.get('curriculum_prefix',[])]+[r['action'] for r in doc['trace'][:pos]]
    demo=CaptureDemo(out/('hand_live_'+uuid.uuid4().hex[:8]));records={}
    def click(obs,name):
        c=[c for c in obs['controls'] if c['name']==name and c['clickable']];assert len(c)==1,(name,len(c))
        return demo.call('click',obs,c[0])['observation']
    try:
        for version,opt in [('baseline',False),('candidate',True)]:
            demo.reset_task(json.loads(reset)['task']);obs=demo.prepare_prefix(prefix);actual,_=align(expected,json.loads(state(obs)['audit.costNativeState']));assert not differences(expected,actual)
            obs=demo.design(opt);obs=click(obs,'Btn_JourneyHand');records[version]={}
            for page in (1,2):
                if page==2:obs=click(obs,'Btn_NextHand')
                r=demo.call('observe',screenshot=True);obs=r['observation'];s=state(obs);assert s['ui.hoverPreviewVisible']=='False'
                label='long_hand_page_'+str(page);target=out/version/(label+'.png');assert not target.exists();shutil.copy2(r['screenshotPath'],target)
                rec=dict(label=label,version=version,context=dict(episode=14617,step=20,archive=volume,path=path,source_sha256=sha),screenshot=str(target),screenshot_sha256=hashlib.sha256(target.read_bytes()).hexdigest(),native_state=json.loads(s['audit.costNativeState']),discard_slots=json.loads(s['audit.costFixture']).get('turnFlow',{}).get('discardSlots',[]),hover_preview=False,observation=obs)
                write_json(target.with_suffix('.json'),rec);records[version][label]=rec
    finally:demo.close()
    mpath=out/'paired_manifest.json';m=json.loads(mpath.read_text(encoding='utf8'));existing={r['label'] for r in m['pairs']}
    for label,x in records['baseline'].items():
        y=records['candidate'][label];actual,_=align(x['native_state'],y['native_state']);assert not differences(x['native_state'],actual);assert x['discard_slots']==y['discard_slots'];assert label not in existing
        m['pairs'].append(dict(label=label,context=x['context'],baseline=x['screenshot'],candidate=y['screenshot'],same_native_state=True,hover_preview=False))
    write_json(mpath,m);print('Added 2 long-hand pairs')
if __name__=='__main__':main()
