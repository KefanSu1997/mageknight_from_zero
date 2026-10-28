"""Read native SoloCityGate exports; never manufacture combat observations."""
import argparse
import hashlib
import json
from pathlib import Path
from solo_candidate_entities import combat_entities, unit_entities, skill_entities
from solo_printed_catalog import SoloPrintedCatalog


def check(path, unit=False, skill=False):
    raw = Path(path).read_bytes()
    rows = json.loads(raw)
    if unit:
        rows = [dict(rows, kind='Unit')]
    counts = dict(Unit=0) if unit else dict(Skill=0) if skill else dict(Attack=0, Block=0, Damage=0)
    bound_entities = 0
    catalog = SoloPrintedCatalog(Path(__file__).resolve().parents[2])
    printed_references = 0
    for row in rows:
        for command in row['commands']:
            if unit:
                result = unit_entities(row['observation'], command)
            elif skill:
                result = skill_entities(row['observation'], command)
            else:
                result = combat_entities(row['observation'], row['kind'], command)
            for binding in result:
                # Independently follow the archived parameter and actual public zone.
                slot = command
                for key in binding['parameter']:
                    slot = slot[key]
                zone = row['observation']
                for key in binding['zone']:
                    zone = zone[key]
                assert slot == binding['slot']
                assert zone[slot] == binding['entity']
            described = catalog.describe_bindings(result)
            printed_references += sum(len(item['printed']) for item in described)
            counts[row['kind']] += 1
            bound_entities += len(result)
    assert all(counts.values()), counts
    return dict(passed=True, native_candidates=counts, bound_entities=bound_entities,
                printed_references=printed_references, printed_catalog_sha256=catalog.signature,
                unavailable_source_rows=catalog.unavailable,
                fixture_sha256=hashlib.sha256(raw).hexdigest(),
                scope='controlled native combat; not seven-hero conquest or CUDA training',
                training_started=False)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('fixtures')
    parser.add_argument('--out', required=True)
    parser.add_argument('--unit', action='store_true')
    parser.add_argument('--skill', action='store_true')
    args = parser.parse_args()
    if args.unit and args.skill:
        parser.error('Choose only one fixture type')
    result = check(args.fixtures, args.unit, args.skill)
    Path(args.out).write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result))
