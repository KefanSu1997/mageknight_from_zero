"""Pre-review geometry/glyph smoke gate; never substitutes for visual review."""
import argparse
import hashlib
import json
from pathlib import Path


def inspect(path):
    path = Path(path).resolve()
    raw = path.read_bytes()
    capture = json.loads(raw)
    observation = capture.get('observation') or {}
    evaluation = capture.get('evaluation') or {}
    failures = []
    if capture.get('success') is not True:
        failures.append(dict(kind='capture_failed', detail=capture.get('error')))
    if (observation.get('screenWidth'), observation.get('screenHeight')) != (1920, 1080):
        failures.append(dict(kind='canvas_changed', detail=[observation.get('screenWidth'), observation.get('screenHeight')]))
    if not evaluation or 'texts' not in evaluation:
        failures.append(dict(kind='diagnostics_missing'))
    checked = []
    for control in observation.get('controls', []):
        if not control.get('clickable'):
            continue
        prefix = control['path'] + '/'
        texts = [t for t in evaluation.get('texts', [])
                 if t['path'].startswith(prefix) and t.get('fullText', '').strip()]
        # Icon-only controls require separate preregistered discovery checks.
        # Do not invent a mandatory label for every illustrative clickable entity.
        for text in texts:
            row = dict(control=control['path'], path=text['path'], text=text['fullText'],
                       generated_characters=text.get('generatedCharacterCount'),
                       has_visible_glyphs=text.get('hasVisibleGlyphs'),
                       overflowing=text.get('isTextOverflowing'), font_size=text.get('fontSize'))
            checked.append(row)
            if text.get('generatedCharacterCount', 0) == 0 or text.get('hasVisibleGlyphs') is False:
                failures.append(dict(kind='clickable_control_text_has_no_glyphs', **row))
            if text.get('isTextOverflowing') is True:
                failures.append(dict(kind='clickable_control_text_overflow', **row))
    return dict(capture=str(path), sha256=hashlib.sha256(raw).hexdigest(),
                passed=not failures, checked_control_texts=checked, failures=failures)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--capture', action='append', required=True, type=Path)
    parser.add_argument('--out', required=True, type=Path)
    args = parser.parse_args()
    rows = [inspect(p) for p in args.capture]
    result = dict(passed=all(x['passed'] for x in rows), rows=rows,
                  scope='Only supplied captures, fixed canvas and clickable text geometry. '
                        'No global font-size comparison, no blanket ban on moving tutorials to Wiki. '
                        'Glyph metadata cannot prove pixel visibility, aesthetics, information completeness, '
                        'icon discoverability, legal consumption, latency or UI acceptance.')
    args.out.parent.mkdir(parents=True, exist_ok=True)
    with args.out.open('x', encoding='utf8') as stream:
        json.dump(result, stream, ensure_ascii=False, indent=2)
    print(json.dumps(dict(passed=result['passed'], captures=len(rows),
                          failure_count=sum(len(r['failures']) for r in rows), out=str(args.out))))
    raise SystemExit(0 if result['passed'] else 1)
