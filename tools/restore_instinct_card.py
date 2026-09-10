"""Restore the existing original Instinct face (basic_card_027), retaining its GUID and art."""
from pathlib import Path
import json
import os
import re

ROOT = Path(__file__).resolve().parents[1]
PRINTED = dict(card_name='本能', base_effect='移动力2，影响力2，攻击2或格挡2。',
               enhanced_effect='移动力4，影响力4，攻击4或格挡4。', required_crystals='红')


def replace_file(path, text):
    temporary = path.with_suffix(path.suffix + '.tmp')
    temporary.write_text(text, encoding='utf-8')
    os.replace(temporary, path)


def main():
    path = ROOT / 'resources/text_json/basic_card.json'
    cards = json.loads(path.read_text(encoding='utf-8-sig'))
    card = next(c for c in cards if c['id'] == 'basic_card_027')
    card.update(PRINTED)
    replace_file(path, json.dumps(cards, ensure_ascii=False, indent=2) + '\n')
    asset = ROOT / 'Assets/GameData/CardsAssets/basic_card_027.asset'
    text = asset.read_text(encoding='utf-8-sig')
    for field, value in [('NameCn', PRINTED['card_name']), ('BaseEffect', PRINTED['base_effect']),
                         ('EnhancedEffect', PRINTED['enhanced_effect']), ('RequiredCrystals', '00000000')]:
        text, count = re.subn(r'(?m)^  ' + field + r':.*$', '  ' + field + ': ' + value, text)
        assert count == 1, field
    replace_file(asset, text)
    print('Restored original Instinct name, four choices and red enhancement cost; art/GUID unchanged.')


if __name__ == '__main__':
    main()
