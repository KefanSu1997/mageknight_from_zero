"""Independent visual transcription of archived printed scans, countryside 3..11.

Rows are C, NE, E, SE, SW, W, NW in fixed printed orientation.
These facts are not a declaration that the full runtime catalog is complete.
"""
import hashlib
import json
from pathlib import Path

POSITIONS=[(0,0),(1,-1),(1,0),(0,1),(-1,1),(-1,0),(0,-1)]
ROWS={
3:['Forest','Hills/Keep','Hills','Hills/CrystalMine/White','Plains/Village','Plains','Plains'],
4:['Desert/MageTower','Desert','Mountain','Plains/Village','Plains','Hills/RampagingOrcs','Desert'],
5:['Lake','Plains/Monastery','Plains/RampagingOrcs','Hills/CrystalMine/Blue','Forest','Forest/MagicalGlade','Forest'],
6:['Hills/CrystalMine/Red','Forest','Plains','Forest/RampagingOrcs','Hills','Hills/MonsterDen','Mountain'],
7:['Swamp','Forest/RampagingOrcs','Forest/MagicalGlade','Plains/Dungeon','Plains','Plains/Monastery','Lake'],
8:['Swamp/RampagingOrcs','Forest/AncientRuin','Plains','Swamp/Village','Swamp','Forest','Forest/MagicalGlade'],
9:['Mountain','Mountain','Wasteland/Keep','Plains','Wasteland/MageTower','Plains','Wasteland/Dungeon'],
10:['Mountain','Forest','Plains','Hills/AncientRuin','Hills/Keep','Hills','Hills/MonsterDen'],
11:['Plains/MageTower','Lake','Lake','Hills/RampagingOrcs','Lake','Plains/AncientRuin','Hills'],
}
RULES={'Keep':'place_011','MageTower':'place_010','CrystalMine':'place_006',
       'MagicalGlade':'place_006','Village':'place_003','Monastery':'place_002',
       'MonsterDen':'place_014','Dungeon':'place_008','AncientRuin':'place_005','RampagingOrcs':'place_000'}

if __name__=='__main__':
    root=Path('H:/MageKnight_RL_Runs/20261021_solo_conquest_prepare/tile_source_images')
    tiles=[]
    for number,rows in ROWS.items():
        image=root/f'MK_map_tiles_01-{number}.png'
        cells=[]
        for (q,r),value in zip(POSITIONS,rows):
            fields=value.split('/')
            cell={'q':q,'r':r,'terrain':fields[0]}
            if len(fields)>1:
                cell.update(site=fields[1],ruleId=RULES[fields[1]])
                if fields[1]=='RampagingOrcs':cell['enemyTokenColor']='Green'
            if len(fields)>2:cell['manaColor']=fields[2]
            cells.append(cell)
        tiles.append({'id':str(number),'kind':'Countryside','orientation':'printed_upright',
                      'source_image':str(image),'source_sha256':hashlib.sha256(image.read_bytes()).hexdigest(),
                      'cells':cells})
    out={'schemaVersion':1,'status':'visual_transcription_partial_catalog_not_runtime_integrated',
         'coordinates':{'type':'axial','rotationAllowed':False},
         'verification':'Visual cell transcription from archived scans; site symbols cross-checked against local original printed place cards.',
         'reference_discrepancies':[{'tile':'7','position':'SE','reference':'Tomb','printed':'Dungeon'},
                                    {'tile':'9','position':'NW','reference':'Tomb','printed':'Dungeon'}],
         'tiles':tiles}
    target=Path('resources/original_maps/countryside_3_11_transcription.json')
    target.write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'tiles':len(tiles),'cells':sum(len(t['cells']) for t in tiles),'output':str(target)}))
