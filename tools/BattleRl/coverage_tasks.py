"""Coverage-oriented independent task drafts, never evidence of actual use.

All drafts start unknown and are excluded from victory training until the real
rules admit the build and a complete-task solvability witness is certified.
The initial design covers hero/enemy pairs and original ID/mode intentions;
specific fee/material/consumer obligations remain independently visible.
"""
import argparse
import hashlib
import json
from pathlib import Path

FAMILIES = ('physical', 'elemental', 'ranged_siege', 'block_heal',
            'draw_cycle', 'mana', 'units', 'control')
STREAMS = ('RandomSeed', 'InventorySeed', 'ShuffleSeed', 'ManaSeed', 'SupplySeed',
           'ProgressionSeed', 'EffectSeed', 'SummonSeed')
SUPPORT = {
    'physical': ['advanced_card_004', 'advanced_card_028'],
    'elemental': ['magic_009', 'magic_015'],
    'ranged_siege': ['advanced_card_003', 'advanced_card_002'],
    'block_heal': ['advanced_card_005', 'magic_005'],
    'draw_cycle': ['magic_002', 'advanced_card_032'],
    'mana': ['advanced_card_022', 'magic_012'],
    'units': ['advanced_card_016', 'advanced_card_015'],
    'control': ['magic_019', 'magic_022'],
}


def digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(',', ':')).encode()).hexdigest()


def combination(task):
    # Orders and RNG variants of a related composition stay in the same split.
    build = task.get('Build')
    if build:
        build = {k: v for k, v in build.items() if k != 'Family'}
        build = build | {key: sorted(build[key]) for key in ('AdditionalCards', 'UnitIds', 'SkillIds')}
    return dict(hero=task['HeroId'], opening=task['OriginalOpening'], build=build,
                enemy_groups=sorted(sorted(group) for group in task['EnemyGroups']))


def split(task):
    bucket = int(digest(combination(task))[:8], 16) % 10
    return 'test' if bucket == 9 else 'validation' if bucket == 8 else 'train'


def planned_keys(task, inventory):
    hero = next(h for h in inventory['heroes'] if h['Id'] == task['HeroId'])
    keys = {f"hero_enemy:{hero['Id']}:{enemy}" for group in task['EnemyGroups'] for enemy in group}
    build = task.get('Build')
    ids = set(hero['StartingCards']) | set(build['AdditionalCards'] if build else [])
    for card in inventory['cards']:
        if card['id'] in ids:
            keys.update(f"card_mode:{card['id']}:{mode}" for mode in card['modes'])
    if build:
        keys.add(f"hero_family:{hero['Id']}:{build['Family']}")
        keys.update('unit_id:' + unit for unit in build['UnitIds'])
        keys.update('own_skill:' + skill for skill in build['SkillIds'])
    else:
        keys.add('original_opening:' + hero['Id'])
    return keys


def requirements(inventory):
    result = {f"hero_enemy:{h['Id']}:{e['Id']}" for h in inventory['heroes'] for e in inventory['enemies']}
    result.update(f"card_mode:{c['id']}:{mode}" for c in inventory['cards']
                  if c['category'] not in ('army', 'army_2') for mode in c['modes'])
    result.update('unit_id:' + c['id'] for c in inventory['cards'] if c['category'] in ('army', 'army_2'))
    result.update('own_skill:' + s['SourceId'] for h in inventory['heroes'] for s in h['skills'])
    result.update(f"hero_family:{h['Id']}:{family}" for h in inventory['heroes'] for family in FAMILIES)
    result.update('original_opening:' + h['Id'] for h in inventory['heroes'])
    return result


