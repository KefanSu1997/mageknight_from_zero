"""Verify explicit decorative-shadow additions separately from unchanged actor images."""
import json, hashlib, argparse, re
from pathlib import Path

def read(p): return json.loads(p.read_text(encoding='utf8'))

def main():
    p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);a=p.parse_args()
    root=a.root.resolve(); rows=[]
    for group in ['validation','four_unit_validation','card_validation']:
        for current in sorted((root/'attempt44'/group/'key_phases').glob('*/capture.json')):
            old=root/'attempt43'/(group+'_final')/'key_phases'/current.parent.name/'capture.json'
            if not old.exists(): continue
            b,c=read(old),read(current)
            state=lambda r:{x['key']:x['value'] for x in r['observation']['state']}
            assert state(b)['audit.costNativeState']==state(c)['audit.costNativeState'],current
            controls=lambda r:{x['path']:{k:x[k] for k in ['name','text','interactable','clickable','x','y','width','height']} for x in r['observation']['controls']}
            assert controls(b)==controls(c),current
            prior={i['path']:i for i in b['evaluation']['rawImages']}; shadows=[]
            now={i['path']:i for i in c['evaluation']['rawImages']}
            for path,i in now.items():
                if i['name']=='CharacterCastShadow':
                    actors=[v for k,v in now.items() if k.rsplit('/',1)[0]==path.rsplit('/',1)[0] and v['name'] in ['PartyFigure','EnemyIllustration']]
                    assert len(actors)==1,(current,path)
                    actor=actors[0]
                    for k in ['texture','sourceWidth','sourceHeight','uvRect','effectiveRendererAlpha']: assert i[k]==actor[k],(path,k)
                    assert i['shader']=='MageKnight/UI/BattleCharacterProjection' and i['shaderSupported'],path
                    shadows.append(path);continue
                assert path in prior,(current,path)
                for k in ['texture','sourceWidth','sourceHeight','screenRect','uvRect','componentColor','effectiveRendererAlpha']:
                    assert i[k]==prior[path][k],(current,path,k)
            assert set(prior).issubset(now),current
            old_text={t['path']:t for t in b['evaluation']['texts']}
            for t in c['evaluation']['texts']:
                if not re.search(r'/Enemy_\d+/(Name|Numbers|State|EnemyIntent)$',t['path']):continue
                assert t['fullText']==old_text[t['path']]['fullText'] and t['fontSize']>=old_text[t['path']]['fontSize'],(current,t['path'])
                assert not t['isTextOverflowing'],(current,t['path'])
                assert ''.join(g['character'] for g in t['glyphs'])==''.join(ch for ch in t['fullText'] if not ch.isspace()),(current,t['path'])
            rows.append(dict(capture=str(current),shadows=shadows))
    assert len(rows)>=37,len(rows)
    inventory=read(root/'attempt42/catalog_alpha_inventory.json')
    project=Path(__file__).resolve().parents[2]
    for item in inventory['records']:assert hashlib.sha256((project/item['path']).read_bytes()).hexdigest()==item['sha256'],item['id']
    catalog=read(root/'attempt44/asset_cases/manifest.json');assert catalog['passed']
    result=dict(passed=True,matched_captures=len(rows),source_png_unchanged=len(inventory['records']),rows=rows,
        scope='Same native state, control click geometry, actor PNG/UV/geometry/alpha, required enemy text and explicit supported decorative shadows. PNG equality does not prove printed identity or visual integration. Native rendering color/priority may differ. No performance or human acceptance claim.')
    destination=root/'attempt44/projection_contract_audit.json'
    with destination.open('x',encoding='utf8') as f:json.dump(result,f,ensure_ascii=False,indent=2)
    print('PASS',len(rows),'matched captures;',len(inventory['records']),'unchanged PNGs')

if __name__=='__main__':main()
