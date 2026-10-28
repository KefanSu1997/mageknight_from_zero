"""Read-only readiness inspection, without exposing a reviewer's analysis or scoring drafts."""
import argparse
import base64
import hashlib
import json
from pathlib import Path
from persist_retrospective_review import delivered
from verify_reader_delivery import text_parts


def inspect(packet_path, trace):
    packet=json.loads(packet_path.read_text(encoding='utf8')); rendered=set();outputs=[];models=set();finals=0;last=None
    def walk(value):
        if isinstance(value,dict):
            if value.get('type')=='input_image':
                url=value.get('image_url','')
                if isinstance(url,str) and url.startswith('data:'):rendered.add(hashlib.sha256(base64.b64decode(url.split(',',1)[1])).hexdigest())
            for v in value.values():walk(v)
        elif isinstance(value,list):
            for v in value:walk(v)
    for line in trace.open(encoding='utf8'):
        try: row=json.loads(line)
        except ValueError:continue
        last=row.get('timestamp',last);p=row.get('payload',{})
        if row.get('type')=='turn_context':models.add(p.get('model'))
        if p.get('type') in ('custom_tool_call_output','function_call_output'):outputs.append(text_parts(p.get('output','')))
        if p.get('type')=='message' and p.get('role')=='assistant' and p.get('phase') in ('final','final_answer'):finals+=1
        walk(row)
    texts=[Path(packet['sp_path']),packet_path]+[Path(x['path']) for x in packet['required_text']]
    missing=[str(p) for p in texts if not delivered(p.read_text(encoding='utf8'),outputs)]
    hashes={x['sha256'] for x in packet['required_images']}
    return dict(models=sorted(models),text_files_required=len(texts),text_files_fully_delivered=len(texts)-len(missing),missing_text=missing,
        required_unique_images=len(hashes),images_byte_verified=len(hashes & rendered),missing_image_hashes=sorted(hashes-rendered),
        actual_finals=finals,last_trace_event=last,scope='Delivery readiness only; no draft ratings exposed, no agent liveness or completion claim.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--input',type=Path,required=True);parser.add_argument('--trace',type=Path,required=True)
    args=parser.parse_args();print(json.dumps(inspect(args.input.resolve(),args.trace.resolve()),ensure_ascii=False))
