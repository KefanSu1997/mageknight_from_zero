"""Freeze reviewed artwork identities before adding reversible render-only mirrors."""
import hashlib,json,shutil
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'
OUT=BASE/'attempt55'
def main():
    saved=OUT/'source_before';saved.mkdir(parents=True,exist_ok=True)
    for name in ['PveBattleController.EmbeddedBattle.cs','PveBattleController.BattlefieldReadability.cs']:
        target=saved/name
        if not target.exists():shutil.copy2(ROOT/'Assets/Scripts/Battle'/name,target)
    items=[]
    for group,category in [('enemies','EnemyCharacters'),('party','UnitCharacters')]:
        document=json.loads((BASE/f'attempt54/facing_inventory/{group}/review.json').read_text(encoding='utf8'))
        for row in document['items']:
            if row.get('recommendation')!='mirror_candidate':continue
            path=Path(row['path']);digest=hashlib.sha256(path.read_bytes()).hexdigest()
            assert digest==row['sha256'],row['id']
            items.append(dict(id=row['id'],category=category,resourceVersion=path.parent.name,
                sha256=digest,mirrorHorizontal=True,reason=row.get('evidence',row.get('reason','Reviewed original PNG direction'))))
    assert len(items)==13,len(items)
    target=ROOT/'Assets/Resources/UI/CharacterFacing/mirrors_v1.json'
    target.parent.mkdir(parents=True,exist_ok=True)
    target.write_text(json.dumps(dict(version=1,items=items),ensure_ascii=False,indent=2),encoding='utf8')
    (OUT/'mirror_registry.json').write_bytes(target.read_bytes())
    print('Frozen',len(items),'SHA/version bound mirror candidates')
if __name__=='__main__':main()
