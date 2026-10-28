"""Conservative invocation deficits; loaded/planned cards are never coverage."""
import argparse
import json
from pathlib import Path
import numpy as np
from original_data_signature import data_signature
from task_feasibility import digest,file_hash


def coverage(events,inventory,mode_map):
    choices={}
    for card in mode_map['cards']:
        for choice in card['choices']:
            key=card['id'],choice['choice']
            if key in choices and choices[key]!=choice['mode']:
                raise ValueError('Ambiguous native choice mode; do not guess from spelling')
            choices[key]=choice['mode']
    categories={card['id']:card['category'] for card in inventory['cards']}
    modes,units,skills,encounters=set(),set(),set(),set()
    unresolved=[]
    for key,count in events.items():
        if count<=0:
            continue
        if key.startswith('card_mode:'):
            _,card,choice=key.split(':',2)
            mode=choices.get((card,choice))
            if categories.get(card)=='basic_card' and choice in ('shapeshift_base','shapeshift_strong'):
                mode={'shapeshift_base':'base','shapeshift_strong':'strong'}[choice]
            if categories.get(card)=='items' and choice in ('normal','burn'):
                mode=choice  # Explicit normal/burn protocol, not prefix guessing.
            if mode is None:
                unresolved.append(key)
            else:
                modes.add(f'card_mode:{card}:{mode}')
        elif key.startswith('unit_invocation:'):
            units.add('unit_id:'+key.split(':')[1])
        elif key.startswith('skill_invocation:'):
            # Original skill IDs contain a colon; the final field is Option.
            skills.add('own_skill:'+key[len('skill_invocation:'):].rsplit(':',1)[0])
        elif key.startswith('hero_enemy_encounter:'):
            encounters.add('hero_enemy:'+key[len('hero_enemy_encounter:'):])
    required_modes={f'card_mode:{card["id"]}:{mode}' for card in inventory['cards']
                    if card['category'] in ('basic_card','advanced_card','magic','items') for mode in card['modes']}
    required_units={'unit_id:'+card['id'] for card in inventory['cards'] if card['category'].startswith('army')}
    required_skills={'own_skill:'+s['SourceId'] for hero in inventory['heroes'] for s in hero['skills']}
    required_pairs={f'hero_enemy:{hero["Id"]}:{enemy["Id"]}' for hero in inventory['heroes'] for enemy in inventory['enemies']}
    # Existing source audit: these directory IDs are not in any of the seven
    # official starting decks. Keep their evidence/gaps; never assign them to
    # a hero merely to fill a numerical coverage target.
    source_gaps={f'card_mode:{card}:{mode}' for card in ('basic_card_011','basic_card_025') for mode in ('base','strong')}
    return dict(invoked_modes=sorted(modes),invoked_units=sorted(units),invoked_skills=sorted(skills),
                encountered_pairs=sorted(encounters),unresolved_choice_events=unresolved,
                missing_modes=sorted(required_modes-modes),missing_units=sorted(required_units-units),
                missing_skills=sorted(required_skills-skills),missing_pairs=sorted(required_pairs-encounters),
                source_eligibility_gaps=sorted(source_gaps-modes),
                missing_modes_with_source_eligibility=sorted((required_modes-modes)-source_gaps),
                scope='Conservative actual invocation/encounter lower bound; fees/materials/consumers and Unity still required')


def rank_drafts(rows,report):
    missing=set(report['missing_modes']+report['missing_units']+report['missing_skills']+report['missing_pairs'])
    ranked=[]
    for row in rows:
        if row['split']!='train':
            continue
        gain=sorted(missing.intersection(row['planned_keys']))
        if gain:
            ranked.append(dict(draft_id=row['task_id'],planned_gain=len(gain),estimated_cost=row['estimated_cost'],
                               score=len(gain)/row['estimated_cost'],missing_intentions=gain,
                               task_definition=row['task_definition'],
                               status='candidate_only_not_coverage_or_solvable'))
    return sorted(ranked,key=lambda r:(-r['score'],-r['planned_gain'],r['draft_id']))


def validate_actual_event_source(source,header,dll_sha):
    if header['metadata']['rules_sha256']!=dll_sha or header['replay_state']['sampler']!=source:
        raise ValueError('Coverage events differ from saved real training history or rule signature')
    if 'rules_data_sha256' in header['metadata'] and header['metadata']['rules_data_sha256']!=data_signature()['sha256']:
        raise ValueError('Current runtime JSON differs from saved training data')


def main():
    parser=argparse.ArgumentParser()
    for name in ('events','modes','drafts','dll','out'):
        parser.add_argument('--'+name,required=True)
    parser.add_argument('--inventory',default='AutomationConfigs/rl_full_coverage_inventory.json')
    args=parser.parse_args()
    source=json.loads(Path(args.events).read_text())
    checkpoint=Path(args.events).parent/'checkpoint.npz'
    with np.load(checkpoint,allow_pickle=False) as arrays:
        header=json.loads(str(arrays['header']))
    validate_actual_event_source(source,header,file_hash(args.dll))
    mode_map=json.loads(Path(args.modes).read_text())
    if mode_map['rules_sha256']!=file_hash(args.dll) or mode_map['inventory_sha256']!=file_hash(args.inventory):
        raise ValueError('Frozen mode metadata does not match actual rules/inventory')
    report=coverage(source['events'],json.loads(Path(args.inventory).read_text()),mode_map)
    binding=header['replay_state'].get('data_binding')
    report['historical_data_version_recorded']='rules_data_sha256' in header['metadata'] and binding is None
    if binding is not None:
        legacy=binding['legacy_events']
        current={key:value-legacy.get(key,0) for key,value in source['events'].items()}
        if any(value<0 for value in current.values()):
            raise ValueError('Legacy history counts exceed cumulative real events')
        report['current_data_bound_invocations']=coverage(current,json.loads(Path(args.inventory).read_text()),mode_map)
        report['legacy_data_unbound_through_episode']=binding['legacy_data_unbound_through_episode']
    report['ranked_train_candidates']=rank_drafts(json.loads(Path(args.drafts).read_text())['candidates'],report)
    report['sources']={name:dict(path=str(Path(path).resolve()),sha256=file_hash(path)) for name,path in
                       (('events',args.events),('modes',args.modes),('drafts',args.drafts),('rules',args.dll))}
    report['sources']['training_checkpoint']=dict(path=str(checkpoint.resolve()),sha256=file_hash(checkpoint))
    audit=Path('vibe_coding/codex/original_hero_start_source_audit_2026-09-23.md')
    report['source_eligibility_audit']=dict(path=str(audit.resolve()),sha256=file_hash(audit))
    output=Path(args.out)
    if output.exists():
        raise ValueError('Preserve actual coverage evidence')
    output.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({key:len(report[key]) for key in ('invoked_modes','missing_modes','invoked_units','invoked_skills','encountered_pairs','unresolved_choice_events')}))


if __name__=='__main__':
    main()
