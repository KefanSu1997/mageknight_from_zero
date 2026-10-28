"""Counterfactual UI routes on immutable accepted evidence, no Unity replay."""
import csv
import hashlib
import json
import math
import re
import sqlite3
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'AutomationOutputs/UiFluency/ui_iteration'


def cases(path):
    # A bounded JSON decoder handles both pretty and compact case bundles.
    decoder = json.JSONDecoder()
    with path.open(encoding='utf8') as f:
        buf = ''
        while True:
            chunk = f.read(1024 * 1024)
            if not chunk:
                raise ValueError('No samples array: ' + str(path))
            buf += chunk
            match = re.search(r'"samples"\s*:\s*\[', buf)
            if match:
                buf = buf[match.end():]
                break
        while True:
            buf = buf.lstrip(' \r\n\t,')
            if buf.startswith(']'):
                return
            try:
                value, end = decoder.raw_decode(buf)
            except json.JSONDecodeError:
                chunk = f.read(1024 * 1024)
                if not chunk:
                    raise
                buf += chunk
                continue
            yield value
            buf = buf[end:]


def point_step(row, intent):
    actual = row['actual']
    obs = actual['before']
    control = next(c for c in obs['controls'] if c['path'] == actual['click']['buttonPath'])
    values = {v['key']: v['value'] for v in obs['state']}
    fixture = json.loads(values.get('audit.costFixture', '{}'))
    native = json.loads(values.get('audit.costNativeState', '{}'))
    from ui_cost_views import panel
    return dict(name=control['name'], path=control['path'], x=control['x'], y=control['y'],
                w=control['width'], h=control['height'], intent=intent,
                before=panel(obs), after=panel(actual['observation']), phase=native.get('phase'),
                ordinary=[c['index'] for c in fixture.get('cardChoices', []) if c['type'] != 'Wound'],
                discard=fixture.get('turnFlow', {}).get('discardSlots', []))


def synthetic(name, x, top, w, h, intent, before, after):
    prefix='Canvas/BattleTable/TurnReview/' if name in ('Btn_SelectAllOrdinary','Btn_ConfirmEndTurn') else 'Canvas/BattleTable/'
    return dict(name=name, path=prefix + name, x=x + w/2, y=1080-top-h/2,
                w=w, h=h, intent=intent, before=before, after=after)


def redesign(steps, kinds, version):
    out = []
    removed = []
    quick = False
    tools_open = bool(steps and steps[0]['before'] == 'JourneyTools')
    return_view = 'Outside' if steps and steps[0]['phase'] == 'Outside' else 'Battle.Hand'
    skip_close = False
    for j, raw in enumerate(steps):
        s = dict(raw)
        name = s['name']
        nxt = steps[j+1]['name'] if j+1 < len(steps) else ''
        if name == 'Btn_Menu' and nxt == 'Btn_JourneyTools':
            return_view=s['before']
            removed.append(dict(index=j, reason='direct_public_mana')); continue
        if name == 'Btn_CloseTools' and skip_close:
            removed.append(dict(index=j, reason='auto_return_after_source')); skip_close = False; continue
        if name == 'Btn_JourneyTools':
            if s['before'] in ('Outside','Battle.Hand','Battle.Skills','Battle.Units','JourneyHand'):
                return_view=s['before']
            if s['phase'] != 'Outside':
                s.update(name='Btn_QuickMana',path='Canvas/BattleTable/Btn_QuickMana', x=1537.5, y=1006., w=215., h=52., before=return_view)
            tools_open = True; quick = True
        elif '/JourneyTools/' in s['path'] and not tools_open:
            if s['phase'] == 'Outside':
                out.append(synthetic('Btn_JourneyTools',42,872,415,64,s['intent'],return_view,'JourneyTools'))
            else:
                out.append(synthetic('Btn_QuickMana',1430,48,215,52,s['intent'],return_view,'JourneyTools'))
            tools_open = True; quick = True
        if version >= 4 and name == 'Btn_UseSource' and nxt == 'Btn_CloseTools':
            s.update(name='Btn_UseSourceReturn',path='Canvas/BattleTable/JourneyTools/Btn_UseSourceReturn',x=1630.,y=944.,w=480.,h=60.,after=return_view)
            tools_open=False;skip_close=True
        elif name in ('Btn_UseSource','Btn_UseExtraSource') and quick and version < 4:
            s['after'] = return_view; tools_open = False; skip_close = True
        elif name == 'Btn_CloseTools':
            tools_open = False; quick = False; skip_close = False
            if version >= 5:s.update(x=1372.,y=114.,w=860.,h=70.)
        if version >= 5 and s['path']=='Canvas/BattleTable/JourneyTools/Btn_UseSourceReturn':
            s.update(x=472.,y=114.,w=860.,h=70.)
        out.append(s)
    if version == 1:
        return out, removed
    result = []
    for intent in ['A', 'B']:
        part = [s for s in out if s['intent'] == intent]
        if kinds[intent] != 'nextTurn':
            result.extend(part); continue
        end = next((i for i,s in enumerate(part) if s['name']=='Btn_EndTurn'), None)
        if end is None or not part[end].get('ordinary'):
            result.extend(part); continue
        stop = part[end]
        # The old macro's exact selected physical slots must be preserved.
        if stop['ordinary'] != stop['discard']:
            raise ValueError('Batch cannot reproduce recorded ordered physical discard slots')
        allowed = {'Btn_JourneyHand','Btn_DiscardAtEnd','Btn_NextHand','Btn_PreviousHand','Btn_CloseHand','Btn_CloseBattleMenu'}
        start = end
        while start and (part[start-1]['name'] in allowed or part[start-1]['name'].startswith('Btn_Hand_')):
            start -= 1
        initial = part[start]['before']
        # A shielded old menu/hand cannot be skipped merely because it has no
        # game-rule effect. Preserve necessary closes before entering review.
        retained=[s for s in part[start:end] if s['name']=='Btn_CloseBattleMenu']
        if initial=='JourneyHand':
            retained.extend(s for s in part[start:end] if s['name']=='Btn_CloseHand')
        button = dict(stop, before=stop['before'], after='TurnReview')
        result.extend(part[:start]);result.extend(retained);result.append(button)
        # V2 initially put batch selection near the summary. V3 moves it near
        # final confirmation, without changing sizes or concealing text.
        top = 210 if version == 2 else 760
        result.append(synthetic('Btn_SelectAllOrdinary',1010,top,860,64,intent,'TurnReview','TurnReview'))
        result.append(synthetic('Btn_ConfirmEndTurn',1010,876,860,57,intent,'TurnReview',stop['after']))
        result.extend(part[end+1:])
        removed.append(dict(intent=intent, reason='exact_all_ordinary_discard_batch', original_controls=[s['name'] for s in part[start:end+1]], selected_slots=stop['discard']))
    return result, removed


