"""Verify actual independent image-reader output and frozen-answer accuracy."""
import argparse
import base64
import hashlib
import json
from pathlib import Path
from persist_retrospective_review import delivered
from verify_reader_delivery import text_parts


def persist(root, trace):
    models, finals, images, outputs = set(), [], set(), []
    for line in trace.open(encoding='utf8'):
        record = json.loads(line); item = record.get('payload', {})
        if record.get('type') == 'turn_context':
            models.add(item.get('model'))
        if item.get('type') == 'message' and item.get('role') == 'assistant' and item.get('phase') in ('final', 'final_answer'):
            finals.append(''.join(x.get('text','') for x in item.get('content',[]) if x.get('type')=='output_text'))
        if item.get('type') in ('custom_tool_call_output','function_call_output'):
            outputs.append(text_parts(item.get('output',[])))
        def walk(value):
            if isinstance(value,dict):
                if value.get('type')=='input_image':
                    url=value.get('image_url','')
                    if isinstance(url,str) and url.startswith('data:'):
                        images.add(hashlib.sha256(base64.b64decode(url.split(',',1)[1])).hexdigest())
                for v in value.values(): walk(v)
            elif isinstance(value,list):
                for v in value: walk(v)
        walk(record)
    if not finals: raise ValueError('No actual final')
    raw=finals[-1]; result=json.loads(raw)
    packet=json.loads((root/'input.json').read_text(encoding='utf8'))
    oracle=json.loads((root/'oracle.json').read_text(encoding='utf8'))
    errors=[]
    if models!={'gpt-6.1-sol'}:errors.append('Actual model mismatch')
    for p in [root/'system_prompt.txt',root/'input.json']:
        if not delivered(p.read_text(encoding='utf8'),outputs):errors.append('Full role/input text delivery not proved: '+str(p))
    for q in packet['questions']:
        p=Path(q['image']['path']);digest=hashlib.sha256(p.read_bytes()).hexdigest()
        if digest!=q['image']['sha256'] or digest not in images:errors.append('Image hash/delivery mismatch: '+q['id'])
    answers=result.get('answers',{}); checks=[]
    for q in packet['questions']:
        key=q['id'];expected=oracle['answers'][key];actual=answers.get(key)
        correct=(set(actual)==set(expected) if key=='q04' and isinstance(actual,list)
                 else actual in expected if isinstance(expected,list) else type(actual) is int and actual==expected)
        checks.append(dict(id=key,actual=actual,expected=expected,correct=correct))
    (root/'actual_final.txt').write_bytes(raw.encode('utf8'))
    proof=dict(verified=not errors,errors=errors,model=sorted(models),transcript=str(trace),
               actual_final_sha256=hashlib.sha256(raw.encode('utf8')).hexdigest(),
               image_bytes_verified=len(images),correct=sum(c['correct'] for c in checks),denominator=len(checks),
               checks=checks,scope='One model screenshot-reading trial; not human search time or error rate; not converted to1–5 score')
    (root/'verification.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2),encoding='utf8')
    return proof


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--root',type=Path,required=True);parser.add_argument('--trace',type=Path,required=True)
    args=parser.parse_args();r=persist(args.root.resolve(),args.trace.resolve());print(json.dumps({k:v for k,v in r.items() if k!='checks'},ensure_ascii=False))