def drafts(inventory, master_seed=7):
    extras = [c['id'] for c in inventory['cards'] if c['category'] in ('advanced_card', 'magic', 'items')]
    units = [c['id'] for c in inventory['cards'] if c['category'] in ('army', 'army_2')]
    enemies = [e['Id'] for e in inventory['enemies']]
    rows = max((len(extras) + 2) // 3, (len(enemies) + 1) // 2)
    result = []
    for hero in inventory['heroes']:
        own = [s['SourceId'] for s in hero['skills']]
        for row in range(rows + 1):
            seeds = {name: int(hashlib.sha256(f'{master_seed}/{row}/{name}'.encode()).hexdigest()[:8], 16)
                     % (2**31 - 256) for name in STREAMS}
            opening = row == rows
            primary = extras[(row % ((len(extras) + 2) // 3))*3:][:3] if not opening else []
            family = FAMILIES[row % len(FAMILIES)]
            additional = list(dict.fromkeys(primary + SUPPORT[family])) if not opening else []
            # Strong spells require the actual night lifecycle, not a granted black crystal.
            groups = 6 if any(c.startswith('magic_') for c in additional) else 2
            task = dict(HeroId=hero['Id'], OriginalOpening=opening, **seeds,
                        EnemyGroups=[[enemies[(row*2+i) % len(enemies)]] for i in range(1 if opening else groups)])
            if not opening:
                task['Build'] = dict(Family=family, Fame=24,
                                     AdditionalCards=additional,
                                     UnitIds=[units[(row*3+i) % len(units)] for i in range(3)],
                                     SkillIds=[own[(row*2+i) % len(own)] for i in range(2)],
                                     Crystals={'Red': 3, 'Blue': 3, 'Green': 3, 'White': 3}, InitialWounds=2)
            keys = planned_keys(task, inventory)
            result.append(dict(task_id=digest(task), task_definition=task, split=split(task),
                               status='unknown', excluded_from_victory_pool=True,
                               planned_keys=sorted(keys), estimated_cost=len(task['EnemyGroups']),
                               scope='Declared membership intentions only; no actual activation or solvability credit'))
    # Plug training pair gaps with genuinely different compositions, never move
    # a withheld validation/test combination into the training split.
    train_planned = set().union(*(set(c['planned_keys']) for c in result if c['split']=='train'))
    missing_pairs = sorted(k for k in requirements(inventory) - train_planned if k.startswith('hero_enemy:'))
    artifacts = [c['id'] for c in inventory['cards'] if c['category']=='items']
    for index, key in enumerate(missing_pairs):
        _, hero_id, enemy = key.split(':')
        hero = next(h for h in inventory['heroes'] if h['Id']==hero_id)
        seeds = {name: int(hashlib.sha256(f'{master_seed}/anchor/{index}/{name}'.encode()).hexdigest()[:8],16)
                 % (2**31-256) for name in STREAMS}
        for artifact in artifacts:
            candidate = dict(HeroId=hero_id, OriginalOpening=False, **seeds,
                             EnemyGroups=[[enemy]], Build=dict(Family='elemental', Fame=24,
                                 AdditionalCards=['advanced_card_004','magic_009','magic_015',artifact],
                                 UnitIds=['army_003','army_005','army_013'],
                                 SkillIds=[s['SourceId'] for s in hero['skills'][:2]],
                                 Crystals={'Red':3,'Blue':3,'Green':3,'White':3},InitialWounds=2))
            if split(candidate)=='train':
                result.append(dict(task_id=digest(candidate),task_definition=candidate,split='train',
                                   status='unknown',excluded_from_victory_pool=True,
                                   planned_keys=sorted(planned_keys(candidate,inventory)),estimated_cost=1,
                                   scope='Additional distinct training composition for a missing hero-enemy pair'))
                break
    return result


def greedy_cover(candidates, desired):
    remaining = set(desired)
    selected = []
    candidates = list(candidates)
    while candidates:
        def score(row):
            gain = remaining.intersection(row['planned_keys'])
            weight = sum(2 if k.startswith(('card_mode:', 'own_skill:')) else 1 for k in gain)
            return weight / row['estimated_cost'], len(gain), row['task_id']
        best = max(candidates, key=score)
        gain = remaining.intersection(best['planned_keys'])
        if not gain:
            break
        selected.append(best)
        remaining.difference_update(gain)
        candidates.remove(best)
    return selected, sorted(remaining)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--inventory', default='AutomationConfigs/rl_full_coverage_inventory.json')
    parser.add_argument('--out', required=True)
    args = parser.parse_args()
    inventory = json.loads(Path(args.inventory).read_text(encoding='utf-8-sig'))
    candidates = drafts(inventory)
    # Test/validation compositions are never borrowed to plug training coverage gaps.
    selected, missing = greedy_cover([c for c in candidates if c['split']=='train'], requirements(inventory))
    result = dict(schema_version=1, status='drafts_only_not_solvable_training_pool',
                  candidates=candidates, selected_train_drafts=[c['task_id'] for c in selected],
                  missing_train_planned_keys=missing,
                  pending=['real_reset_validation', 'complete_task_feasibility_witnesses_or_proofs',
                           'detailed_fee_material_consumer_interactions', 'level_strata_beyond_midgame',
                           'world_site_contexts', 'runtime_actual_coverage_sampling'])
    path = Path(args.out)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(dict(drafts=len(candidates), selected_train=len(selected), missing_train=len(missing))))


if __name__ == '__main__':
    main()
