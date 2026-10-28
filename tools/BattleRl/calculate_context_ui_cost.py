"""Conservative offline v2 routes; no Unity timings or unmeasured zero fill."""
import csv,json,re,hashlib,argparse
from pathlib import Path
from collections import defaultdict
from iterate_ui_cost_design import ROOT,cost,synthetic
OUT=ROOT/'AutomationOutputs/UiFluency/design_review/v2_context_preserving'

def transform(row,profile='v2',context=None):
    if profile=='v7':
        previous,changes=transform(row,'v6',context)
        result=[];i=0
        while i<len(previous):
            s=previous[i]
            if s['name']=='Btn_CloseHand' and i+1<len(previous):
                following=previous[i+1]
                if following['name']=='Btn_EndTurnDiscardOrdinary' and following['intent']==s['intent']:
                    p=synthetic('Btn_DiscardOrdinaryEndFromHand',970,1001,435,56,s['intent'],'JourneyHand',following['after'])
                    p['path']='Canvas/BattleTable/JourneyHand/'+p['name'];result.append(p)
                    changes.append(dict(type='explicit_discard_ordinary_from_open_hand',intent=s['intent']));i+=2;continue
            s=dict(s)
            if '/JourneyHand/' in s['path'] and s['name'] in ('Btn_CloseHand','Btn_EndTurnFromHand','Btn_SelectAllTurnDiscards'):
                left={'Btn_CloseHand':1435,'Btn_EndTurnFromHand':505,'Btn_SelectAllTurnDiscards':40}[s['name']]
                s.update(x=left+217.5,y=51.,w=435.,h=56.)
            result.append(s);i+=1
        return result,changes
    original=row['steps']; out=[]; changes=[]; tray=False; battle=None; view=None
    if profile in ('v5','v6'):
        revised=[]; selected=None
        for ordinal,step in enumerate(original):
            step=dict(step,origin_ordinal=ordinal)
            m=re.fullmatch(r'Btn_Unit_(\d+)',step['name'])
            if m:selected=int(m.group(1))
            meta=context[ordinal] if context and ordinal<len(context) else None
            if step['name']=='Btn_AssignUnit' and selected is not None and meta and meta['phase']=='AssignDamage' and 0<meta['unit_count']<=4 and meta.get('unit_page') and meta['units_allowed']=='True':
                if revised and revised[-1]['name']=='Btn_Unit_'+str(selected) and revised[-1]['intent']==step['intent']:revised.pop()
                revised.append(synthetic('Btn_QuickAssignUnit_'+str(selected),1066,812+selected*56,288,48,step['intent'],step['before'],step['after']))
                changes.append(dict(type='explicit_unit_damage_toggle',ordinal=ordinal,unit=selected))
            else:revised.append(step)
        original=revised
    # Only replace a complete, understood mana subroute. Shared-skill tools
    # remain legacy; opening those still requires their original navigation.
    i=0
    while i<len(original):
        s=dict(original[i]); name=s['name']
        if profile=='v6' and name=='Btn_JourneyHand' and s['before']=='Outside' and i+1<len(original) and original[i+1]['name']=='Btn_Support' and original[i+1]['intent']==s['intent']:
            out.append(synthetic('Btn_QuickSupport',482,792,410,64,s['intent'],'Outside',original[i+1]['after']))
            changes.append(dict(type='direct_existing_support',ordinal=s.get('origin_ordinal')));i+=2;continue
        start=i
        if name=='Btn_Menu' and i+1<len(original) and original[i+1]['name']=='Btn_JourneyTools':
            opening=i+1; shell=True; return_view=s['before']
        elif name=='Btn_JourneyTools':
            opening=i; shell=s['before'].startswith('Battle.'); return_view=s['before']
            if s['before'] not in ('Outside','Battle.Hand','Battle.Units','Battle.Skills'):out.append(s);i+=1;continue
        else:out.append(s);i+=1;continue
        if profile in ('v3','v4') and not shell:
            out.append(s);i+=1;tray=False;continue
        j=opening+1
        while j<len(original) and original[j]['name']!='Btn_CloseTools' and '/JourneyTools/' in original[j]['path']:j+=1
        segment=original[opening+1:j]
        allowed=lambda x: bool(re.fullmatch(r'Btn_Source_\d+|Btn_SourceColor_\w+|Btn_UseSource|Btn_UseExtraSource',x['name']))
        direct=False;source_meta=None
        if profile=='v6' and segment and all(allowed(t) for t in segment):
            selected=next((t for t in segment if re.fullmatch(r'Btn_Source_\d+',t['name'])),None)
            if selected and context:
                source_meta=context[selected['origin_ordinal']]
                idx=int(selected['name'].rsplit('_',1)[1])
                direct=bool(source_meta and len(source_meta.get('source_faces',[]))>idx and source_meta['source_faces'][idx] in ('Red','Blue','Green','White','Gold'))
            # Formal source actions bind Option to the actual source face;
            # extraSource binds Targets to the face and Option to paid color.
            # This recovers only explicit immutable action arguments, never an
            # inferred face from click position or frequency.
            if selected and row.get('arguments'):
                arg=row['arguments'][selected['intent']];kind=arg['Kind']
                face_index=arg['Option'] if kind=='source' else arg['Targets'] if kind=='extraSource' else None
                if face_index is not None:
                    face=['Red','Blue','Green','White','Gold','Black'][face_index]
                    idx=int(selected['name'].rsplit('_',1)[1])
                    if source_meta and len(source_meta.get('source_faces',[]))>idx:assert source_meta['source_faces'][idx]==face,(row['key'],arg,source_meta)
                    direct=face!='Black'
        if profile=='v6' and (not direct or not any(t['name'] in ('Btn_UseSource','Btn_UseExtraSource') for t in segment)):
            if not shell:
                legacy=synthetic('Btn_SourceTacticDetails',1011,118,210,43,s['intent'],'Outside+SourceTray','JourneyTools')
                legacy['path']='Canvas/BattleTable/InlineSource/Btn_SourceTacticDetails';out.append(legacy);i=opening+1;continue
            out.append(s);i+=1;continue
        if not segment or not all(allowed(x) for x in segment) or not any(x['name'] in ('Btn_UseSource','Btn_UseExtraSource') for x in segment):
            out.append(s);i+=1;tray=False;continue
        # Retain one context-panel toggle; never classify it as no view change.
        if (not tray or battle!=shell) and not (profile=='v6' or profile=='v5' and shell):
            out.append(synthetic('Btn_QuickMana' if shell else 'Btn_JourneyTools',1430 if shell else 42,48 if shell else 872,215 if shell else 415,52 if shell else 64,s['intent'],return_view,return_view+'+SourceTray'))
        tray=True;battle=shell;view=return_view+'+SourceTray'; page=0
        x,y=(38,206) if shell else (995,108)
        for old in segment:
            n=old['name']; intent=old['intent']
            if n.startswith('Btn_Source_'):
                idx=int(n.rsplit('_',1)[1]); per=6 if shell or profile in ('v5','v6') else 12; cols=3 if shell else 6
                while page!=idx//per:
                    page_step=synthetic('Btn_SourcePage',x+(248 if shell else 690),y+12,82,43,intent,view,view)
                    page_step['path']='Canvas/BattleTable/InlineSource/Btn_SourcePage';out.append(page_step);page+=1
                slot=idx%per
                if profile=='v6' and any(t['name']=='Btn_UseExtraSource' for t in segment) and (not source_meta or source_meta.get('ordinary_used')!='True'):
                    mode=synthetic('Btn_InlineUseExtraSource',x+(207 if shell else 591),y+(292 if shell else 184),174,43,intent,view,view)
                    mode['path']='Canvas/BattleTable/InlineSource/'+mode['name'];out.append(mode)
                p=synthetic(('Btn_TakeSource_' if profile=='v6' else 'Btn_InlineSource_')+str(idx),x+16+slot%cols*128,y+(118 if shell else 66)+slot//cols*58,116,52,intent,view,view)
            elif n.startswith('Btn_SourceColor_'):
                color=n.rsplit('_',1)[1]; idx=['Red','Blue','Green','White','Gold','Black'].index(color)
                p=synthetic('Btn_InlineSourceColor_'+color,x+16+60*idx,y+(238 if shell else 184),56,47,intent,view,view)
            else:
                if profile=='v6':continue
                extra=n=='Btn_UseExtraSource'
                p=synthetic('Btn_InlineUseExtraSource' if extra else 'Btn_InlineUseSource',x+(16 if shell else 400)+191*extra,y+((292 if profile in ('v4','v5','v6') else 301) if shell else 184),174,43,intent,view,view)
            p['path']='Canvas/BattleTable/InlineSource/'+p['name'];out.append(p)
        i=j+int(j<len(original) and original[j]['name']=='Btn_CloseTools')
        changes.append(dict(type='inline_source',start=start,stop=i,battle_shell=shell))
    # Only battle End route with the original Inspector commit is supported.
    result=[]
    for intent,kind in [('A',row['ak']),('B',row['bk'])]:
        part=[x for x in out if x['intent']==intent]
        end=next((i for i,x in enumerate(part) if x['name']=='Btn_EndTurn' and (profile in ('v3','v4','v5','v6') or '/Inspector/' in x['path'])),None)
        if profile=='v6' and kind=='nextTurn' and end is not None and end>0 and part[end-1]['name']=='Btn_CloseHand' and not part[end].get('ordinary') and not part[end].get('discard'):
            result+=part[:end-1]
            result.append(synthetic('Btn_EndTurnFromHand',650,1001,590,56,intent,'JourneyHand',part[end]['after']))
            result[-1]['path']='Canvas/BattleTable/JourneyHand/Btn_EndTurnFromHand'
            result+=part[end+1:]
            changes.append(dict(type='empty_discard_end_from_open_hand',intent=intent,slots=[]));continue
        if kind!='nextTurn' or end is None or not part[end].get('ordinary'):result+=part;continue
        stop=part[end]
        if stop['ordinary']!=stop['discard']:raise AssertionError('ordered physical discard mismatch')
        allowed={'Btn_JourneyHand','Btn_DiscardAtEnd','Btn_NextHand','Btn_PreviousHand','Btn_CloseHand'}
        start=end
        while start and (part[start-1]['name'] in allowed or part[start-1]['name'].startswith('Btn_Hand_')):start-=1
        if start==end:result+=part;continue
        result+=part[:start]
        if profile in ('v4','v5','v6'):
            before=part[start]['before']; shell='/Inspector/' in stop['path']
            if before=='JourneyHand':
                result.append(synthetic('Btn_CloseHand',1280,1001,590,56,intent,before,'Battle.Hand' if shell else 'Outside'))
            result.append(synthetic('Btn_EndTurnDiscardOrdinary',1430 if shell else 1393,139 if shell else 872,215 if shell else 410,43 if shell else 64,intent,'Battle.Hand' if shell else 'Outside',stop['after']))
            result+=part[end+1:]
            changes.append(dict(type='explicit_discard_ordinary_and_end',intent=intent,slots=stop['discard']))
            continue
        if profile=='v3' and '/Inspector/' not in stop['path']:
            # The existing hand page gains batch selection and a formal commit;
            # retain its opening if needed, including any real prefix closes.
            if part[start]['before']!='JourneyHand':
                if part[start]['name']!='Btn_JourneyHand':
                    # An old macro may first close an already-open hand then
                    # reopen it. That close is unnecessary for the same view.
                    if part[start]['name']!='Btn_CloseHand':raise AssertionError(part[start]['name'])
                else:result.append(part[start])
            result.append(synthetic('Btn_SelectAllTurnDiscards',40,1001,590,56,intent,'JourneyHand','JourneyHand'))
            result.append(synthetic('Btn_EndTurnFromHand',650,1001,590,56,intent,'JourneyHand',stop['after']))
            result+=part[end+1:]
            changes.append(dict(type='exact_outside_discard_batch',intent=intent,slots=stop['discard']))
            continue
        before=part[start]['before']; mode='Battle.DiscardPlan'
        result.append(synthetic('Btn_PlanEndTurnDiscards',1430,139,450,43,intent,before,mode))
        result.append(synthetic('Btn_SelectAllTurnDiscards',1415,653,442,52,intent,mode,mode))
        result.append(dict(stop,before=mode));result+=part[end+1:]
        changes.append(dict(type='exact_all_ordinary_discard',intent=intent,slots=stop['discard']))
    if profile in ('v3','v4','v5','v6'):
        for step in result:
            if step['path']=='Canvas/BattleTable/JourneyHand/Btn_CloseHand':
                step.update(x=1575.,y=51.,w=590.,h=56.)
            if profile in ('v4','v5','v6') and step['path']=='Canvas/BattleTable/Btn_EndTurn':
                step.update(x=1158.,y=176.,w=410.,h=64.)
            if profile=='v6' and step['path']=='Canvas/BattleTable/Btn_JourneyHand':step.update(x=247.,y=256.,w=410.,h=64.)
    return result,changes

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--profile',choices=['v2','v3','v4','v5','v6','v7'],default='v2');parser.add_argument('--out',type=Path);args=parser.parse_args()
    global OUT
    if args.out:OUT=args.out.resolve()
    OUT.mkdir(parents=True,exist_ok=True)
    context_path=ROOT/'AutomationOutputs/UiFluency/design_review/route_public_context.jsonl'
    context={tuple(r['key']):r['steps'] for r in map(json.loads,context_path.open(encoding='utf8'))} if args.profile in ('v5','v6','v7') else {}
    argument_map={}
    if args.profile in ('v6','v7'):
        import sqlite3
        selected=sqlite3.connect('file:'+str(ROOT/'AutomationOutputs/UiFluency/20261012_sample5_selected/selected.sqlite').replace('\\','/')+'?mode=ro',uri=True)
        selected.execute('ATTACH DATABASE ? AS work',(str(ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite'),))
        for g,e,s,a,b in selected.execute('SELECT s.group_id,s.episode,s.a_step,a.arguments_json,b.arguments_json FROM samples s JOIN work.actions a ON a.id=s.a JOIN work.actions b ON b.id=s.b'):
            argument_map[(g,e,s)]={'A':json.loads(a),'B':json.loads(b)}
        with (OUT/'formal_action_arguments.jsonl').open('w',encoding='utf8') as f:
            for key,arg in argument_map.items():f.write(json.dumps(dict(key=key,arguments=arg),ensure_ascii=False)+'\n')
    buckets=defaultdict(lambda:defaultdict(float)); groups=defaultdict(lambda:defaultdict(float)); changes=defaultdict(int); regressions=0; count=0
    with (OUT/'calculated_routes.jsonl').open('w',encoding='utf8') as f:
        for line in (ROOT/'AutomationOutputs/UiFluency/ui_iteration/original_routes.jsonl').open(encoding='utf8'):
            row=json.loads(line)
            if args.profile in ('v6','v7'):row['arguments']=argument_map[tuple(row['key'])]
            new,ch=transform(row,args.profile,context.get(tuple(row['key']))); oldc=cost(row['steps']);newc=cost(new);w=row['weight'];count+=1
            for tag,c in [('baseline',oldc),('candidate',newc)]:
                for b in [buckets[tag],groups[(row['ak'],row['bk'],tag)]]:
                    b['weight']+=w;b['samples']+=1
                    for k,v in c.items():b[k]+=w*v
            for c in ch:changes[c['type']]+=1
            regressions+=newc['transition_px']>oldc['transition_px']+.01
            f.write(json.dumps(dict(key=row['key'],before=oldc,after=newc,steps=new,changes=ch,source_sha=row['source_sha']),ensure_ascii=False)+'\n')
    def norm(b):return {k:v if k in ('weight','samples') else v/b['weight'] for k,v in b.items()}
    result=dict(scope='offline geometry estimate, not executed timings or human behavior frequencies',routes=count,canvas=[1920,1080],versions={k:norm(v) for k,v in buckets.items()},changes=dict(changes),distance_regression_samples=regressions,unknown_costs='retain null; denominator applies only to previously accepted routes',cost_model_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest())
    result['profile']=args.profile
    if args.profile in ('v5','v6','v7'):result['public_context_sha256']=hashlib.sha256(context_path.read_bytes()).hexdigest()
    source_names=['PveBattleController.FluentUi.cs','PveBattleController.cs','PveBattleController.Journey.cs','PveBattleController.JourneyHand.cs','PveBattleController.DemoControls.cs','BattleCardHover.cs']
    result['design_source_sha256']={name:hashlib.sha256((ROOT/'Assets/Scripts/Battle'/name).read_bytes()).hexdigest() for name in source_names}
    (OUT/'cost_comparison.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
    import sqlite3
    db=sqlite3.connect('file:'+str(ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite').replace('\\','/')+'?mode=ro',uri=True)
    population=db.execute('select a.kind,b.kind,count(*) from transitions t join actions a on a.id=t.a_action join actions b on b.id=t.b_action group by a.kind,b.kind').fetchall()
    with (OUT/'all_transition_costs.csv').open('w',encoding='utf-8-sig',newline='') as f:
        wr=csv.writer(f);wr.writerow(['A','B','population','accepted_samples','accepted_population_weight','baseline_B_clicks','candidate_B_clicks','baseline_transition_px','candidate_transition_px','baseline_pair_panel_changes','candidate_pair_panel_changes'])
        for a,b,n in population:
            x=norm(groups[(a,b,'baseline')]);y=norm(groups[(a,b,'candidate')])
            wr.writerow([a,b,n,int(x.get('samples',0)),x.get('weight',0),x.get('b_clicks'),y.get('b_clicks'),x.get('transition_px'),y.get('transition_px'),x.get('panels'),y.get('panels')])
    print(json.dumps(result,ensure_ascii=False,indent=2))
if __name__=='__main__':main()
