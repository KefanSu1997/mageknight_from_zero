"""Audit candidate external tile facts against the independently verified opening.

Produces reference evidence only. Never updates the runtime catalog or marks
unseen tiles verified. The input is a pinned, separately checked-out repository.
"""
import argparse
import hashlib
import json
import re
from pathlib import Path

COORDS = dict(CENTER=(0, 0), NE=(1, -1), E=(1, 0), SE=(0, 1),
              SW=(-1, 1), W=(-1, 0), NW=(0, -1))


def audit(reference, verified):
    text = reference.read_text(encoding='utf-8')
    arrays = re.findall(r'pub static (\w+): &\[TileHex\] = &\[(.*?)\n\];', text, re.S)
    candidates = {}
    for name, body in arrays:
        cells = {}
        for row in re.findall(r'TileHex \{([^\n]+)\}', body):
            local = re.search(r'local: (\w+)', row).group(1)
            terrain = re.search(r'terrain: Terrain::(\w+)', row).group(1)
            site_match = re.search(r'site_type: Some\(SiteType::(\w+)\)', row)
            site = site_match.group(1) if site_match else None
            if 'RampagingEnemyType::OrcMarauder' in row:
                if site:
                    raise ValueError('Combined site/rampager needs explicit mapping')
                site = 'RampagingOrcs'
            if site == 'Mine':
                site = 'CrystalMine'
            color = re.search(r'mine_color: Some\(BasicManaColor::(\w+)\)', row)
            cells[COORDS[local]] = dict(terrain=terrain, site=site,
                                       manaColor=color.group(1) if color else None)
        if len(cells) != 7:
            raise ValueError(f'{name} does not have seven distinct cells')
        candidates[name] = cells
    opening = json.loads(verified.read_text(encoding='utf-8-sig'))
    differences = []
    compared = 0
    for tile in opening['tiles']:
        name = 'STARTING_TILE_A' if tile['id'] == 'A' else 'COUNTRYSIDE_' + tile['id']
        for cell in tile['cells']:
            actual = candidates[name][cell['q'], cell['r']]
            for field in ('terrain', 'site', 'manaColor'):
                compared += 1
                if cell.get(field) != actual[field]:
                    differences.append(dict(tile=tile['id'], q=cell['q'], r=cell['r'],
                                            field=field, verified=cell.get(field), candidate=actual[field]))
    return dict(status='reference_only_not_approved_for_runtime',
                source_sha256=hashlib.sha256(reference.read_bytes()).hexdigest(),
                verified_sha256=hashlib.sha256(verified.read_bytes()).hexdigest(),
                candidate_tiles=list(candidates), fields_compared=compared,
                differences=differences,
                unseen_tiles_verified=False,
                next_requirement='Verify remaining tile terrain/sites against original artwork; preserve Sea exclusion')


if __name__ == '__main__':
    p = argparse.ArgumentParser()
    p.add_argument('--reference', type=Path, required=True)
    p.add_argument('--verified', type=Path, required=True)
    p.add_argument('--out', type=Path, required=True)
    args = p.parse_args()
    result = audit(args.reference, args.verified)
    args.out.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(result, ensure_ascii=False, indent=2))
