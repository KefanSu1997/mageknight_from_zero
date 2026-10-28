"""Extract an offline UI graph from accepted evidence, without Unity calls.

Exact observed states/paths are retained. Layout reuse does not imply that an
unobserved gameplay context is executable. No missing cost is imputed.
"""
import argparse
import hashlib
import json
import math
from collections import defaultdict
from pathlib import Path
import sqlite3
from ui_cost_views import panel, VERSION

ROOT = Path(__file__).resolve().parents[2]


def canonical(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(',', ':'))


def digest(value):
    return hashlib.sha256(canonical(value).encode()).hexdigest()


def observe(db, observation):
    # Preserve availability, page/physical occurrence indices and geometry.
    # Text/selection and public state are part of the exact node, not a layout.
    controls = sorted(({k: c[k] for k in ('path', 'name', 'x', 'y', 'width', 'height',
                    'clickable', 'interactable')} for c in observation['controls']), key=lambda c: c['path'])
    layout = dict(width=observation['screenWidth'], height=observation['screenHeight'],
                  view=panel(observation), controls=controls)
    lid = digest(layout)
    db.execute('INSERT OR IGNORE INTO layouts VALUES(?,?,?)', (lid, layout['view'], canonical(layout)))
    public = {v['key']: v['value'] for v in observation['state'] if not v['key'].startswith('audit.')}
    node = dict(layout=lid, public_state=public,
                control_text={c['path']: c['text'] for c in observation['controls']})
    nid = digest(node)
    db.execute('INSERT OR IGNORE INTO nodes VALUES(?,?,?)', (nid, lid, canonical(node)))
    return nid, layout['view']


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--plan', type=Path, default=ROOT/'AutomationOutputs/UiFluency/20261012_sample5_selected')
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    out = args.out.resolve(); out.mkdir(parents=True, exist_ok=False)
    source = sqlite3.connect('file:'+str(args.plan.resolve()/'selected.sqlite').replace('\\', '/')+'?mode=ro', uri=True)
    source.execute('ATTACH DATABASE ? AS work', (str(ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite'),))
    accepted = defaultdict(dict)
    for row in source.execute('SELECT * FROM costs'):
        accepted[row[4]][tuple(row[:3])] = row
    db = sqlite3.connect(out/'atlas.sqlite')
    db.executescript('''
      CREATE TABLE layouts(id TEXT PRIMARY KEY,view TEXT,geometry_json TEXT);
      CREATE TABLE nodes(id TEXT PRIMARY KEY,layout TEXT,public_context_json TEXT);
      CREATE TABLE edges(group_id TEXT,episode INTEGER,a_step INTEGER,ordinal INTEGER,intent TEXT,
        before_node TEXT,after_node TEXT,control_path TEXT,x REAL,y REAL,input_x REAL,input_y REAL,
        PRIMARY KEY(group_id,episode,a_step,ordinal));
      CREATE TABLE routes(group_id TEXT,episode INTEGER,a_step INTEGER,start_node TEXT,end_a_node TEXT,end_b_node TEXT,
        a_action_json TEXT,b_action_json TEXT,a_clicks INTEGER,b_clicks INTEGER,internal_a_px REAL,transition_px REAL,
        views INTEGER,source_sha TEXT,source_path TEXT,PRIMARY KEY(group_id,episode,a_step));
    ''')
    errors=[]; checked=0; clicks=0; sources=[]
    for index,(path, wanted) in enumerate(sorted(accepted.items())):
        sha = next(iter(wanted.values()))[3]
        # All accepted rows from this immutable result must bind the same SHA.
        if any(r[3] != sha for r in wanted.values()):raise ValueError('Mixed source SHA '+path)
        h=hashlib.sha256()
        with Path(path).open('rb') as f:
            for block in iter(lambda:f.read(1024*1024), b''):h.update(block)
        if h.hexdigest()!=sha:raise ValueError('Accepted evidence SHA changed '+path)
        data=json.loads(Path(path).read_text(encoding='utf-8'))
        found=set()
        for case in data['samples']:
            key=(case['group'],case['episode'],case['a_step'])
            if key not in wanted:continue
            found.add(key);saved=wanted[key]
            if case['status']!='complete':raise ValueError('Accepted row has no completed evidence')
            rows=case['pointer_steps']; na=case['a_clicks']; points=[]; views=0
            start=end_a=end_b=None
            for ordinal,row in enumerate(rows):
                actual=row['actual'];control=next(c for c in actual['before']['controls'] if c['path']==actual['click']['buttonPath'])
                point=(control['x'],control['y']);points.append(point)
                before,v1=observe(db,actual['before']);after,v2=observe(db,actual['observation'])
                views+=v1!=v2
                if ordinal==0:start=before
                if ordinal==na-1:end_a=after
                end_b=after
                db.execute('INSERT INTO edges VALUES(?,?,?,?,?,?,?,?,?,?,?,?)',
                    (*key,ordinal,'A' if ordinal<na else 'B',before,after,control['path'],*point,
                     actual['click']['clickX'],actual['click']['clickY']))
            internal=sum(math.dist(a,b) for a,b in zip(points[:na],points[1:na]))
            bridge=points[na-1:];transition=sum(math.dist(a,b) for a,b in zip(bridge,bridge[1:]))
            if na!=saved[5] or len(rows)-na!=saved[6] or len(rows)!=saved[7] or views!=saved[10] or abs(internal-saved[8])>.001 or abs(transition-saved[9])>.001:
                errors.append(dict(key=key,derived=[na,len(rows)-na,len(rows),internal,transition,views],stored=list(saved[5:11])))
            actions=source.execute('''SELECT aa.arguments_json,bb.arguments_json FROM samples s
                JOIN work.actions aa ON aa.id=s.a JOIN work.actions bb ON bb.id=s.b
                WHERE s.group_id=? AND s.episode=? AND s.a_step=?''',key).fetchone()
            db.execute('INSERT INTO routes VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)',
                (*key,start,end_a,end_b,*actions,na,len(rows)-na,internal,transition,views,sha,path))
            checked+=1;clicks+=len(rows)
        if found!=set(wanted):raise ValueError('Missing accepted keys '+path)
        sources.append(dict(path=path,sha256=sha,routes=len(found)))
        db.commit()
        if (index+1)%10==0:print(canonical(dict(bundles=index+1,total_bundles=len(accepted),routes=checked)),flush=True)
    summary=dict(status='passed' if not errors else 'failed',scope='Exact observed UI graph and offline geometry recomputation; no unseen-route prediction',
        accepted_routes=checked,clicks=clicks,layouts=db.execute('SELECT COUNT(*) FROM layouts').fetchone()[0],
        exact_nodes=db.execute('SELECT COUNT(*) FROM nodes').fetchone()[0],views=dict(db.execute('SELECT view,COUNT(*) FROM layouts GROUP BY view')),
        metric_version=VERSION,offline_recomputation_errors=errors,
        required_samples=source.execute('SELECT COUNT(*) FROM samples').fetchone()[0],
        pending_samples=source.execute("SELECT COUNT(*) FROM samples WHERE status NOT IN ('complete','verified_incompatible')").fetchone()[0],
        evidence_sources=sources,script_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest())
    db.close();source.close()
    (out/'summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
    print(canonical({k:v for k,v in summary.items() if k not in ('evidence_sources','views','offline_recomputation_errors')}),flush=True)
    if errors:raise SystemExit('Offline geometry disagrees with accepted costs; see summary.json')


if __name__=='__main__':main()
