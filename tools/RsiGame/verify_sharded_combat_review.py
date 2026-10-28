"""Verify delegated Sol raw delivery and persist actual FINAL; never score."""
import argparse,base64,json,hashlib
from pathlib import Path
from jsonschema import Draft202012Validator
from verify_reader_delivery import complete_prompt_coverage,text_parts
from persist_combat_focus_review import semantic_errors
from build_sharded_combat_review import IDS,entry,sha,write

def trace(path,worker):
    raw=Path(path).read_bytes();rows=[json.loads(x) for x in raw.splitlines() if x.strip()]
    ends=[i for i,r in enumerate(rows) if r.get('payload',{}).get('role')=='assistant' and r.get('payload',{}).get('phase') in ('final','final_answer')]
    if not ends:raise ValueError('No actual FINAL')
    rows=rows[:ends[-1]+1];models=set();agents=set();outputs=[];images=set()
    def scan(v):
        if isinstance(v,list):
            for x in v:scan(x)
        elif isinstance(v,dict):
            if v.get('type') in ('input_image','image'):
                u=v.get('image_url','');u=u.get('url','') if isinstance(u,dict) else u
                if isinstance(u,str) and u.startswith('data:image/png;base64,'):images.add(sha(base64.b64decode(u.split(',',1)[1],validate=True)))
            for x in v.values():
                if isinstance(x,(dict,list)):scan(x)
    for r in rows:
        p=r.get('payload',{})
        if r.get('type')=='session_meta':agents.add(p.get('agent_path'))
        if r.get('type')=='turn_context':models.add(p.get('model'))
        if p.get('type') in ('custom_tool_call_output','function_call_output'):
            outputs.append(text_parts(p.get('output','')));scan(p.get('output',''))
    if agents!={worker} or not worker.startswith('/'):raise ValueError('Canonical worker mismatch')
    if models!={'gpt-6.1-sol'}:raise ValueError('Actual model must be gpt-6.1-sol')
    final=''.join(x.get('text','') for x in rows[-1]['payload'].get('content',[]) if x.get('type')=='output_text').encode('utf8')
    return raw,final,outputs,images

def delivery(e,outputs,images,image=False):
    b=Path(e['path']).read_bytes()
    if sha(b)!=e['sha256']:raise ValueError('SHA mismatch '+e['path'])
    if image:
        if not b.startswith(b'\x89PNG\r\n\x1a\n') or sha(b) not in images:raise ValueError('Original PNG not delivered '+e['path'])
    else:complete_prompt_coverage(b.decode('utf-8-sig'),list(enumerate(outputs)))

def image_scope(entries,images):
    if images!={e['sha256'] for e in entries}:raise ValueError('Unexpected or missing original image outside assigned scope')

def independent_final_worker(worker,proofs):
    if any(p['worker']==worker for p in proofs):raise ValueError('Final judge must be independent from fact readers')

def absolute_path_key(path):
    # Windows accepts both separators and case variants for the same absolute
    # file. Compare native path identities without rewriting the actual FINAL.
    p=Path(path)
    if not p.is_absolute():raise ValueError('Read ledger/coverage requires absolute path '+str(path))
    return p

def facts(final,task):
    Draft202012Validator(json.loads(Path(task['must_read'][3]['path']).read_text(encoding='utf8'))).validate(final)
    if final['shard_id']!=task['shard_id']:raise ValueError('Wrong shard')
    if len(final['item_facts'])!=32 or {x['id'] for x in final['item_facts']}!=set(IDS):raise ValueError('Missing/duplicate32 IDs')
    for e in task['must_read']+task['images']:
        if not any(absolute_path_key(x.get('path',''))==absolute_path_key(e['path']) and x.get('sha256')==e['sha256'] and x.get('actually_read_or_viewed') is True for x in final['read_ledger']):raise ValueError('Missing actual read ledger '+e['path'])
    # Explicit per-original facts for all32 items prevents a vague shard summary hiding omitted images.
    expected={absolute_path_key(e['path']) for e in task['images']} or {absolute_path_key(e['path']) for e in task['must_read'][4:]}
    covered=[absolute_path_key(x.get('path','')) for x in final['coverage']]
    if len(covered)!=len(set(covered)) or not expected.issubset(set(covered)):raise ValueError('Missing/duplicate evidence coverage')
    for x in final['coverage']:
        if absolute_path_key(x.get('path','')) in expected and (set(x.get('item_ids',[]))!=set(IDS) or len(x.get('item_ids',[]))!=32):raise ValueError('Coverage requires all32 IDs per evidence')
    def no_scores(v):
        if isinstance(v,dict):
            if any(k in ('score','scores','overall_numeric_score','grade') for k in v):raise ValueError('Fact reader numeric scoring forbidden')
            for x in v.values():no_scores(x)
        elif isinstance(v,list):
            for x in v:no_scores(x)
    no_scores(final)

def persist(manifest,shard,transcript,worker,out):
    m=json.loads(Path(manifest).read_text(encoding='utf8'));t=next(x for x in m['tasks'] if x['shard_id']==shard)
    tr,raw,outputs,images=trace(transcript,worker)
    image_scope(t['images'],images)
    for e in t['must_read']:delivery(e,outputs,images)
    for e in t['images']:delivery(e,outputs,images,True)
    f=json.loads(raw);facts(f,t)
    out=Path(out)
    if out.exists():raise FileExistsError(out)
    out.mkdir(parents=True);(out/'actual_final.txt').write_bytes(raw)
    proof=dict(verified=True,worker=worker,model='gpt-6.1-sol',shard_id=shard,transcript=entry(transcript),actual_final=entry(out/'actual_final.txt'),manifest=entry(manifest),task=t,scope='Actual transport and structural coverage only; no judgment accuracy or UI acceptance')
    write(out/'verification.json',proof);return proof

