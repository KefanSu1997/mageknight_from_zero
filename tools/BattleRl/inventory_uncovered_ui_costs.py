"""Prioritize every missing sampled route without imputing its click cost."""
import argparse,csv,json,sqlite3
from collections import defaultdict
from pathlib import Path
from structured_ui_ppo import ROOT,write_json

def main():
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);a=p.parse_args();out=a.out.resolve()
    db=sqlite3.connect('file:'+str(ROOT/'AutomationOutputs/UiFluency/20261012_sample5_selected/selected.sqlite').replace('\\','/')+'?mode=ro',uri=True)
    db.execute('attach database ? as work',(str(ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite'),))
    population={(a,b):n for a,b,n in db.execute('select a.kind,b.kind,count(*) from work.transitions t join work.actions a on a.id=t.a_action join work.actions b on b.id=t.b_action group by a.kind,b.kind')}
    rows=[];groups=defaultdict(lambda:dict(missing_population_weight=0.,samples=0,statuses=defaultdict(int)))
    query='select s.group_id,s.episode,s.a_step,s.status,s.estimator_weight,a.kind,b.kind,a.arguments_json,b.arguments_json,s.a_phase,s.b_phase,e.sha256 from samples s join work.actions a on a.id=s.a join work.actions b on b.id=s.b join work.episodes e on e.id=s.episode left join costs c using(group_id,episode,a_step) where c.pair_clicks is null'
    for group,e,step,status,weight,ak,bk,aj,bj,ap,bp,sha in db.execute(query):
        scaled=population[(ak,bk)]*weight
        rows.append(dict(key=[group,e,step],A=ak,B=bk,status=status,population_weight=scaled,action_A=json.loads(aj),action_B=json.loads(bj),phase_A=ap,phase_B=bp,episode_source_sha256=sha,baseline_cost=None,candidate_cost=None))
        g=groups[(ak,bk)];g['missing_population_weight']+=scaled;g['samples']+=1;g['statuses'][status]+=1
    rows.sort(key=lambda r:-r['population_weight'])
    with (out/'uncovered_routes.jsonl').open('w',encoding='utf8') as f:
        for r in rows:f.write(json.dumps(r,ensure_ascii=False)+'\n')
    ranked=[dict(A=ak,B=bk,**g) for (ak,bk),g in groups.items()];ranked.sort(key=lambda r:-r['missing_population_weight'])
    total=sum(r['population_weight'] for r in rows);assert abs(total-15073.75)<.01
    write_json(out/'uncovered_priority.json',dict(samples=len(rows),population_weight=total,groups=ranked,policy='Offline route planning only; incompatibility and missing adapters stay explicit. No cost imputation or new gameplay simulation.'))
    print(json.dumps(dict(samples=len(rows),population_weight=total,top=ranked[:8]),ensure_ascii=False))
if __name__=='__main__':main()
