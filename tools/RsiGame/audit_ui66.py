"""Auditable same-state UI66 presentation comparison, no live Unity operations."""
import argparse
import json
from pathlib import Path


def read(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--before-prefix', default='baseline65')
    parser.add_argument('--after-prefix', default='final')
    args = parser.parse_args()
    root = args.root.resolve()
    pairs = []
    for before, after in [(args.before_prefix + '_' + family, args.after_prefix + '_' + family) for family in ['field', 'modes', 'pressure']]:
        a = {r['label']: r for r in read(root / before / 'manifest.json')['captures']}
        b = {r['label']: r for r in read(root / after / 'manifest.json')['captures']}
        for label in sorted(a.keys() & b.keys()):
            x, y = read(Path(a[label]['capture'])), read(Path(b[label]['capture']))
            native = lambda d: next(v['value'] for v in d['observation']['state'] if v['key'] == 'audit.costNativeState')
            raw = lambda d: {r['path']: r for r in d['evaluation']['rawImages']}
            controls = lambda d: {(r['name'], (r.get('text') or '').replace('\n', '').replace('\r', '')): (r['interactable'], r['clickable']) for r in d['observation']['controls']}
            old_captions = {c['name']: c.get('text') for c in x['observation']['controls']}
            linebreak_changes = [dict(name=c['name'], before=old_captions[c['name']], after=c.get('text')) for c in y['observation']['controls'] if c['name'] in old_captions and c.get('text') != old_captions[c['name']]]
            old_fonts = {t['path']: t['fontSize'] for t in x['evaluation']['texts']}
            text_failures = []
            for t in y['evaluation']['texts']:
                if not t['fullText'] or t['effectiveRendererAlpha'] <= .01:
                    continue
                targeted = t['name'] in ['Name', 'Identity', 'HeroName', 'Dock66ActualFees', 'Dock66ModeHeading', 'Dock66SourceHeading', 'Value', 'Meaning', 'InspectorLabel'] or ('/Inspector/Actions/' in t['path'] and t['name'] == 'Label')
                if not targeted:
                    continue
                required = {i for i, c in enumerate(t['fullText']) if not c.isspace()}
                missing = required - {g['sourceIndex'] for g in t['glyphs']}
                floor = old_fonts.get(t['path'], 21) if '/Hand/' in t['path'] else 21
                if missing or t['isTextOverflowing'] or t['fontSize'] < floor:
                    text_failures.append(dict(path=t['path'], missing=sorted(missing), overflow=t['isTextOverflowing'], font=t['fontSize'], floor=floor))
            active = [c for c in y['observation']['controls'] if c['clickable'] and '/Inspector/' in c['path']]
            overlaps = []
            for i, c in enumerate(active):
                for v in active[i + 1:]:
                    dx = (c['width'] + v['width']) / 2 - abs(c['x'] - v['x'])
                    dy = (c['height'] + v['height']) / 2 - abs(c['y'] - v['y'])
                    if dx > .1 and dy > .1:
                        overlaps.append([c['name'], v['name'], dx, dy])
            pairs.append(dict(label=label, family=after, native_equal=native(x) == native(y), raw_records_equal=raw(x) == raw(y), control_semantics_equal=controls(x) == controls(y), caption_changes=linebreak_changes, text_failures=text_failures, inspector_hit_overlaps=overlaps, min_inspector_hit_short_side=min((min(c['width'], c['height']) for c in active), default=None)))
    passed = bool(pairs) and all(r['native_equal'] and r['raw_records_equal'] and r['control_semantics_equal'] and not r['text_failures'] and not r['inspector_hit_overlaps'] for r in pairs)
    result = dict(passed=passed, pairs=pairs, scope='Paired serialized native/RawImage records and named text glyphs; control caption comparison ignores only line breaks, all raw caption changes retained; Inspector clickable hit rectangles only. Existing hand font preserved; new targeted fields floor21. Not pixel/background, human, full-screen hit, full33 or global workload acceptance.')
    (root / 'reproducible_audit.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    assert passed, 'See reproducible_audit.json'
    print(f'{len(pairs)} actual paired captures passed scoped preservation/text/Inspector audit')


if __name__ == '__main__':
    main()
