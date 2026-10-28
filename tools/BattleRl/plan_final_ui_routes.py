"""Three remaining UI navigation plans. Rule commit buttons are never clicked."""
import argparse,hashlib,json,shutil,sqlite3,uuid,zipfile
from pathlib import Path
from capture_ui_design_pairs import CaptureDemo
from measure_sampled_ui_pairs import banner_use,skill_action,play,game_difference
from iterate_ui_cost_design import cost
from structured_ui_ppo import ROOT,state,write_json
from ui_cost_views import panel

class CommitBoundary(Exception):pass
def view(o):
    for name in ('MindResult','SpecialArtifact'):
        if any('/'+name+'/' in c['path'] and c['clickable'] for c in o['controls']):return name
    return panel(o)
def native(o):return json.loads(state(o)['audit.costNativeState'])
def named(o,n):
    cs=[c for c in o['controls'] if c['name']==n and c['clickable']];assert len(cs)==1,(n,len(cs));return cs[0]
def point(o,c,intent,after=None):return dict(name=c['name'],path=c['path'],x=c['x'],y=c['y'],w=c['width'],h=c['height'],intent=intent,before=view(o),after=after or view(o))

class UiOnly:
    def __init__(self,demo,terminal,intent):self.demo=demo;self.terminal=terminal;self.intent=intent;self.steps=[];self.boundary=None
    def __getattr__(self,name):return getattr(self.demo,name)
    def call(self,op,obs=None,c=None,**kw):
        if op=='click':
            if c['name']==self.terminal:self.boundary=(obs,c);raise CommitBoundary()
            name=c['name'];assert name.startswith(('Btn_Card_','Btn_Choice_','Btn_InputChoice_','Btn_InputColor_','Btn_Skill_','Btn_SkillChoice_','Btn_Unit_')) or name in ('Btn_Roster','Btn_Skills','Btn_NextSkill','Btn_HandPage','Btn_Target','Btn_MoreChoices','Btn_OpenSpecialArtifact') or (name=='Btn_Play' and self.terminal=='Btn_ConfirmInputs'),name
            before=native(obs);s=point(obs,c,self.intent);r=self.demo.call(op,obs,c,**kw)
            assert not game_difference(before,native(r['observation'])),'UI-only selection changed gameplay'
            s['after']=view(r['observation']);self.steps.append(s);return r
        assert op in ('observe','probe');return self.demo.call(op,obs,c,**kw)