def unchanged_actual_final(proof,raw):
    if entry(proof['actual_final']['path'])!=proof['actual_final'] or Path(proof['actual_final']['path']).read_bytes()!=raw:raise ValueError('Changed actual FINAL')

def assemble(manifest,proofs,out):
    m=json.loads(Path(manifest).read_text(encoding='utf8'));seen={};required=[]
    for p in proofs:
        proof=json.loads(Path(p).read_text(encoding='utf8'))
        if proof.get('verified') is not True:raise ValueError('Unverified shard')
        sid=proof['shard_id']
        if sid in seen:raise ValueError('Duplicate shard')
        # Re-run original proof from current canonical trace; do not trust editable verification flags.
        task=next(x for x in m['tasks'] if x['shard_id']==sid)
        if proof['task']!=task or proof['manifest']!=entry(manifest):raise ValueError('Manifest drift')
        tr,raw,outputs,images=trace(proof['transcript']['path'],proof['worker'])
        image_scope(task['images'],images)
        if entry(proof['transcript']['path'])!=proof['transcript']:raise ValueError('Changed trace')
        unchanged_actual_final(proof,raw)
        for e in task['must_read']:delivery(e,outputs,images)
        for e in task['images']:delivery(e,outputs,images,True)
        facts(json.loads(raw),task);seen[sid]=proof;required += [entry(p),proof['actual_final']]
    if set(seen)!={x['shard_id'] for x in m['tasks']}:raise ValueError('Incomplete team coverage')
    packet=dict(rubric_version=m['protocol_version'],frozen_scope=m['source_scope'],required_images=m['representative_images'],required_text=required+m['final_context'][3:],team_proofs=[entry(p) for p in proofs],manifest=entry(manifest))
    for name,e in zip(('rubric','score_schema','sp'),m['final_context'][:3]):packet[name+'_path']=e['path'];packet[name+'_sha256']=e['sha256']
    if sum(e['bytes'] for e in required)>1000000:raise ValueError('Final verified fact text >1MB; do not truncate, revise shard output protocol')
    write(Path(out)/'input.json',packet);return packet

def final_verify(packet,transcript,worker,out):
    p=json.loads(Path(packet).read_text(encoding='utf8'));m=p['manifest']['path']
    independent_final_worker(worker,[json.loads(Path(e['path']).read_text(encoding='utf8')) for e in p['team_proofs']])
    # Full replay validates every shard and unchanged raw FINAL before admitting final judge.
    target=Path(out); assembled=target.parent/(target.name+'_reverified_inputs')
    assemble(m,[e['path'] for e in p['team_proofs']],assembled)
    if json.loads((assembled/'input.json').read_text(encoding='utf8'))!=p:raise ValueError('Final packet changed')
    tr,raw,outputs,images=trace(transcript,worker)
    image_scope(p['required_images'],images)
    required=[entry(packet)]+[dict(path=p[n+'_path'],sha256=p[n+'_sha256']) for n in ('sp','rubric','score_schema')]+p['required_text']
    for e in required:delivery(e,outputs,images)
    for e in p['required_images']:delivery(e,outputs,images,True)
    f=json.loads(raw);schema=json.loads(Path(p['score_schema_path']).read_text(encoding='utf8'));Draft202012Validator(schema).validate(f)
    # Existing original semantics are preserved; ledger is representative personal scope, team scope checked above.
    errors=semantic_errors(f,p)
    method=f['evidence_method']
    if method['personal_direct_view_scope']!=[e['path'] for e in p['required_images']]:errors.append('Personal scope must equal actually delivered representative originals')
    if set(method['team_coverage'])!={t['shard_id'] for t in json.loads(Path(m).read_text(encoding='utf8'))['tasks']}:errors.append('Incomplete team scope')
    if errors:raise ValueError('; '.join(errors))
    if target.exists():raise FileExistsError(target)
    target.mkdir(parents=True);(target/'actual_final.txt').write_bytes(raw);write(target/'review.json',f)
    proof=dict(verified=True,worker=worker,model='gpt-6.1-sol',transcript=entry(transcript),actual_final=entry(target/'actual_final.txt'),input=entry(packet),scope='Transport/schema/semantics; not UI acceptance or correctness of subjective scores')
    write(target/'verification.json',proof);return proof
if __name__=='__main__':
    a=argparse.ArgumentParser();sub=a.add_subparsers(dest='cmd',required=True)
    p=sub.add_parser('persist');p.add_argument('--manifest',required=True);p.add_argument('--shard',required=True);p.add_argument('--transcript',required=True);p.add_argument('--worker',required=True);p.add_argument('--out',required=True)
    p=sub.add_parser('assemble');p.add_argument('--manifest',required=True);p.add_argument('--proofs',nargs='+',required=True);p.add_argument('--out',required=True)
    p=sub.add_parser('final');p.add_argument('--packet',required=True);p.add_argument('--transcript',required=True);p.add_argument('--worker',required=True);p.add_argument('--out',required=True)
    x=vars(a.parse_args());cmd=x.pop('cmd');print(json.dumps({'verified':bool({'persist':persist,'assemble':assemble,'final':final_verify}[cmd](**x))}))
