"""Explicit provenance migration only when every encoded text/id is unchanged."""
import argparse, hashlib, json, shutil
from pathlib import Path
from solo_printed_catalog import SoloPrintedCatalog
from solo_text_assets import SoloTextAssets

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('source');p.add_argument('out');p.add_argument('repo');a=p.parse_args()
    source=Path(a.source);out=Path(a.out);catalog=SoloPrintedCatalog(a.repo)
    meta=json.loads((source/'manifest.json').read_text(encoding='utf-8'))
    old=json.loads((source/'texts.json').read_text(encoding='utf-8'))
    ids=meta['entity_ids'];new=[catalog.get(i) for i in ids]
    assert ids==sorted(catalog.entries)
    assert [(r['id'],r['text'],r['text_sha256']) for r in old]==[(r['id'],r['text'],r['text_sha256']) for r in new]
    for name,sha in meta['assets_sha256'].items():
        assert hashlib.sha256((source/name).read_bytes()).hexdigest()==sha
    out.mkdir(parents=True,exist_ok=False)
    shutil.copy2(source/'entity_vectors.npy',out/'entity_vectors.npy')
    (out/'texts.json').write_text(json.dumps(new,ensure_ascii=False,indent=2),encoding='utf-8')
    old_signature=meta['catalog_signature'];old_sources=meta['source_sha256']
    meta.update(catalog_signature=catalog.signature,source_sha256=catalog.sources)
    meta['assets_sha256']['texts.json']=hashlib.sha256((out/'texts.json').read_bytes()).hexdigest()
    (out/'manifest.json').write_text(json.dumps(meta,ensure_ascii=False,indent=2),encoding='utf-8')
    SoloTextAssets(out,a.repo)
    (out/'provenance_migration.json').write_text(json.dumps(dict(source=str(source.resolve()),
        old_catalog=old_signature,new_catalog=catalog.signature,old_sources=old_sources,
        new_sources=catalog.sources,all_encoded_texts_identical=True,vectors_byte_identical=True),indent=2),encoding='utf-8')
    print(json.dumps(dict(passed=True,out=str(out),texts=len(new))))