def cost(steps):
    a = [s for s in steps if s['intent']=='A']; b = [s for s in steps if s['intent']=='B']
    # Include A's final click once as the origin of B's path.
    bp = a[-1:] + b
    distance = lambda seq: sum(math.hypot(x['x']-y['x'],x['y']-y['y']) for x,y in zip(seq,seq[1:]))
    return dict(a_clicks=len(a),b_clicks=len(b),pair_clicks=len(steps),internal_a_px=distance(a),transition_px=distance(bp),panels=sum(s['before']!=s['after'] for s in steps))


def main():
    OUT.mkdir(exist_ok=True)
    db = sqlite3.connect(f'file:{ROOT.as_posix()}/AutomationOutputs/UiFluency/20261012_sample5_selected/selected.sqlite?mode=ro',uri=True)
    db.row_factory=sqlite3.Row
    db.execute('ATTACH DATABASE ? AS work',(str(ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite'),))
    population={(a,b):n for a,b,n in db.execute('SELECT a.kind,b.kind,count(*) FROM work.transitions t JOIN work.actions a ON a.id=t.a_action JOIN work.actions b ON b.id=t.b_action GROUP BY a.kind,b.kind')}
    wanted=defaultdict(dict)
    for r in db.execute('SELECT c.*,s.estimator_weight,a.kind ak,b.kind bk FROM costs c JOIN samples s USING(group_id,episode,a_step) JOIN work.actions a ON a.id=s.a JOIN work.actions b ON b.id=s.b'):
        wanted[r['source_path']][(r['group_id'],r['episode'],r['a_step'])]=dict(r)
    cache=OUT/'original_routes.jsonl'
    signature=hashlib.sha256(json.dumps([(p,[(k,v['result_sha']) for k,v in sorted(rows.items())]) for p,rows in sorted(wanted.items())],sort_keys=True).encode()).hexdigest()
    manifest=OUT/'route_manifest.json'
    if not cache.exists() or not manifest.exists() or json.loads(manifest.read_text())['signature']!=signature:
        n=0; geometry_errors=[]
        with cache.open('w',encoding='utf8') as output:
            for index,(path,required) in enumerate(sorted(wanted.items())):
                h=hashlib.sha256()
                with Path(path).open('rb') as f:
                    for chunk in iter(lambda:f.read(4*1024*1024),b''):h.update(chunk)
                assert h.hexdigest()==next(iter(required.values()))['result_sha'],path
                found=set()
                for case in cases(Path(path)):
                    key=(case['group'],case['episode'],case['a_step'])
                    if key not in required:continue
                    found.add(key);row=required[key]
                    steps=[point_step(s,'A' if j<case['a_clicks'] else 'B') for j,s in enumerate(case['pointer_steps'])]
                    derived=cost(steps)
                    for metric,value in derived.items():
                        if abs(value-row[metric])>0.01:geometry_errors.append((key,metric,value,row[metric]))
                    output.write(json.dumps(dict(key=key,ak=row['ak'],bk=row['bk'],weight=population[(row['ak'],row['bk'])]*row['estimator_weight'],steps=steps,source_sha=row['result_sha']),ensure_ascii=False)+'\n');n+=1
                assert found==set(required),(path,len(found),len(required))
                print(f'Extracted {index+1}/{len(wanted)} files, {n} accepted routes',flush=True)
        assert not geometry_errors,geometry_errors[:5]
        manifest.write_text(json.dumps(dict(signature=signature,routes=n,geometry_errors=geometry_errors),indent=2),encoding='utf8')
    aggregates={v:defaultdict(float) for v in range(6)}
    kinds={v:defaultdict(lambda:defaultdict(float)) for v in range(6)}
    pairs={v:defaultdict(lambda:defaultdict(float)) for v in range(6)}
    regressions=defaultdict(int);count=0;plans=[]
    with (OUT/'counterfactual_routes.jsonl').open('w',encoding='utf8') as output:
        for line in cache.open(encoding='utf8'):
            r=json.loads(line);count+=1
            old=cost(r['steps'])
            for version in range(6):
                steps,changes=(r['steps'],[]) if version==0 else redesign(r['steps'],dict(A=r['ak'],B=r['bk']),version)
                c=cost(steps);w=r['weight']
                for s in steps:
                    assert 0<=s['x']-s['w']/2 and s['x']+s['w']/2<=1920.01
                    assert -.01<=s['y']-s['h']/2 and s['y']+s['h']/2<=1080.01
                for bucket in [aggregates[version],kinds[version][r['bk']],pairs[version][r['key'][0]]]:
                    bucket['weight']+=w;bucket['samples']+=1
                    for metric,value in c.items():bucket[metric]+=w*value
                if version and c['b_clicks']>old['b_clicks']:regressions[f'v{version}_click_samples']+=1
                if version and c['transition_px']>old['transition_px']+.01:regressions[f'v{version}_distance_samples']+=1
                if version==5:
                    output.write(json.dumps(dict(key=r['key'],before=old,after=c,steps=steps,changes=changes,source_sha=r['source_sha']),ensure_ascii=False)+'\n')
            if count%500==0:print('Calculated',count,flush=True)
    def normalize(bucket):
        return dict(weight=bucket['weight'],samples=int(bucket['samples']),**{m:bucket[m]/bucket['weight'] for m in ('a_clicks','b_clicks','pair_clicks','internal_a_px','transition_px','panels')})
    sources=['Assets/Scripts/Battle/PveBattleController.FluentUi.cs','Assets/Scripts/Battle/PveBattleController.cs','Assets/Scripts/Battle/PveBattleController.Journey.cs','Assets/Scripts/Battle/PveBattleController.DemoControls.cs']
    result=dict(scope='offline counterfactual, not executed Unity performance',canvas=[1920,1080],routes=count,chosen_version=5,design_source_sha256={p:hashlib.sha256((ROOT/p).read_bytes()).hexdigest() for p in sources},cost_model_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),versions={str(v):normalize(a) for v,a in aggregates.items()},intents={str(v):{k:normalize(b) for k,b in bs.items()} for v,bs in kinds.items()},pairs={str(v):{k:normalize(b) for k,b in bs.items()} for v,bs in pairs.items()},regressions=dict(regressions),route_signature=signature)
    (OUT/'comparison.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
    with (OUT/'behavior_costs.csv').open('w',encoding='utf-8-sig',newline='') as f:
        wr=csv.writer(f);wr.writerow(['B','measured_population_weight','old_clicks','v5_clicks','old_distance_px','v5_distance_px','old_pair_views','v5_pair_views'])
        for k,old in result['intents']['0'].items():
            new=result['intents']['5'][k];wr.writerow([k,old['weight'],old['b_clicks'],new['b_clicks'],old['transition_px'],new['transition_px'],old['panels'],new['panels']])
    print(json.dumps(dict(versions=result['versions'],regressions=result['regressions']),indent=2),flush=True)


if __name__=='__main__':main()
