"""Actual physical invocation modes, separate from raw menu/event history.

The original non-spell next-card enhancement overrides a basic menu choice.
No mode is guessed from choice spelling; unknowns remain explicit.
"""
import argparse
import json
from pathlib import Path
import numpy as np
from original_data_signature import data_signature
from task_feasibility import file_hash


def indexes(inventory, modes):
    categories = {c['id']: c['category'] for c in inventory['cards']}
    choices = {}
    for card in modes['cards']:
        for choice in card['choices']:
            key = card['id'], choice['choice']
            if key in choices and choices[key] != choice['mode']:
                raise ValueError('Ambiguous original native mode')
            choices[key] = choice['mode']
    return categories, choices


def committed_modes(trace, categories, choices):
    records, unknown = [], []
    destinations = ('played', 'removed', 'banners', 'roundAside')
    for step, row in enumerate(trace):
        before, after, action = row['before'], row['after'], row['action']
        kind = action['Kind']
        if kind not in ('play', 'artifact', 'skillShapeshift'):
            continue
        hands = {c['instance']: c for c in before['zones']['hand']}
        card = action['Card']
        if kind == 'skillShapeshift':
            slot = action['Parts']
            if not 0 <= slot < len(before['handOrder']):
                raise ValueError('Shapeshift lacks the exact physical hand slot')
            card = hands[before['handOrder'][slot]]['id']
        old_destinations = {c['instance'] for zone in destinations for c in before['zones'].get(zone, [])}
        moved = [c for zone in destinations for c in after['zones'].get(zone, [])
                 if c['instance'] not in old_destinations and c['instance'] in hands
                 and hands[c['instance']]['id'] == card]
        if not moved:
            continue  # No physical commitment evidence: never count a selection.
        if len(moved) != 1:
            raise ValueError('Ambiguous physical invocation, do not pick an arbitrary duplicate')
        try:
            category = categories[card]
            if kind == 'artifact':
                if category != 'items' or action['Choice'] not in ('normal', 'burn'):
                    raise ValueError('Unsupported original artifact mode')
                mode = action['Choice']
            else:
                if category in ('basic_card', 'advanced_card') and 'nextEnhanced' not in before:
                    raise ValueError('Missing actual free-enhancement state')
                if kind == 'skillShapeshift':
                    if category != 'basic_card' or action['Choice'] not in ('base', 'strong'):
                        raise ValueError('Unsupported original shapeshift host/mode')
                    mode = action['Choice']
                else:
                    mode = choices[(card, action['Choice'])]
                # ActionSystem freeEnhancedAction explicitly excludes spells.
                if category in ('basic_card', 'advanced_card') and before['nextEnhanced']:
                    mode = 'strong'
            records.append(dict(step=step, card=card, physical=moved[0]['instance'],
                                mode=mode, raw_choice=action['Choice']))
        except (KeyError, ValueError) as error:
            unknown.append(dict(step=step, card=card, raw_choice=action['Choice'], reason=str(error)))
    return records, unknown


def main():
    parser = argparse.ArgumentParser()
    for name in ('dll', 'modes', 'out'):
        parser.add_argument('--' + name, required=True)
    parser.add_argument('--inventory', default='AutomationConfigs/rl_full_coverage_inventory.json')
    parser.add_argument('--runs', nargs='+', required=True)
    args = parser.parse_args()
    out = Path(args.out)
    if out.exists():
        raise ValueError('Preserve previous effective mode evidence')
    mode_data = json.loads(Path(args.modes).read_text())
    if mode_data['rules_sha256'] != file_hash(args.dll) or mode_data['inventory_sha256'] != file_hash(args.inventory):
        raise ValueError('Original frozen metadata source mismatch')
    categories, choices = indexes(json.loads(Path(args.inventory).read_text()), mode_data)
    sources, seen, unknown = [], set(), []
    for folder in args.runs:
        with np.load(Path(folder) / 'checkpoint.npz', allow_pickle=False) as saved:
            metadata = json.loads(str(saved['header']))['metadata']
        if metadata['rules_sha256'] != file_hash(args.dll) or metadata['rules_data_sha256'] != data_signature()['sha256']:
            raise ValueError('Actual trace run rules/data differ from current frozen mode metadata')
        for path in sorted(Path(folder).glob('episode_*.json')):
            episode = json.loads(path.read_text(encoding='utf-8'))
            records, gaps = committed_modes(episode['trace'], categories, choices)
            seen.update(f'card_mode:{x["card"]}:{x["mode"]}' for x in records)
            unknown.extend(dict(path=str(path.resolve()), **x) for x in gaps)
            sources.append(dict(path=str(path.resolve()), sha256=file_hash(path),
                                task_id=episode['task_id'], seed=episode['seed'], records=records))
    result = dict(scope='Only supplied saved PPO traces; actual physical/effective modes, not full rule/fee/consumer or Unity acceptance; raw history unchanged',
        rules_sha256=file_hash(args.dll), inventory_sha256=file_hash(args.inventory),
        modes_sha256=file_hash(args.modes), code_sha256=file_hash(__file__),
        invoked_modes=sorted(seen), unknown=unknown, sources=sources)
    out.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(dict(traces=len(sources), effective_modes=len(seen), unknown=len(unknown))))


if __name__ == '__main__':
    main()
