"""Printed base-game core tiles; independent cell transcription, not runtime approval."""
import hashlib
import json
from pathlib import Path
from transcribe_solo_countryside_tiles import POSITIONS, RULES

ROWS = {
    1: ['Desert/Monastery', 'Desert/Tomb', 'Desert', 'Desert', 'Hills', 'Hills/SpawningGround', 'Mountain'],
    2: ['Lake', 'Swamp/AncientRuin', 'Hills/CrystalMine/Green', 'Swamp/RampagingDragon', 'Swamp/MageTower', 'Forest', 'Lake'],
    3: ['Wasteland', 'Wasteland/AncientRuin', 'Hills/MageTower', 'Wasteland', 'Hills/CrystalMine/White', 'Wasteland/Tomb', 'Mountain'],
    4: ['Mountain/RampagingDragon', 'Hills', 'Wasteland/Keep', 'Wasteland', 'Wasteland/AncientRuin', 'Wasteland', 'Hills/CrystalMine/Blue'],
    5: ['Swamp/City/Green', 'Swamp/Village', 'Swamp/RampagingOrcs', 'Swamp', 'Forest/RampagingOrcs', 'Lake', 'Forest/MagicalGlade'],
    6: ['Plains/City/Blue', 'Plains/Monastery', 'Lake', 'Lake', 'Hills', 'Mountain/RampagingDragon', 'Forest'],
    7: ['Plains/City/White', 'Plains', 'Forest', 'Lake/RampagingDragon', 'Lake', 'Wasteland/Keep', 'Wasteland/SpawningGround'],
    8: ['Wasteland/City/Red', 'Hills/CrystalMine/Red', 'Desert', 'Desert/RampagingDragon', 'Wasteland', 'Wasteland/RampagingDragon', 'Desert/AncientRuin'],
}

def transcribe(root):
    rules = dict(RULES, Tomb='place_009', SpawningGround='place_015', RampagingDragon='place_001')
    tiles = []
    for number, rows in ROWS.items():
        image = root / f'MK_map_tiles_02-{number}.png'
        cells = []
        for (q, r), value in zip(POSITIONS, rows):
            fields = value.split('/')
            cell = dict(q=q, r=r, terrain=fields[0])
            if len(fields) > 1:
                cell['site'] = fields[1]
                if fields[1] != 'City':
                    cell['ruleId'] = rules[fields[1]]
                if fields[1] in ('RampagingOrcs', 'RampagingDragon'):
                    cell['enemyTokenColor'] = 'Green' if fields[1] == 'RampagingOrcs' else 'Red'
            if len(fields) > 2:
                cell['cityColor' if fields[1] == 'City' else 'manaColor'] = fields[2]
            cells.append(cell)
        tiles.append(dict(id=f'core_{number}', printedNumber=number, kind='Core',
                          orientation='printed_upright', source_image=str(image),
                          source_sha256=hashlib.sha256(image.read_bytes()).hexdigest(), cells=cells))
    return dict(schemaVersion=1, status='visual_transcription_not_runtime_integrated',
                coordinates=dict(type='axial', rotationAllowed=False),
                verification='Independent visual transcription of archived original printed scans, core 1-8.',
                scope='Base-game core tiles only; expansion core tiles and walls remain separate pending transcription.',
                tiles=tiles)

if __name__ == '__main__':
    result = transcribe(Path('H:/MageKnight_RL_Runs/20261021_solo_conquest_prepare/tile_source_images'))
    target = Path('resources/original_maps/core_1_8_transcription.json')
    target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(dict(tiles=len(result['tiles']), cells=sum(len(t['cells']) for t in result['tiles']), output=str(target))))
