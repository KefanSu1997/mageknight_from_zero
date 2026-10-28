"""Archived source/payment and empty-discard UI equivalence evidence."""
import argparse,hashlib,json,shutil,sqlite3,uuid,zipfile
from pathlib import Path
from capture_ui_design_pairs import CaptureDemo
from cost_entity_identity import align
from gate_unity_cost_prefix import differences
from structured_ui_ppo import ROOT,state,write_json

def main():
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);a=p.parse_args();out=a.out.resolve()
    db=sqlite3.connect('file:'+str(ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite').replace('\\','/')+'?mode=ro',uri=True)
    demo=CaptureDemo(out/('edge_live_'+uuid.uuid4().hex[:8]));records={};events=[];checks=[]
    def click(obs,name):
        matches=[c for c in obs['controls'] if c['name']==name and c['clickable']]
        if len(matches)!=1:raise ValueError((name,len(matches)))
        events.append(dict(version=version,label=label,name=name));return demo.call('click',obs,matches[0])['observation']
    def verify(obs,expected,stage):
        actual,_=align(expected,json.loads(state(obs)['audit.costNativeState']));diff=differences(expected,actual)
        if diff:raise AssertionError(dict(stage=stage,differences=diff[:5]))
        checks.append(dict(version=version,stage=stage,archived_native_equal=True))
    def snap(obs,name):
        r=demo.call('observe',screenshot=True);obs=r['observation'];s=state(obs)
        if s.get('ui.hoverPreviewVisible')!='False':raise AssertionError('Unexpected hover')
        target=out/version/(name+'.png')
        if target.exists():raise FileExistsError(target)
        shutil.copy2(r['screenshotPath'],target);fixture=json.loads(s['audit.costFixture'])
        rec=dict(label=name,version=version,context=context,screenshot=str(target),screenshot_sha256=hashlib.sha256(target.read_bytes()).hexdigest(),native_state=json.loads(s['audit.costNativeState']),discard_slots=fixture.get('turnFlow',{}).get('discardSlots',[]),hover_preview=False,observation=obs)
        write_json(target.with_suffix('.json'),rec);records.setdefault(version,{})[name]=rec;return obs
    try:
        for label,eid,pos in [('gold_payment',14054,4),('black_extra',3854,29),('empty_discard',5092,13)]:
            path,volume,sha,reset=db.execute('select path,volume,sha256,reset_json from episodes where id=?',(eid,)).fetchone()
            with zipfile.ZipFile('H:/MageKnight_RL_Backups/20261012_before_reward_v2/'+volume) as z:raw=z.read(path)
            assert hashlib.sha256(raw).hexdigest()==sha
            doc=json.loads(raw);trace=doc['trace'];prefix=[r['action'] for r in doc.get('curriculum_prefix',[])]+[r['action'] for r in trace[:pos]]
            context=dict(episode=eid,step=pos,archive=volume,path=path,source_sha256=sha)
            for version,opt in [('baseline',False),('candidate',True)]:
                demo.reset_task(json.loads(reset)['task']);obs=demo.prepare_prefix(prefix);verify(obs,trace[pos]['before'],label+'_before');obs=demo.design(opt)
                if label=='empty_discard':
                    obs=click(obs,'Btn_JourneyHand');obs=snap(obs,label+'_hand')
                    obs=click(obs,'Btn_EndTurnFromHand') if opt else click(click(obs,'Btn_CloseHand'),'Btn_EndTurn')
                    obs=snap(obs,label+'_ended');continue
                obs=snap(obs,label+'_before');faces=state(obs)['source.faces'].split(',');face='Gold' if label=='gold_payment' else 'Black';idx=faces.index(face)
                if not opt:obs=click(click(obs,'Btn_Menu'),'Btn_JourneyTools')
                if label=='black_extra' and opt:
                    # Explicit extra-mode selection; black still requires color and confirmation.
                    obs=click(obs,'Btn_InlineUseExtraSource')
                obs=click(obs,('Btn_TakeSource_' if opt else 'Btn_Source_')+str(idx))
                if label=='black_extra':
                    obs=snap(obs,label+'_selected')
                    obs=click(obs,('Btn_InlineSourceColor_' if opt else 'Btn_SourceColor_')+'Gold');obs=snap(obs,label+'_color')
                if not opt or label=='black_extra':
                    obs=click(obs,('Btn_InlineUseExtraSource' if opt else 'Btn_UseExtraSource') if label=='black_extra' else 'Btn_UseSource')
                    if not opt:obs=click(obs,'Btn_CloseTools')
                verify(obs,trace[pos+1]['before'],label+'_taken');obs=snap(obs,label+'_taken')
                if label=='gold_payment':
                    for offset in (1,2):
                        action=trace[pos+offset]['action'];print('Formal payment action',offset,json.dumps(action),flush=True)
                        obs=click(obs,'Btn_Card_'+action['Card']+'_0')
                        if action['Kind']=='sideways':obs=click(obs,'Btn_Sideways')
                        else:obs=click(click(obs,'Btn_Choice_'+action['Choice']),'Btn_Play')
                        verify(obs,trace[pos+offset+1]['before'],label+'_action_'+str(offset));obs=snap(obs,label+'_action_'+str(offset))
    finally:
        demo.close();write_json(out/'edge_events.json',dict(clicks=events,checks=checks))
    added=[]
    assert records['baseline'].keys()==records['candidate'].keys()
    for name,x in records['baseline'].items():
        y=records['candidate'][name];actual,_=align(x['native_state'],y['native_state']);diff=differences(x['native_state'],actual)
        if diff or x['discard_slots']!=y['discard_slots']:raise AssertionError((name,diff[:5]))
        added.append(dict(label=name,context=x['context'],baseline=x['screenshot'],candidate=y['screenshot'],same_native_state=True,hover_preview=False))
    manifest_path=out/'paired_manifest.json';m=json.loads(manifest_path.read_text(encoding='utf8'))
    assert not {r['label'] for r in m['pairs']} & {r['label'] for r in added}
    m['pairs']+=added;write_json(manifest_path,m);print(json.dumps(dict(added_pairs=len(added),checks=len(checks),clicks=len(events))))
if __name__=='__main__':main()
