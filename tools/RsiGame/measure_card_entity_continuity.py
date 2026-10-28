"""Measure native card-instance continuity in saved real input pairs; no grading."""
import argparse
import hashlib
import json
from pathlib import Path


def read(p):return json.loads(p.read_text(encoding='utf8'))


def native(observation):
    return json.loads(next(x['value'] for x in observation['state'] if x['key']=='audit.costNativeState'))


def inventory(value):
    result={};duplicates=[]
    for zone,entries in value['zones'].items():
        for card in entries:
            key=card['instance']
            if type(key) is not int:raise ValueError('Native physical instance number missing')
            if key in result:duplicates.append(key)
            result[key]=dict(zone=zone,id=card['id'],wound=card['wound'])
    return result,duplicates


def measure(inputs):
    rows=[];sources=[]
    for path in inputs:
        sources.append(dict(path=str(path.resolve()),sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
        for event in read(path)['events']:
            before,after=native(event['before']),native(event['after'])
            a,ad=inventory(before);b,bd=inventory(after)
            retained=set(a)&set(b)
            changed=[key for key in retained if any(a[key][field]!=b[key][field] for field in ['id','wound'])]
            rows.append(dict(request_id=event['request_id'],control=event['name'],source=str(path.resolve()),
                before_phase=before['phase'],after_phase=after['phase'],phase_transition=before['phase']!=after['phase'],
                expected_existing_card_instances=len(a),retained_existing_card_instances=len(retained),
                missing_instances=sorted(set(a)-set(b)),new_instances=sorted(set(b)-set(a)),
                changed_catalog_or_wound_identity=changed,duplicate_before=ad,duplicate_after=bd,
                zone_transfers=[dict(instance=i,card=a[i]['id'],before=a[i]['zone'],after=b[i]['zone']) for i in sorted(retained) if a[i]['zone']!=b[i]['zone']],
                hero_identifier_unchanged=before['hero']==after['hero']))
    boundaries=[x for x in rows if x['phase_transition']]
    def aggregate(events):
        return dict(actual_input_pairs=len(events),expected_existing_card_instance_pairs=sum(x['expected_existing_card_instances'] for x in events),
                    retained_existing_card_instance_pairs=sum(x['retained_existing_card_instances'] for x in events),
                    missing_or_changed_pairs=sum(len(x['missing_instances'])+len(x['changed_catalog_or_wound_identity']) for x in events),
                    duplicated_instances=sum(len(x['duplicate_before'])+len(x['duplicate_after']) for x in events))
    return dict(schema='scoped-native-card-continuity-v1',sources=sources,all_saved_input_pairs=aggregate(rows),phase_boundary_pairs=aggregate(boundaries),events=rows,
        score=None,unit='native card-instance observation pairs',
        scope='Only these saved actual before/after pairs. Native instance numbers refer to audit reference identity within each fixture; resets between event groups are never compared. Not UI control names or card catalog IDs as identity.',
        missing=['Physical unit instance identities unavailable in saved unit rows; not inferred from unit catalog IDs.',
                 'No all-game transition denominator, independent pixel occlusion proof, human behavior or numerical score anchors.',
                 'Produced after cycle02 review input freeze; supplemental measurement, not evidence read by that reviewer.'])


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--inputs',type=Path,nargs='+',required=True);parser.add_argument('--out',type=Path,required=True)
    a=parser.parse_args();result=measure(a.inputs);a.out.parent.mkdir(parents=True,exist_ok=True)
    with a.out.open('x',encoding='utf8') as stream:json.dump(result,stream,ensure_ascii=False,indent=2)
    print(json.dumps({k:result[k] for k in ['all_saved_input_pairs','phase_boundary_pairs','score']},ensure_ascii=False))
