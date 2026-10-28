"""Inventory gaps from one run's actual-use counters; never infer acceptance."""
import argparse, hashlib, json
from pathlib import Path

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('run')
    args=parser.parse_args()
    run=Path(args.run)
    source=run/'training_coverage.json'
    inventory_path=Path('AutomationConfigs/rl_full_coverage_inventory.json')
    inventory=json.loads(inventory_path.read_text(encoding='utf-8'))
    coverage=json.loads(source.read_text(encoding='utf-8'))
    effects=set(coverage['effects'])
    skills={':'.join(entry.split(':')[:2]) for entry in coverage['skills']}
    skill_ids={s['SourceId'] for hero in inventory['heroes'] for s in hero['skills']}
    categories={}
    for category in sorted({c['category'] for c in inventory['cards']}):
        cards=[c for c in inventory['cards'] if c['category']==category]
        if category.startswith('army'):
            seen=set(coverage['units'])
            categories[category]={'total_ids':len(cards),'actual_activation_ids':len({c['id'] for c in cards}&seen),'missing_ids':[c['id'] for c in cards if c['id'] not in seen],'limits':'ability modes, printed fees, target consumers and UI must be verified separately'}
        else:
            present={entry.split(':')[0] for entry in effects}
            missing_modes=[c['id']+':'+mode for c in cards for mode in c['modes'] if c['id']+':'+mode not in effects]
            categories[category]={'total_ids':len(cards),'actual_effect_ids':len({c['id'] for c in cards}&present),'missing_ids':[c['id'] for c in cards if c['id'] not in present],'exact_primary_modes_missing':missing_modes,'limits':'a missing exact mode may use a specialized choice label; a recorded effect is not proof of printed fees or downstream use'}
    expected_enemies={e['Id'] for e in inventory['enemies']}
    result={'run':str(run),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'inventory_sha256':hashlib.sha256(inventory_path.read_bytes()).hexdigest(),'categories':categories,'native_skills':{'total_ids':len(skill_ids),'recorded_activation_ids':len(skill_ids&skills),'missing_ids':sorted(skill_ids-skills),'limits':'recorded activation only; acquisitions, choices, physical costs and downstream consumers still require evidence'},'enemies':{'total_ids':len(expected_enemies),'recorded_encounter_ids':len(expected_enemies&set(coverage['enemies'])),'missing_ids':sorted(expected_enemies-set(coverage['enemies'])),'limits':'encountering is not defeating; original map/site contexts remain separate'},'heroes':coverage['heroes'],'limits':'single-run counter snapshot, no aggregation across incompatible rule versions; not rules/UI/held-out acceptance'}
    (run/'inventory_gap_snapshot.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'categories':{k:{field:value for field,value in row.items() if field in ('total_ids','actual_activation_ids','actual_effect_ids')} for k,row in categories.items()},'skills':{k:v for k,v in result['native_skills'].items() if k.endswith('_ids') and isinstance(v,int)},'enemy_encounters':result['enemies']['recorded_encounter_ids'],'scope':result['limits']},ensure_ascii=False))

if __name__=='__main__':main()
