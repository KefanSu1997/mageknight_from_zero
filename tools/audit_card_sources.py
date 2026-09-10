"""Inventory existing source records and artwork; does not modify game assets."""
from pathlib import Path
import hashlib
import json
import subprocess
from PIL import Image, ImageStat

ROOT = Path(__file__).resolve().parents[1]


def main():
    rows = []
    for batch in ['basic_card', 'advanced_card', 'magic', 'items']:
        cards = json.loads((ROOT / f'resources/text_json/{batch}.json').read_text(encoding='utf-8-sig'))
        for c in cards:
            image = ROOT / f"Assets/GameData/cards/{c['id']}.png"
            with Image.open(image) as im:
                stat = ImageStat.Stat(im.convert('RGB'))
            relative = image.relative_to(ROOT).as_posix()
            pointer = subprocess.run(['git', 'show', 'ffe6bcf:' + relative], cwd=ROOT,
                                     check=True, capture_output=True).stdout.decode('utf-8')
            original_hash = pointer.split('oid sha256:')[1].splitlines()[0]
            actual_hash = hashlib.sha256(image.read_bytes()).hexdigest()
            category = 'playable_face'
            # Inspected originals: three nearly black slots, then the deck back.
            if c['id'] in ['advanced_card_044', 'advanced_card_045', 'advanced_card_046']:
                category = 'empty_image_slot'
            elif c['id'] == 'advanced_card_047':
                category = 'card_back'
            rows.append({'id': c['id'], 'category': category, 'image': str(image), 'rgbMean': stat.mean,
                         'originalRevision': 'ffe6bcf', 'originalSha256': original_hash, 'actualSha256': actual_hash,
                         'matchesRepositoryOriginal': actual_hash == original_hash,
                         'name': c.get('card_name') or c.get('spell_top', {}).get('name', ''),
                         'baseText': c.get('base_effect') or c.get('assign_effect') or c.get('spell_top', {}).get('effect', ''),
                         'enhancedText': c.get('enhanced_effect') or c.get('once_per_round') or c.get('spell_bottom', {}).get('effect', '')})
    path = ROOT / 'AutomationOutputs/AllOriginalCards/20260910/source_inventory.json'
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding='utf-8')
    print('Records:', len(rows), 'Playable faces:', sum(r['category'] == 'playable_face' for r in rows),
          'Empty definition:', [r['id'] for r in rows if not r['name']])
    assert all(r['matchesRepositoryOriginal'] for r in rows), 'Artwork differs from the original repository imports; inspect before acceptance.'


if __name__ == '__main__':
    main()
