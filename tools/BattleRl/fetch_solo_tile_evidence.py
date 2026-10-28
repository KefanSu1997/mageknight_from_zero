"""Archive observed public tile scans for factual transcription, not runtime artwork distribution."""
import concurrent.futures
import hashlib
import json
from pathlib import Path
import urllib.request

ROOT = Path('H:/MageKnight_RL_Runs/20261021_solo_conquest_prepare/tile_source_images')
# CDN paths observed in the Map_tiles gallery DOM on 2026-10-22.
ROWS = [('01-A','9/94'),('01-B','e/e8'),('01-1','6/6f'),('01-2','d/d4'),
        ('01-3','a/aa'),('01-4','3/31'),('01-5','e/e7'),('01-6','e/ed'),
        ('01-7','8/81'),('01-8','4/42'),('01-9','e/e0'),('01-10','4/48'),
        ('01-11','c/c6'),('01-12','0/02'),('01-13','2/2b'),('01-14','a/ad'),
        ('02-1','4/4d'),('02-2','d/d3'),('02-3','7/71'),('02-4','1/10'),
        ('02-5','3/3a'),('02-6','5/5e'),('02-7','e/e4'),('02-8','9/9c'),
        ('02-9','7/75'),('02-10','9/90'),('02-V','7/7e')]

def fetch(row):
    key, shard = row
    name=f'MK_map_tiles_{key}.png'
    url=f'https://static.wikia.nocookie.net/unofficialmageknighttheboardgame/images/{shard}/{name}/revision/latest'
    path=ROOT/name
    if not path.exists():
        request=urllib.request.Request(url,headers={'User-Agent':'Mozilla/5.0'})
        with urllib.request.urlopen(request,timeout=45) as response:
            data=response.read()
        if not data.startswith(b'\x89PNG') and not data.startswith(b'RIFF'):
            raise ValueError(f'Not an image: {key}')
        path.write_bytes(data)
    data=path.read_bytes()
    return {'tile':key,'url':url,'path':str(path),'bytes':len(data),
            'sha256':hashlib.sha256(data).hexdigest(),'transcription_verified':False}

if __name__=='__main__':
    ROOT.mkdir(parents=True,exist_ok=True)
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        rows=list(pool.map(fetch,ROWS))
    (ROOT/'manifest.json').write_text(json.dumps({'source_page':
        'https://unofficialmageknighttheboardgame.fandom.com/wiki/Map_tiles',
        'scope':'Reference scans; no claim all cell facts verified or runtime integration complete',
        'images':rows},indent=2),encoding='utf-8')
    print(json.dumps({'images':len(rows),'bytes':sum(r['bytes'] for r in rows)}))
