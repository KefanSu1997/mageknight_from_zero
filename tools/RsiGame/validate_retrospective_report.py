"""Check report links, canvas sizes, CSV fidelity and JavaScript syntax.

Static checks only: this does not claim browser rendering or score accuracy.
"""
import argparse
import collections
import csv
import json
import struct
import subprocess
from pathlib import Path


def validate(root, node):
    html = (root / 'index.html').read_text(encoding='utf8')
    script = html.split('<script>', 1)[1].split('</script>', 1)[0]
    data = json.loads(script.split('const D=', 1)[1].split(';const $=', 1)[0])
    errors, counts = [], collections.Counter()
    for n, review in data['results'].items():
        for section in ('items', 'hard_failures'):
            for item in review[section]:
                for path in item.get('evidence', []):
                    url = data['path_lookup'].get(path.replace('\\', '/'))
                    if not url or not (root / url).is_file():
                        errors.append(f'Missing clickable evidence R{n}: {path}')
                    counts[section + '_evidence_links'] += 1
    for packet in data['packets'].values():
        for image in packet['required_images']:
            path = root / image['url']
            raw = path.read_bytes()
            if raw[:8] != b'\x89PNG\r\n\x1a\n' or struct.unpack('>II', raw[16:24]) != (1920, 1080):
                errors.append('Unexpected canvas: ' + str(path))
            counts['required_image_paths'] += 1
    with (root / 'scores.csv').open(encoding='utf-8-sig', newline='') as handle:
        rows = list(csv.DictReader(handle))
    if len(rows) != 384:
        errors.append('CSV must contain exactly 384 rows')
    for row in rows:
        review = data['results'].get(row['round'])
        if not review:
            counts['pending_csv_items'] += 1
            continue
        item = next(x for x in review['items'] if x['id'] == row['item_id'])
        score = '' if item['score'] is None else str(item['score'])
        if (row['score'], row['reason'], row['status']) != (score, item['reason'], item['status']):
            errors.append(f"CSV differs from final R{row['round']}/{item['id']}")
        counts['csv_items_matched'] += 1
    script_path = root / 'report_script.check.js'
    script_path.write_text(script, encoding='utf8')
    process = subprocess.run([str(node), '--check', str(script_path)], capture_output=True, text=True)
    if process.returncode:
        errors.append(process.stderr)
    result = dict(scope=__doc__, verified_rounds=len(data['results']), counts=dict(counts),
                  javascript_syntax_passed=process.returncode == 0, errors=errors)
    (root / 'report_static_validation.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf8')
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--node', type=Path, required=True)
    args = parser.parse_args()
    result = validate(args.root.resolve(), args.node)
    print(json.dumps(result, ensure_ascii=False))
    raise SystemExit(bool(result['errors']))
