"""Compare planned new control coordinates/paths to captured actual Unity controls."""
import argparse,json
from collections import defaultdict
def main():
    from pathlib import Path
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);a=p.parse_args();out=a.out.resolve()
    controls=defaultdict(list)
    for file in (out/'candidate').glob('*.json'):
        for c in json.loads(file.read_text(encoding='utf8'))['observation']['controls']:controls[c['path']].append(c)
    compared=0;missing=set();errors=[];seen=set()
    new_prefixes=('Btn_Inline','Btn_TakeSource_','Btn_SourceTacticDetails','Btn_QuickSupport','Btn_QuickAssignUnit_','Btn_EndTurnDiscardOrdinary','Btn_DiscardOrdinaryEndFromHand','Btn_SelectAllTurnDiscards','Btn_EndTurnFromHand','Btn_SourcePage')
    for line in (out/'calculated_routes.jsonl').open(encoding='utf8'):
        for s in json.loads(line)['steps']:
            if not s['name'].startswith(new_prefixes):continue
            key=(s['path'],s['x'],s['y'],s['w'],s['h'])
            if key in seen:continue
            seen.add(key);actual=controls.get(s['path'],[])
            if not actual:missing.add(s['path']);continue
            compared+=1
            if not any(max(abs(s['x']-c['x']),abs(s['y']-c['y']),abs(s['w']-c['width']),abs(s['h']-c['height']))<.1 for c in actual):errors.append(dict(planned=s,actual=[{k:c[k] for k in ('path','x','y','width','height')} for c in actual[:3]]))
    result=dict(compared_distinct_geometries=compared,errors=errors,unobserved_controls=sorted(missing),scope='Actual Unity coordinates validate captured contexts only; missing edge-state controls remain unverified')
    (out/'geometry_validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8');print(result)
    if errors:raise AssertionError(errors)
if __name__=='__main__':main()
