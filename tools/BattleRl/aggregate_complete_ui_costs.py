"""Join immutable accepted routes and separately labelled residual evidence."""
import argparse,csv,hashlib,json,sqlite3
from collections import defaultdict
from pathlib import Path
from structured_ui_ppo import ROOT,write_json

def main():
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);out=p.parse_args().out.resolve()
    original={tuple(r['key']):r for r in map(json.loads,(ROOT/'AutomationOutputs/UiFluency/ui_iteration/original_routes.jsonl').open(encoding='utf8'))}
    groups=defaultdict(lambda:dict(samples=0,finite_weight=0.,unavailable_weight=0.,unresolved_weight=0.,baseline=defaultdict(float),candidate=defaultdict(float)))
    evidence=defaultdict(lambda:dict(samples=0,weight=0.));metrics=['b_clicks','transition_px']
    def add(a,b,w,tag,old=None,new=None):
        g=groups[(a,b)];g['samples']+=1;evidence[tag]['samples']+=1;evidence[tag]['weight']+=w
        if old is not None:
            g['finite_weight']+=w
            for v,c in [('baseline',old),('candidate',new)]:
                for k in metrics:g[v][k]+=w*c[k]
        else:g['unresolved_weight' if tag=='unresolved_not_imputed' else 'unavailable_weight']+=w
    for r in map(json.loads,(out/'calculated_routes.jsonl').open(encoding='utf8')):
        source=original[tuple(r['key'])];add(source['ak'],source['bk'],source['weight'],'accepted_pointer_route_geometry',r['before'],r['after'])
    residual_path=out/'uncovered_complete_resolutions.json';residual=json.loads(residual_path.read_text(encoding='utf8'))
    for r in residual['rows']:add(r['A'],r['B'],r['population_weight'],r['resolution'],r['baseline_cost'],r['candidate_cost'])
    db=sqlite3.connect('file:'+str(ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite').replace('\\','/')+'?mode=ro',uri=True)
    population=db.execute('select a.kind,b.kind,count(*) from transitions t join actions a on a.id=t.a_action join actions b on b.id=t.b_action group by a.kind,b.kind').fetchall()
    total=dict(population=0,finite_weight=0.,unavailable_weight=0.,unresolved_weight=0.)
    sums={v:defaultdict(float) for v in ['baseline','candidate']}
    rows=[]
    for a,b,n in population:
        g=groups[(a,b)];assert abs(g['finite_weight']+g['unavailable_weight']+g['unresolved_weight']-n)<.01,(a,b,n,g)
        record=dict(A=a,B=b,population=n,sampled_routes=g['samples'],finite_weight=g['finite_weight'],unavailable_weight=g['unavailable_weight'],unresolved_weight=g['unresolved_weight'])
        record['status']='unresolved' if g['unresolved_weight'] else ('finite_and_unavailable' if g['finite_weight'] and g['unavailable_weight'] else 'finite' if g['finite_weight'] else 'unavailable_no_finite_completion_cost')
        for v in sums:
            for k in metrics:
                record[v+'_'+k]=g[v][k]/g['finite_weight'] if g['finite_weight'] else None
                sums[v][k]+=g[v][k]
        rows.append(record);total['population']+=n
        for k in ['finite_weight','unavailable_weight','unresolved_weight']:total[k]+=g[k]
    assert len(rows)==1009 and total['population']==850374
    versions={v:{k:sums[v][k]/total['finite_weight'] for k in metrics} for v in sums}
    reductions={k:1-versions['candidate'][k]/versions['baseline'][k] for k in metrics}
    result=dict(scope='Finite completion cost conditional on UI-completable classified samples; unavailable and unresolved remain in coverage accounting and never receive zero cost.',**total,versions=versions,reductions=reductions,evidence=dict(evidence),full_population_finite_mean_defined=False,goal_complete=False,missing_costs_imputed=False,panel_comparison='Excluded from this merged metric: old view classifier omits MindResult/SpecialArtifact. Screen-count constraint needs source inventory and screenshots, independently.',source_residual_sha256=hashlib.sha256(residual_path.read_bytes()).hexdigest())
    write_json(out/'complete_cost_summary.json',result)
    with (out/'complete_transition_costs.csv').open('w',encoding='utf-8-sig',newline='') as f:
        wr=csv.DictWriter(f,fieldnames=list(rows[0]));wr.writeheader();wr.writerows(rows)
    print(json.dumps(result,ensure_ascii=False))

if __name__=='__main__':main()
