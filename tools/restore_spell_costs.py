"""Restore spell cost metadata from the existing printed mana symbols, preserving art/GUIDs."""
from pathlib import Path
import json
import re
import struct

ROOT = Path(__file__).resolve().parents[1]
# The 24 original faces show six green, six red, six blue and six white spells.
# Every bottom half shows the same basic color plus the black night symbol.
GROUPS = [('绿', 2), ('红', 0), ('蓝', 1), ('白', 3)]


def main():
    path = ROOT / 'resources/text_json/magic.json'
    cards = json.loads(path.read_text(encoding='utf-8-sig'))
    assert [c['id'] for c in cards] == [f'magic_{i:03}' for i in range(24)]
    changed = []
    for i, card in enumerate(cards):
        cn, color = GROUPS[i // 6]
        before = (card.get('mana_color'), card['spell_top'].get('mana_cost'), card['spell_bottom'].get('mana_cost'))
        expected = (cn + '色', cn, cn + ',黑')
        card['mana_color'], card['spell_top']['mana_cost'], card['spell_bottom']['mana_cost'] = expected
        asset = ROOT / f"Assets/GameData/CardsAssets/{card['id']}.asset"
        text = asset.read_text(encoding='utf-8-sig')
        text, count = re.subn(r'(?m)^  ManaColor: \d+$', f'  ManaColor: {color}', text)
        assert count == 1
        costs = iter([struct.pack('<i', color).hex(), struct.pack('<ii', color, 5).hex()])
        text, count = re.subn(r'(?m)^    ManaCost:.*$', lambda _: '    ManaCost: ' + next(costs), text)
        assert count == 2
        if text != asset.read_text(encoding='utf-8-sig'):
            asset.write_text(text, encoding='utf-8')
        if before != expected:
            changed.append({'id': card['id'], 'before': before, 'after': expected})
    path.write_text(json.dumps(cards, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(changed, ensure_ascii=False))


if __name__ == '__main__':
    main()
