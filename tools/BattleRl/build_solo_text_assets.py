"""CPU-only, frozen pretrained rule encoding; never starts RL training."""
import argparse
import hashlib
import json
from pathlib import Path
import time
import numpy as np
from solo_printed_catalog import SoloPrintedCatalog
from frozen_ui_text import FrozenTextEncoder


def build(root, out):
    out = Path(out)
    if (out/'manifest.json').exists():
        raise ValueError('Completed assets already exist; use a new version directory')
    out.mkdir(parents=True, exist_ok=True)
    catalog = SoloPrintedCatalog(root)
    identities = sorted(catalog.entries)
    records = [catalog.get(identity) for identity in identities]
    encoder = FrozenTextEncoder(out/'cache', local_only=True)
    started = time.perf_counter()
    vectors = []
    for i, record in enumerate(records):
        vectors.append(encoder.encode([record['text']])[0])
        if (i+1) % 25 == 0:
            print(json.dumps(dict(encoded=i+1, total=len(records), seconds=time.perf_counter()-started)), flush=True)
    table = np.asarray(vectors, dtype=np.float32)
    assert table.shape == (len(identities), 384) and np.isfinite(table).all()
    assert np.all(np.linalg.norm(table, axis=1) > .99)
    after = encoder.fingerprint()
    assert after == encoder.fingerprint_before
    assert all(not p.requires_grad and p.grad is None for p in encoder.model.parameters())
    (out/'texts.json').write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding='utf-8')
    with (out/'entity_vectors.npy.pending').open('wb') as stream:
        np.save(stream, table, allow_pickle=False)
    (out/'entity_vectors.npy.pending').replace(out/'entity_vectors.npy')
    manifest = dict(schema='solo_printed_text_v1', entity_ids=identities,
        catalog_signature=catalog.signature, source_sha256=catalog.sources,
        unavailable=catalog.unavailable, encoder=encoder.signature,
        parameter_sha256=after, frozen=True, gradients_absent=True,
        elapsed_seconds=time.perf_counter()-started,
        assets_sha256={name:hashlib.sha256((out/name).read_bytes()).hexdigest()
                       for name in ('texts.json','entity_vectors.npy')},
        scope='static public text assets only; entries do not imply implemented legal actions',
        training_started=False)
    (out/'manifest.json.pending').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    (out/'manifest.json.pending').replace(out/'manifest.json')
    print(json.dumps(dict(passed=True, entities=len(identities), elapsed_seconds=manifest['elapsed_seconds'],
                          parameter_sha256=after, training_started=False)), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', default=str(Path(__file__).resolve().parents[2]))
    parser.add_argument('--out', required=True)
    args = parser.parse_args()
    build(args.root, args.out)
