"""Extract the original wound component from the locally stored rulebook, without redraw.

Requires existing pypdf and Pillow. Page 4 contains one original full-page JPEG;
crop its original pixels directly, with no PDF rasterization or upsampling.
"""
import hashlib
import json
from pathlib import Path

from pypdf import PdfReader


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'resources/rulebook/rulebook_chinese_quick_start.PDF'
OUTPUT = ROOT / 'Assets/Resources/Adventure/Art/original_wound.png'
PAGE = 4
# Original embedded image pixels, top-left origin, exclusive right/bottom.
# Visually verified against the complete wound card below the page's wound heading.
CROP = (1672, 1335, 1926, 1696)


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def main():
    source_bytes = SOURCE.read_bytes()
    page = PdfReader(SOURCE).pages[PAGE - 1]
    images = list(page.images)
    assert len(images) == 1, 'The source page structure changed; re-audit its component image.'
    source_image = images[0]
    assert source_image.name == 'Im0.jpg'
    assert source_image.image.size == (2508, 3353), 'Re-audit crop if original dimensions change.'
    # Extraction only: preserve the original RGB pixels and printed rounded corners.
    wound = source_image.image.crop(CROP).convert('RGB')
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    wound.save(OUTPUT, format='PNG')
    metadata = {
        'source': SOURCE.relative_to(ROOT).as_posix(),
        'source_sha256': sha256(source_bytes),
        'page_one_based': PAGE,
        'embedded_image': source_image.name,
        'embedded_image_sha256': sha256(source_image.data),
        'embedded_image_dimensions': list(source_image.image.size),
        'crop_xyxy_top_left_exclusive': list(CROP),
        'operation': 'Crop original embedded JPEG pixels; RGB PNG; no redraw, resampling or generated content',
        'output': OUTPUT.relative_to(ROOT).as_posix(),
        'output_dimensions': list(wound.size),
        'output_sha256': sha256(OUTPUT.read_bytes()),
        'reproduce': 'python tools/extract_original_wound_art.py',
        'visual_source': 'Page 4, right column, complete card beneath 创伤卡牌 heading',
    }
    manifest = OUTPUT.with_suffix('.source.json')
    manifest.write_text(json.dumps(metadata, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'output': str(OUTPUT), 'dimensions': wound.size, 'manifest': str(manifest)}, ensure_ascii=False))


if __name__ == '__main__':
    main()
