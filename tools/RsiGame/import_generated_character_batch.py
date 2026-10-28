"""Copy reviewed original PNGs; alpha bounds affect layout only, never image pixels."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import uuid
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--manifest', type=Path, required=True)
    parser.add_argument('--overrides', type=Path)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    rows = json.loads(args.manifest.read_text(encoding='utf8'))
    overrides = {x['id']: x for x in json.loads(args.overrides.read_text(encoding='utf8'))} if args.overrides else {}
    template = (ROOT / 'Assets/Resources/UI/EnemyCharacters/embedded_v1/monster_061.png.meta').read_text(encoding='utf8')
    prepared = []
    for original in rows:
        row = overrides.get(original['id'], original)
        source = Path(row['source'])
        category = 'EnemyCharacters' if row['id'].startswith('monster_') else 'UnitCharacters'
        target = ROOT / 'Assets/Resources/UI' / category / 'embedded_v1' / (row['id'] + '.png')
        frame = ROOT / 'Assets/Resources/UI/CharacterFrames' / (row['id'] + '.json')
        if target.exists() or frame.exists():
            raise ValueError('Refusing replacement: ' + row['id'])
        with Image.open(source) as image:
            if image.mode != 'RGBA':
                raise ValueError('RGBA required: ' + row['id'])
            alpha = image.getchannel('A')
            bounds = alpha.point(lambda value: 255 if value >= 64 else 0).getbbox()
            x, y, right, bottom = bounds
            margins = [x, y, image.width - right, image.height - bottom]
            alpha_min, alpha_max = alpha.getextrema()
            # Native generated cutouts can peak at 254/255 while retaining real
            # zero-alpha background. Do not rewrite pixels to satisfy an exact
            # integer endpoint. Near-opaque content and safe margins still apply.
            if alpha_min != 0 or alpha_max < 250 or min(margins) < 10:
                raise ValueError('Transparency/margin gate: ' + row['id'] + str(margins))
            sha = hashlib.sha256(source.read_bytes()).hexdigest()
            layout = dict(id=row['id'], x=x, y=y, width=right-x, height=bottom-y,
                textureWidth=image.width, textureHeight=image.height, source_sha256=sha,
                method='alpha>=64 layout bounds only; full UV and original unedited PNG retained')
        prepared.append((row, source, target, frame, layout, margins, [alpha_min, alpha_max]))
    records = []
    for row, source, target, frame, layout, margins, alpha_range in prepared:
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
        assert hashlib.sha256(target.read_bytes()).hexdigest() == layout['source_sha256']
        target.with_suffix('.png.meta').write_text(re.sub(r'guid: [a-f0-9]+', 'guid: '+uuid.uuid4().hex, template, count=1), encoding='utf8')
        frame.write_text(json.dumps(layout, ensure_ascii=False, indent=2), encoding='utf8')
        records.append(dict(id=row['id'], source=str(source), target=str(target), sha256=layout['source_sha256'],
            alpha_margin_pixels=margins, alpha_range=alpha_range, layout=layout, prompt=row['prompt'], reference=row['reference'],
            original_card_retained=True, image_pixels_edited=False))
    args.out.parent.mkdir(parents=True, exist_ok=True)
    with args.out.open('x', encoding='utf8') as stream:
        json.dump(dict(passed=True, records=records, scope='File copy/hash/alpha only; requires Unity binding and visual validation.'), stream, ensure_ascii=False, indent=2)
    print('Imported', len(records), 'unedited PNGs')


if __name__ == '__main__':
    main()
