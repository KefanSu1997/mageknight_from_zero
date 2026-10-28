"""Offline measurements of opt-in Unity evaluation captures; never assigns scores."""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image


def distribution(values):
    a = np.asarray(values, dtype=float)
    a = a[np.isfinite(a)]
    return {'n': int(a.size), 'quantiles': {str(q): float(np.percentile(a, q))
            for q in (0, 5, 25, 50, 75, 95, 100)}} if a.size else {'n': 0, 'quantiles': {}}


def rect(r):
    return [float(r.get(k, 0)) for k in ('x', 'y', 'width', 'height')]


def outside(r, width, height):
    x, y, w, h = rect(r)
    return x < 0 or y < 0 or x+w > width or y+h > height


def pixel_bounds(r, width, height, pad=0):
    x, y, w, h = rect(r)
    return (max(0, int(np.floor(x-pad))), max(0, int(np.floor(y-pad))),
            min(width, int(np.ceil(x+w+pad))), min(height, int(np.ceil(y+h+pad))))


def pixel_proxy(luminance, glyphs):
    height, width = luminance.shape
    ratios, glyph_samples, background_samples, rows = [], [], [], []
    valid = [g for g in glyphs if not g.get('character', '').isspace()]
    for glyph in valid:
        r = glyph['screenRect']
        x0, y0, x1, y1 = pixel_bounds(r, width, height)
        if x1 <= x0 or y1 <= y0:
            continue
        gx0, gy0, gx1, gy1 = pixel_bounds(r, width, height, 2)
        ring = luminance[gy0:gy1, gx0:gx1]
        mask = np.ones(ring.shape, dtype=bool)
        # Exclude all non-whitespace glyph quads of this text from its local ring.
        for other in valid:
            ax0, ay0, ax1, ay1 = pixel_bounds(other['screenRect'], width, height)
            ix0, iy0, ix1, iy1 = max(gx0, ax0), max(gy0, ay0), min(gx1, ax1), min(gy1, ay1)
            if ix1 > ix0 and iy1 > iy0:
                mask[iy0-gy0:iy1-gy0, ix0-gx0:ix1-gx0] = False
        ink_quad = luminance[y0:y1, x0:x1].ravel()
        background = ring[mask]
        if not background.size:
            continue
        lo, hi = np.percentile(ink_quad, [5, 95])
        bg = float(np.median(background))
        # The farther observed percentile is a polarity-agnostic foreground proxy.
        fg = float(lo if abs(lo-bg) >= abs(hi-bg) else hi)
        ratio = (max(fg, bg)+.05)/(min(fg, bg)+.05)
        ratios.append(ratio)
        glyph_samples.extend(ink_quad.tolist())
        background_samples.extend(background.tolist())
        rows.append({'sourceIndex': glyph.get('sourceIndex'), 'screenRect': r,
                     'clipped_pixel_bounds': [x0, y0, x1, y1],
                     'quad_pixel_count': int(ink_quad.size), 'ring_pixel_count': int(background.size),
                     'quad_luminance_p05': float(lo), 'quad_luminance_p95': float(hi),
                     'ring_luminance_median': bg, 'selected_foreground_proxy': fg, 'ratio': ratio})
    return {'method': 'observed-pixel-contrast-proxy', 'glyph_quad_denominator': len(valid),
            'sampled_glyphs': len(rows), 'quad_pixel_sample_count': len(glyph_samples),
            'ring_pixel_sample_count': len(background_samples),
            'ratio_distribution': distribution(ratios),
            'quad_luminance_distribution': distribution(glyph_samples),
            'ring_luminance_distribution': distribution(background_samples), 'samples': rows}


