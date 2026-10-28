"""Read-only archive search for actual pre-End level reward opportunities."""
import hashlib,json,sqlite3,zipfile
from pathlib import Path
from structured_ui_ppo import ROOT,write_json

def main():
    db=sqlite3.connect('file:'+str(ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite').replace('\\','/')+'?mode=ro',uri=True)
    archives={};found=[];inspected=0
    try:
        for eid,path,volume,sha in db.execute('select id,path,volume,sha256 from episodes order by id'):
            if volume not in archives:archives[volume]=zipfile.ZipFile('H:/MageKnight_RL_Backups/20261012_before_reward_v2/'+volume)
            raw=archives[volume].read(path);assert hashlib.sha256(raw).hexdigest()==sha
            doc=json.loads(raw);inspected+=1
            for pos,row in enumerate(doc['trace']):
                s=row['before'];level=s.get('level',0);fame_level=1+sum(s['fame']>=t for t in [3,8,15,24,35,48,63,80,99])
                if s['phase']=='End' and fame_level>level and any(v%2==0 for v in range(level+1,fame_level+1)):
                    found.append(dict(episode=eid,step=pos,level=level,fame=s['fame'],fame_level=fame_level,action=row['action'],source_sha256=sha));break
            if len(found)>=6 or inspected>=3000:break
    finally:
        for z in archives.values():z.close()
    result=dict(inspected=inspected,candidates=found,scope='Archived native states; candidates still require actual UI replay and End')
    write_json(ROOT/'AutomationOutputs/UiFluency/design_review/reward_fixture_candidates.json',result);print(json.dumps(result))
if __name__=='__main__':main()
