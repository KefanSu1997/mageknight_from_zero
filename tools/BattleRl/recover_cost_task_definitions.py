"""Recover omitted task definitions by exact task_id, never guess seeds/flags."""
import collections
import hashlib
import json
from pathlib import Path
import sqlite3
import zipfile
from structured_ui_ppo import ROOT,write_json

def task_records(value):
    if isinstance(value,dict):
        if 'task_id' in value and isinstance(value.get('task'),dict):yield value
        for child in value.values():yield from task_records(child)
    elif isinstance(value,list):
        for child in value:yield from task_records(child)

def main():
    folder=ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset'
    db=sqlite3.connect(folder/'workset.sqlite')
    rows=db.execute("SELECT id,task_id,path,reset_json FROM episodes WHERE reset_kind IN ('legacy_seed','independent_task_header_recovered')").fetchall()
    wanted={r[1] for r in rows if r[1] is not None};records=collections.defaultdict(dict)
    for eid,tid,path,source_sha,reset in db.execute("SELECT id,task_id,path,sha256,reset_json FROM episodes WHERE reset_kind NOT IN ('legacy_seed','independent_task_header_recovered')"):
        if tid not in wanted:continue
        definition=json.loads(reset).get('task')
        if definition is None:continue
        key=json.dumps(definition,sort_keys=True,separators=(',',':'),ensure_ascii=False)
        records[tid].setdefault(key,dict(registry=path,sha256=source_sha,task=dict(definition=definition),reference_episode=eid))
    for path in (ROOT/'AutomationOutputs/BattleRl').rglob('*pool*.json'):
        raw=path.read_bytes()
        # Skip unrelated large records without parsing their bodies.
        if not any(t.encode() in raw for t in wanted):continue
        try:doc=json.loads(raw)
        except json.JSONDecodeError:continue
        for r in task_records(doc):
            if not isinstance(r,dict) or r.get('task_id') not in wanted:continue
            definition=r.get('task',{}).get('definition')
            if definition is None:continue
            key=json.dumps(definition,sort_keys=True,separators=(',',':'),ensure_ascii=False)
            records[r['task_id']].setdefault(key,dict(registry=str(path),sha256=hashlib.sha256(raw).hexdigest(),task=r['task']))
    audit=[];missing=[]
    for eid,tid,path,old in rows:
        options=records[tid]
        if not options:
            volume,source_sha=db.execute('SELECT volume,sha256 FROM episodes WHERE id=?',(eid,)).fetchone()
            with zipfile.ZipFile('H:/MageKnight_RL_Backups/20261012_before_reward_v2/'+volume) as archive:raw=archive.read(path)
            assert hashlib.sha256(raw).hexdigest()==source_sha
            original=json.loads(raw)
            definition=original.get('task_definition')
            if definition is not None:
                key=json.dumps(definition,sort_keys=True,separators=(',',':'),ensure_ascii=False)
                # Missing task IDs are distinct source records; never merge them.
                options={key:dict(registry=path,sha256=source_sha,task=dict(definition=definition),reference_episode=eid)}
        if len(options)!=1:missing.append(dict(episode=eid,task_id=tid,candidates=len(options)));continue
        key,origin=next(iter(options.items()))
        definition=json.loads(key)
        old_doc=json.loads(old)
        old_seed=old_doc.get('seed',old_doc.get('task',{}).get('RandomSeed'))
        if definition['RandomSeed']!=old_seed:raise ValueError('Original random seed differs from matching task definition')
        new=json.dumps(dict(task=definition),sort_keys=True,separators=(',',':'),ensure_ascii=False)
        db.execute('UPDATE episodes SET reset_json=?,reset_kind=? WHERE id=?',(new,'independent_task_header_recovered',eid))
        audit.append(dict(episode=eid,task_id=tid,source=path,old_reset_json=json.dumps(dict(seed=old_seed)),definition=definition,**origin))
    db.commit();manifest=json.loads((folder/'manifest.json').read_text(encoding='utf-8'))
    manifest['reset_types']=dict(db.execute('SELECT reset_kind,count(*) FROM episodes GROUP BY reset_kind'))
    manifest['reset_recovery_note']='Missing episode headers are independent-task roots when exact task_id registry contains definition; not presumed legacy-seed generators. Native replay verification still required.'
    write_json(folder/'independent_header_recovery.json',dict(recovered=len(audit),unresolved=missing,records=audit))
    write_json(folder/'manifest.json',manifest)
    print(json.dumps(dict(recovered_total=len(audit),unresolved=len(missing),reset_types=manifest['reset_types']),ensure_ascii=False));db.close()


if __name__=='__main__':main()
