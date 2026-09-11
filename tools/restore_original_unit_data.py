"""Restore fields directly transcribed from existing army_003/army_2_001 card images."""
import json
import os
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main():
    for name in ('army', 'army_2'):
        path = ROOT / f'resources/text_json/{name}.json'
        data = json.loads(path.read_text(encoding='utf-8-sig'))
        if name == 'army':
            row = next(x for x in data if x['id'] == 'army_003')
            row.update(card_name_cn='埃莫提普烈焰枪手', level='III', influence_cost='8', armor='6', resistances='',
                       recruit_location='城堡,城市', abilities=[dict(mana_cost='None', effect='攻击或格挡5。'),
                                                            dict(mana_cost='红', effect='远程火焰攻击6。')])
        else:
            next(x for x in data if x['id'] == 'army_2_001')['resistances'] = '物理'
            locations = {'army_2_001': '魔法塔,城堡', 'army_2_005': '修道院', 'army_2_007': '修道院',
                         'army_2_008': '修道院', 'army_2_012': '城堡,村庄'}
            for row in data:
                if row['id'] in locations:
                    row['recruit_location'] = locations[row['id']]
        temporary = path.with_suffix('.json.tmp')
        temporary.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
        os.replace(temporary, path)


if __name__ == '__main__':
    main()