def main():
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);a=p.parse_args();out=a.out.resolve();out.mkdir(parents=True,exist_ok=False)
    db=sqlite3.connect('file:'+str(ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite').replace('\\','/')+'?mode=ro',uri=True)
    gaps=list(map(json.loads,(out.parent/'uncovered_routes.jsonl').open(encoding='utf8')));demo=CaptureDemo(out/('live_'+uuid.uuid4().hex[:8]));results=[]
    def restore(prefix,opt):
        demo.reset_task(task);obs=demo.prepare_prefix(prefix);return demo.design(opt)
    def verify(expected,obs):
        errors=game_difference(expected,native(obs));assert not errors,errors[:5]
    def shot(obs,label):
        r=demo.call('observe',screenshot=True);target=out/(label+'.png');shutil.copy2(r['screenshotPath'],target);write_json(target.with_suffix('.json'),dict(observation=r['observation'],screenshot_sha256=hashlib.sha256(target.read_bytes()).hexdigest(),role='UI-only route planning; rule commit not clicked'));return r['observation']
    try:
        for eid,pos,terminal,fn in [(5386,70,'Btn_Fortitude',banner_use),(8519,54,'Btn_UseSkill',skill_action),(9022,70,'Btn_ConfirmInputs',play)]:
            sample=next(r for r in gaps if r['key'][1:]==[eid,pos]);path,volume,sha,reset=db.execute('select path,volume,sha256,reset_json from episodes where id=?',(eid,)).fetchone();task=json.loads(reset)['task']
            with zipfile.ZipFile('H:/MageKnight_RL_Backups/20261012_before_reward_v2/'+volume) as z:raw=z.read(path)
            assert hashlib.sha256(raw).hexdigest()==sha==sample['episode_source_sha256'];doc=json.loads(raw);t=doc['trace'];prefix=[r['action'] for r in doc.get('curriculum_prefix',[])]+[r['action'] for r in t[:pos]]
            for version,opt in [('baseline',False),('candidate',True)]:
                obs=restore(prefix,opt);verify(t[pos]['before'],obs);proxy=UiOnly(demo,terminal,'A')
                try:fn(proxy,obs,t[pos]['action'],[])
                except CommitBoundary:pass
                assert proxy.boundary is not None
                pre,control=proxy.boundary;pre=shot(pre,f'{eid}_{version}_A_boundary');steps=proxy.steps+[point(pre,control,'A')]
                obs=restore(prefix+[t[pos]['action']],opt);verify(t[pos+1]['before'],obs)
                ui=UiOnly(demo,'unused','B');supplement=[];bb=t[pos+1]['action']
                if eid==5386:
                    # Fortitude leaves the selected unit page open. Its single
                    # roster toggle is recorded from the observed boundary.
                    supplement.append(point(pre,named(pre,'Btn_Roster'),'B',panel(obs)))
                    card='Btn_Card_basic_card_021_0';obs=ui.call('click',obs,named(obs,card))['observation'];assert state(obs)['ui.cardMode']=='strong'
                    final=named(obs,'Btn_Play');replacement=dict(bb,Choice='strong')
                    obs=shot(obs,f'{eid}_{version}_B_boundary');check=restore(prefix+[t[pos]['action'],replacement],opt);verify(t[pos+1]['after'],check)
                    equivalence='Native NextCardEnhanced makes attack/strong option0 equivalent; substituted strong formal replay matches archived after-B.'
                elif eid==8519:
                    supplement.append(point(pre,named(pre,'Btn_Skills'),'B',panel(obs)))
                    # Post-skill target0 survives the UI's skills->hand toggle.
                    obs=demo.call('click',obs,next(c for c in obs['controls'] if '/Enemy_0/' in c['path'] and c['name']=='Btn_Target'))['observation'];verify(t[pos+1]['before'],obs)
                    for index in (0,2):
                        c=next(c for c in obs['controls'] if '/Enemy_'+str(index)+'/' in c['path'] and c['name']=='Btn_Target' and c['clickable']);obs=ui.call('click',obs,c)['observation']
                    final=named(obs,'Btn_KillCrystal');enemy=native(obs)['enemies'][2]
                    colors=[color for ability,color in [('FireResist','Red'),('IceResist','Blue'),('PhysicalResist','Green')] if ability in enemy['abilities']]+['White']
                    current=next(color for text,color in [('红色','Red'),('蓝色','Blue'),('绿色','Green'),('白色','White')] if text in final['text'])
                    clicks=(colors.index('White')-colors.index(current))%len(colors) or len(colors)
                    # Each cycle changes only the explicit reward choice. All
                    # projected points share one fixed button rectangle.
                    ui.steps += [point(obs,final,'B') for _ in range(clicks-1)]
                    equivalence='Clear the retained melee target before selecting enemy2; cycle to an explicit White entry, not a zero-cost assumed default.'
                    obs=shot(obs,f'{eid}_{version}_B_boundary');check=restore(prefix+[t[pos]['action'],bb],opt);verify(t[pos+1]['after'],check)
                else:
                    supplement.append(dict(name='Btn_CloseMindResult',path='Canvas/BattleTable/MindResult/Btn_CloseMindResult',x=955,y=52.5,w=1830,h=55,intent='B',before='MindResult',after=panel(obs)))
                    obs=ui.call('click',obs,named(obs,'Btn_Card_items_024_0'))['observation'];obs=ui.call('click',obs,named(obs,'Btn_OpenSpecialArtifact'))['observation'];final=named(obs,'Btn_ConfirmSpecial')
                    equivalence='Close the read-only mind-result page, select the box, open its existing special-artifact panel, then reveal.'
                    obs=shot(obs,f'{eid}_{version}_B_boundary');check=restore(prefix+[t[pos]['action'],bb],opt);verify(t[pos+1]['after'],check)
                # A final observations were recorded before restoring gameplay;
                # B boundary was observed before its formal replay as well.
                steps+=supplement+ui.steps+[point(obs,final,'B',view(obs) if eid==9022 else view(check))]
                results.append(dict(key=sample['key'],version=version,population_weight=sample['population_weight'],source_sha256=sha,status='offline_planned',steps=steps,cost=cost(steps),rule_commits_clicked=0,archived_after_A_equal=True,archived_after_B_equal=True,equivalence=equivalence))
                write_json(out/'planned_routes.json',dict(results=results,scope='Offline planned costs; UI selections observed, final commits replaced by independently restored formal states.'))
    finally:demo.close()
    print(json.dumps(dict(planned_versions=len(results),rule_commits_clicked=0)))
if __name__=='__main__':main()
