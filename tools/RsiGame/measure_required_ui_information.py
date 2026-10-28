"""Measure a predeclared, limited UI information inventory; never score or infer human performance.
Glyph coverage is generated-quad evidence, NOT an unobscured-pixel/readability verdict.
"""
import argparse
import hashlib
import json
import re

from PIL import Image
from pathlib import Path


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def measure_text(item, width, height):
    full = item.get('fullText', '')
    # TMP sourceIndex counts UTF-16 code units. Skip rich-text markup and whitespace.
    markup = set()
    for match in re.finditer(r'<[^>]*>', full):
        markup.update(range(match.start(), match.end()))
    expected, offset = {}, 0
    for index, char in enumerate(full):
        if not char.isspace() and index not in markup:
            expected[offset] = char
        offset += len(char.encode('utf-16-le')) // 2
    generated, onscreen, mismatches = set(), set(), []
    for glyph in item.get('glyphs', []):
        index = glyph.get('sourceIndex')
        if index not in expected:
            continue
        if glyph.get('character') != expected[index]:
            mismatches.append(index)
            continue
        generated.add(index)
        r = glyph.get('screenRect', {})
        x, y, w, h = [r.get(k, 0) for k in ('x', 'y', 'width', 'height')]
        if w > 0 and h > 0 and x >= 0 and y >= 0 and x+w <= width and y+h <= height:
            onscreen.add(index)
    return {'path': item.get('path'), 'full_text': full,
            'full_text_sha256': hashlib.sha256(full.encode('utf-8')).hexdigest(),
            'expected_nonwhitespace_character_count': len(expected),
            'matching_generated_character_count': len(generated),
            'fully_onscreen_quad_character_count': len(onscreen),
            'missing_generated_source_indices': sorted(set(expected)-generated),
            'quad_outside_screen_source_indices': sorted(generated-onscreen),
            'substituted_character_source_indices': mismatches,
            'tmp_overflow': item.get('isTextOverflowing'),
            'first_overflow_character_index': item.get('firstOverflowCharacterIndex'),
            'font_size': item.get('fontSize'),
            'effective_renderer_alpha': item.get('effectiveRendererAlpha'),
            'complete_generated_text': bool(expected) and generated == set(expected) and not item.get('isTextOverflowing'),
            'actual_visible_glyph_coverage': None,
            'visibility_status': 'requires_independent_image_validation_or_glyph_pixel_mask_with_occlusion'}


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--captures', type=Path, required=True, help='key_phases directory')
    p.add_argument('--spec', type=Path, default=Path('AutomationConfigs/RsiGame/evaluation/required_ui_information_cycle02_v1.json'))
    p.add_argument('--out', type=Path, required=True, help='new output JSON (existing output rejected)')
    args = p.parse_args()
    if args.out.exists():
        p.error('Output exists; use a new versioned path')
    spec = read(args.spec)
    results = []
    for context, role_ids in spec['contexts'].items():
        path = args.captures/context/'capture.json'
        if not path.exists():
            results.append({'context': context, 'status': 'missing_capture', 'required_role_denominator': len(role_ids)})
            continue
        doc = read(path)
        ev = doc.get('response', doc).get('evaluation', {})
        image = path.parent/'screen.png'
        dimensions = list(Image.open(image).size) if image.exists() else None
        aligned = dimensions == [ev.get('screenWidth'), ev.get('screenHeight')]
        roles = []
        for role_id in role_ids:
            role = spec['roles'][role_id]
            matches = [t for t in ev.get('texts', []) if re.search(role['path_regex'], t.get('path', ''))]
            if role.get('prefer_path_regex'):
                preferred = [t for t in matches if re.search(role['prefer_path_regex'], t.get('path', ''))]
                matches = preferred or matches
            texts = [measure_text(t, ev.get('screenWidth', 0), ev.get('screenHeight', 0)) for t in matches]
            roles.append({'role': role_id, 'meaning': role['meaning'], 'matched_component_count': len(texts),
                          'expected_minimum_component_count': spec.get('minimum_component_counts', {}).get(context, {}).get(role_id, 1),
                          'component_count_sufficient': len(texts) >= spec.get('minimum_component_counts', {}).get(context, {}).get(role_id, 1),
                          'required_tokens_present': all(any(token in t['full_text'] for t in texts) for token in role.get('required_tokens', [])) and (not role.get('any_required_tokens') or any(token in t['full_text'] for token in role['any_required_tokens'] for t in texts)),
                          'viewport_clip_visibility_unverified': role.get('clip_visibility_unverified', False),
                          'generated_text_complete': bool(texts) and all(t['complete_generated_text'] for t in texts),
                          'texts': texts})
        results.append({'context': context, 'status': 'measured', 'capture': str(path), 'capture_sha256': sha(path),
                        'image': str(image), 'image_sha256': sha(image) if image.exists() else None,
                        'image_dimensions': dimensions, 'geometry_matches_image_dimensions': aligned,
                        'required_role_denominator': len(role_ids), 'roles': roles})
    denominator = sum(c['required_role_denominator'] for c in results)
    checked = sum(bool(c.get('geometry_matches_image_dimensions')) and r['component_count_sufficient'] and r['required_tokens_present'] and r['generated_text_complete'] and all(t.get('effective_renderer_alpha', 0) > 0 for t in r['texts']) for c in results for r in c.get('roles', []))
    output = {'limited_required_role_checks': {'numerator': checked, 'denominator': denominator, 'ratio': checked/denominator if denominator else None, 'meaning': 'present component count + specified content tokens + complete matching generated text + positive renderer alpha + aligned image geometry; not visibility or semantic correctness'},
              'actual_visible_glyph_coverage': None, 'schema': 'required-ui-information-measurement-v1', 'spec': str(args.spec), 'spec_sha256': sha(args.spec),
              'purpose': 'Scoped factual information inventory and generated geometry; no rubric scores.',
              'limits': spec['limits'], 'contexts': results}
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(output, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    failures = [(c['context'], r['role']) for c in results for r in c.get('roles', []) if not r['generated_text_complete']]
    print(json.dumps({'output': str(args.out), 'contexts': len(results), 'incomplete_generated_text_roles': failures}, ensure_ascii=False))


if __name__ == '__main__':
    main()
