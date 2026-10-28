"""Offline completion routes from restored UI states; never click rule commits.

Only UI selections/paging run. A/B rule transitions are restored using archived
formal prefixes, independently checked, and not counted as pointer execution.
"""
import argparse,hashlib,json,shutil,sqlite3,uuid,zipfile
from pathlib import Path
from capture_ui_design_pairs import CaptureDemo
from cost_entity_identity import align
from gate_unity_cost_prefix import differences
from iterate_ui_cost_design import cost
from structured_ui_ppo import ROOT,state,write_json
from ui_cost_views import panel

def main():
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);a=p.parse_args();out=a.out.resolve();out.mkdir(parents=True,exist_ok=False)
    candidates=list(map(json.loads,(ROOT/'AutomationOutputs/UiFluency/design_review/v7_hand_commit/uncovered_routes.jsonl').open(encoding='utf8')))
    wanted=[r for r in candidates if r['status']=='needs_adapter' and r['key'][1] in (15299,233) and r['B']=='play']
    assert len(wanted)==2
    db=sqlite3.connect('file:'+str(ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite').replace('\\','/')+'?mode=ro',uri=True)
    demo=CaptureDemo(out/('live_'+uuid.uuid4().hex[:8]));results=[]
    def native(obs):return json.loads(state(obs)['audit.costNativeState'])
    def verify(expected,obs):
        actual,_=align(expected,native(obs));diff=differences(expected,actual)
        if diff:raise AssertionError(diff[:5])
    def control(obs,name):
        cs=[c for c in obs['controls'] if c['name']==name and c['clickable']];assert len(cs)==1,(name,len(cs))
        return cs[0]
    def step(obs,name,intent,after=None):
        c=control(obs,name)
        return dict(name=name,path=c['path'],x=c['x'],y=c['y'],w=c['width'],h=c['height'],intent=intent,before=panel(obs),after=after or panel(obs))
    def select(obs,name,intent,steps):
        assert name.startswith(('Btn_Card_','Btn_Choice_')) or name=='Btn_HandPage',name
        before=native(obs);s=step(obs,name,intent);obs=demo.call('click',obs,control(obs,name))['observation'];verify(before,obs)
        s['after']=panel(obs);steps.append(s);return obs
    def select_card(obs,card,intent,steps):
        n=native(obs);slot=n['hand'].index(card);desired=slot//7
        pages=max(1,(len(n['hand'])+6)//7)
        for _ in range(pages):
            name='Btn_Card_'+card+'_'+str(n['hand'][:slot].count(card))
            if any(c['name']==name and c['clickable'] for c in obs['controls']):return select(obs,name,intent,steps)
            obs=select(obs,'Btn_HandPage',intent,steps)
        raise AssertionError(('Card not visible',card,desired))
    def shot(obs,name):
        r=demo.call('observe',screenshot=True);obs=r['observation'];dest=out/(name+'.png');shutil.copy2(r['screenshotPath'],dest)
        write_json(dest.with_suffix('.json'),dict(observation=obs,screenshot_sha256=hashlib.sha256(dest.read_bytes()).hexdigest(),role='offline_plan_UI_state_not_gameplay_execution'))
        return obs
    try:
        for sample in wanted:
            _,eid,pos=sample['key'];path,volume,sha,reset=db.execute('select path,volume,sha256,reset_json from episodes where id=?',(eid,)).fetchone()
            with zipfile.ZipFile('H:/MageKnight_RL_Backups/20261012_before_reward_v2/'+volume) as z:raw=z.read(path)
            assert hashlib.sha256(raw).hexdigest()==sha==sample['episode_source_sha256']
            doc=json.loads(raw);trace=doc['trace'];prefix=[r['action'] for r in doc.get('curriculum_prefix',[])]+[r['action'] for r in trace[:pos]]
            for version,opt in [('baseline',False),('candidate',True)]:
                steps=[];demo.reset_task(json.loads(reset)['task']);obs=demo.prepare_prefix(prefix);verify(trace[pos]['before'],obs);obs=demo.design(opt);obs=shot(obs,f'{eid}_{version}_before_A')
                aa,bb=sample['action_A'],sample['action_B'];assert aa=={k:v for k,v in trace[pos]['action'].items() if k!='Key'}
                if aa['Kind']=='fight':
                    name='Btn_JourneyEncounter_'+str(native(obs)['taskProgress']['encounterIndex'])
                    if not any(c['name']==name and c['clickable'] for c in obs['controls']):
                        # The original-map overview intentionally replaces the
                        # off-map declared-task encounter buttons. Restoring a
                        # legal native fight cannot create a UI completion cost.
                        assert state(obs).get('ui.offMapBattles')=='0'
                        demo.reset_task(json.loads(reset)['task']);obs=demo.prepare_prefix(prefix+[trace[pos]['action']]);verify(trace[pos+1]['before'],obs);obs=demo.design(opt);obs=shot(obs,f'{eid}_{version}_native_A_only')
                        results.append(dict(key=sample['key'],version=version,population_weight=sample['population_weight'],source_sha256=sha,cost=None,status='unavailable_current_UI',reason='Original map overview hides off-map declared-task fight; native BeginBattle exists, encounter control absent',rule_commits_clicked=0,archived_before_after_equal=True))
                        write_json(out/'planned_routes.json',dict(results=results));continue
                    last=step(obs,name,'A')
                elif aa['Kind']=='sideways':
                    obs=select_card(obs,aa['Card'],'A',steps);last=step(obs,'Btn_Sideways','A')
                else:raise AssertionError(aa)
                # Restore the formal A result instead of clicking its commit.
                demo.reset_task(json.loads(reset)['task']);obs=demo.prepare_prefix(prefix+[trace[pos]['action']]);verify(trace[pos+1]['before'],obs);obs=demo.design(opt);last['after']=panel(obs);steps.append(last);obs=shot(obs,f'{eid}_{version}_after_A')
                # These two fixtures stay on hand page zero, making the carried
                # post-A UI context exact without inventing a page restoration.
                assert len(trace[pos]['before']['hand'])<=7 and len(native(obs)['hand'])<=7
                assert bb['Input']=='' and bb['Parts']==bb['Option']==bb['Targets']==0
                assert bb['Card'] in ('basic_card_019','basic_card_022')
                bindings=json.loads(state(obs)['audit.costFixture'])['cardChoices'];host=next(x for x in bindings if x['id']==bb['Card']);choice=next(x for x in host['choices'] if x['id']==bb['Choice']);assert choice['target']=='None'
                obs=select_card(obs,bb['Card'],'B',steps)
                mode='Btn_Choice_'+('strong' if choice['enhanced'] else 'base')
                if '●' not in control(obs,mode)['text']:obs=select(obs,mode,'B',steps)
                if bb['Choice'] not in ('base','strong'):
                    name='Btn_Choice_'+bb['Choice']
                    if '●' not in control(obs,name)['text']:obs=select(obs,name,'B',steps)
                obs=shot(obs,f'{eid}_{version}_before_B_commit');last=step(obs,'Btn_Play','B')
                # PveDeedInputs.Supports excludes both hosts. Their normal Play
                # handler performs a direct formal play; no input dialog omitted.
                demo.reset_task(json.loads(reset)['task']);obs=demo.prepare_prefix(prefix+[trace[pos]['action'],trace[pos+1]['action']]);verify(trace[pos+2]['before'],obs);obs=demo.design(opt);last['after']=panel(obs);steps.append(last)
                results.append(dict(key=sample['key'],version=version,population_weight=sample['population_weight'],source_sha256=sha,steps=steps,cost=cost(steps),status='offline_planned',rule_commits_clicked=0,archived_before_after_equal=True,scope='Planned completion cost; UI geometry and non-mutating selections observed, gameplay completion not pointer-executed'))
                write_json(out/'planned_routes.json',dict(results=results))
    finally:demo.close()
    print(json.dumps(dict(planned_versions=len(results),population_weight=sum(r['population_weight'] for r in results if r['version']=='baseline'),rule_commits_clicked=0)))
if __name__=='__main__':main()