def analyze(capture):
    doc = json.loads(capture.read_text(encoding='utf-8-sig'))
    response = doc.get('response', doc)
    evaluation = response.get('evaluation')
    if not evaluation:
        return {'capture': str(capture), 'status': 'skipped', 'reason': 'No response.evaluation metadata'}
    image = capture.parent/'screen.png'
    if not image.exists():
        given = response.get('screenshotPath') or doc.get('screenshotPath') or doc.get('screenshot')
        if not given:
            return {'capture': str(capture), 'status': 'skipped', 'reason': 'No screenshot path or sibling screen.png'}
        image = Path(given)
        if not image.is_absolute():
            image = capture.parent/image
    if not image.is_file():
        return {'capture': str(capture), 'status': 'skipped', 'reason': 'Screenshot file unavailable', 'image': str(image)}
    rgb = np.asarray(Image.open(image).convert('RGB'), dtype=float)/255
    linear = np.where(rgb <= .04045, rgb/12.92, ((rgb+.055)/1.055)**2.4)
    luminance = linear @ np.array([.2126, .7152, .0722])
    height, width = luminance.shape
    expected = [evaluation.get('screenWidth'), evaluation.get('screenHeight')]
    aligned = expected == [width, height]
    texts = []
    key_heights = {}
    for item in evaluation.get('texts', []):
        glyphs = item.get('glyphs', [])
        measured = {'path': item.get('path'), 'name': item.get('name'), 'fullText': item.get('fullText'),
                    'fontSize': item.get('fontSize'), 'screenRect': item.get('screenRect'),
                    'isTextOverflowing': item.get('isTextOverflowing'),
                    'firstOverflowCharacterIndex': item.get('firstOverflowCharacterIndex'),
                    'overflowMode': item.get('overflowMode'),
                    'glyph_quad_height_pixels': distribution([g['screenRect']['height'] for g in glyphs])}
        if any(key.lower() in (item.get('name', '')+' '+item.get('path', '')).lower()
               for key in ('Numbers', 'Details', 'Heading', 'Phase')):
            key_heights[item.get('path')] = measured['glyph_quad_height_pixels']
            measured['pixel_contrast'] = pixel_proxy(luminance, glyphs) if aligned else {
                'status': 'skipped', 'reason': 'PNG/evaluation dimensions differ'}
        texts.append(measured)
    illustrations = []
    for image_row in evaluation.get('rawImages', []):
        if 'enemyillustration' not in (image_row.get('path', '')+' '+image_row.get('name', '')).lower():
            continue
        sw, sh = image_row.get('sourceWidth', 0), image_row.get('sourceHeight', 0)
        _, _, dw, dh = rect(image_row['screenRect'])
        uv = image_row.get('uvRect', {})
        u, v, uw, uh = rect(uv)
        sr = sw/sh if sh else None
        dr = dw/dh if dh else None
        sampled = abs(sw*uw/(sh*uh)) if sh and uh else None
        illustrations.append({**image_row, 'source_ratio': sr, 'display_ratio': dr,
            'relative_display_vs_source_ratio_difference': dr/sr-1 if sr and dr else None,
            'uv_sampled_source_ratio': sampled,
            'relative_display_vs_uv_sampled_ratio_difference': dr/sampled-1 if sampled and dr else None,
            'uv_full': all(abs(a-b)<1e-6 for a,b in zip((u,v,uw,uh), (0,0,1,1)))})
    controls = evaluation.get('controls')
    raw_controls = response.get('observation', {}).get('controls', [])
    control_rows = []
    for c in controls or []:
        r = c['screenRect']
        control_rows.append({'path': c.get('path'), 'screenRect': r, 'out_of_bounds': outside(r,width,height)})
    return {'capture': str(capture), 'status': 'measured', 'image': str(image),
        'image_sha256': hashlib.sha256(image.read_bytes()).hexdigest(),
        'png_dimensions': [width,height], 'is_1920x1080': [width,height]==[1920,1080],
        'evaluation_dimensions': expected, 'geometry_matches_png_dimensions': aligned,
        'coordinate_system': evaluation.get('coordinateSystem'),
        'text_count': len(texts), 'overflow_count': sum(bool(t['isTextOverflowing']) for t in texts),
        'texts': texts, 'key_label_glyph_height_pixels': key_heights,
        'enemy_illustration_denominator': len(illustrations), 'enemy_illustrations': illustrations,
        'control_rect_status': 'measured' if controls is not None and aligned else 'unmeasured',
        'control_rect_denominator': len(controls) if controls is not None else None,
        'control_rect_out_of_bounds_count': sum(c['out_of_bounds'] for c in control_rows) if controls is not None and aligned else None,
        'raw_observation_control_count': len(raw_controls),
        'control_rect_limitation': 'Raw observation points can be raycast points and widths are local rect units; not substituted for screen rectangles.',
        'control_rects': control_rows, 'canvases': evaluation.get('canvases', [])}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', required=True, type=Path)
    parser.add_argument('--out', type=Path)
    args = parser.parse_args()
    root = args.root.resolve()
    out = (args.out or root/'evaluation_analysis').resolve()
    captures = sorted(root.rglob('capture.json'))
    reports = []
    for capture in captures:
        try:
            report = analyze(capture)
        except Exception as error:
            report = {'capture': str(capture), 'status': 'error', 'reason': repr(error)}
        target = out/capture.relative_to(root).parent/'report.json'
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
        reports.append({'capture': str(capture), 'report': str(target), 'status': report['status']})
    summary = {'root': str(root), 'capture_denominator': len(captures),
        'status_counts': {s: sum(r['status']==s for r in reports) for s in ('measured','skipped','error')},
        'reports': reports,
        'sampling_definition': 'Linear sRGB luminance. Non-whitespace visible TMP generated glyph quad pixel rectangles; floor/ceil bounds clipped to PNG. Local background is 2px expanded rectangle excluding all glyph quads in the same text. Compare farther quad p05/p95 to ring median using (lighter+.05)/(darker+.05). Samples can overlap between glyphs and are not independent.',
        'limitations': ['observed-pixel-contrast-proxy; not WCAG certification or foreground segmentation',
            'Glyph quads include internal whitespace/antialiasing; generated geometry may be partially masked or occluded.',
            'Textured backgrounds, nearby graphics, shaders and mixed colors can dominate percentiles; ring is not guaranteed true background.',
            'Pixel contrast sampled for Numbers/Details/Heading/Phase labels only; all active captured text retains geometry and full text.',
            'Source aspect-ratio difference is not subject cropping or deformation proof; UV and transforms require interpretation.',
            'Control rectangle measurement requires evaluation.controls screenRect metadata. Raw observation raycast points/local widths cannot establish screen bounds.',
            'No numeric-to-score anchors or 1-5 scores are produced.']}
    out.mkdir(parents=True, exist_ok=True)
    (out/'summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'summary': str(out/'summary.json'), **summary['status_counts']}))


if __name__ == '__main__':
    main()
