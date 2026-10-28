"""Compare same-fixture raw states, text geometry and fixed canvas; not a visual grade."""
import argparse
import json
from pathlib import Path


def read(p): return json.loads(p.read_text(encoding='utf8'))


def compare(root):
    candidate=read(root/'cycle.json')['candidate']; rows=[]; decreases=[]; changed=[]
    for p in sorted((root/candidate/'key_phases').glob('*/capture.json')):
        a=read(root/'before/key_phases'/p.parent.name/'capture.json');b=read(p)
        def native(d):
            return json.loads(next(x['value'] for x in d['observation']['state'] if x['key']=='audit.costNativeState'))
        ta={x['path']:x for x in a['evaluation']['texts']};tb={x['path']:x for x in b['evaluation']['texts']}
        for path in ta.keys() & tb.keys():
            if tb[path]['fontSize']+0.01 < ta[path]['fontSize']:
                decreases.append(dict(context=p.parent.name,path=path,before=ta[path]['fontSize'],after=tb[path]['fontSize']))
            if ta[path]['screenRect']!=tb[path]['screenRect']:
                changed.append(dict(context=p.parent.name,path=path,before=ta[path]['screenRect'],after=tb[path]['screenRect']))
        rows.append(dict(context=p.parent.name,native_state_identical=native(a)==native(b),
                         canvas_1920x1080=all(d['evaluation']['screenWidth']==1920 and d['evaluation']['screenHeight']==1080 for d in (a,b))))
    value=dict(contexts=rows,all17_native_states_identical=len(rows)==17 and all(x['native_state_identical'] for x in rows),
               canvas_unchanged=all(x['canvas_1920x1080'] for x in rows),font_decreases=decreases,text_rect_changes=changed,
               scope='Same captured contexts only. Intentional inspector context changes need independent visual review; no absence-of-occlusion claim.')
    with (root/'anti_regression_checks.json').open('x',encoding='utf8') as stream:json.dump(value,stream,ensure_ascii=False,indent=2)
    return {k:v for k,v in value.items() if k not in ('contexts','text_rect_changes') } | dict(changed_rectangles=len(changed))


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--root',type=Path,required=True)
    print(json.dumps(compare(parser.parse_args().root.resolve()),ensure_ascii=False))
