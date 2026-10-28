"""Strict versioned lookup for the solo A-policy frozen text branch."""
import hashlib
import json
from pathlib import Path
import numpy as np
from solo_printed_catalog import SoloPrintedCatalog


class SoloTextAssets:
    def __init__(self, directory, repository):
        root = Path(directory)
        self.manifest = json.loads((root/'manifest.json').read_text(encoding='utf-8'))
        meta = self.manifest
        if meta['schema'] != 'solo_printed_text_v1' or not meta['frozen'] or not meta['gradients_absent']:
            raise ValueError('Unsupported or nonfrozen text assets')
        encoder = meta['encoder']
        if (encoder['model_id'] != 'jhu-clsp/mmBERT-small'
            or encoder['revision'] != 'abc32620dd4f6ab06f5fbe905dc25f310618e09f'
            or encoder['width'] != 384 or not encoder['frozen']):
            raise ValueError('Unexpected pretrained encoder binding')
        catalog = SoloPrintedCatalog(repository)
        self.catalog = catalog
        if meta['catalog_signature'] != catalog.signature or meta['source_sha256'] != catalog.sources:
            raise ValueError('Current printed sources differ from encoded sources')
        for name in ('texts.json', 'entity_vectors.npy'):
            if hashlib.sha256((root/name).read_bytes()).hexdigest() != meta['assets_sha256'][name]:
                raise ValueError('Changed text asset: '+name)
        records = json.loads((root/'texts.json').read_text(encoding='utf-8'))
        ids = meta['entity_ids']
        if ids != sorted(catalog.entries) or records != [catalog.get(i) for i in ids]:
            raise ValueError('Text identity/order binding differs')
        self.index = {identity:i for i, identity in enumerate(ids)}
        self.vectors = np.load(root/'entity_vectors.npy', allow_pickle=False)
        if self.vectors.shape != (len(ids),384) or not np.isfinite(self.vectors).all():
            raise ValueError('Invalid text vectors')
        if not np.allclose(np.linalg.norm(self.vectors, axis=1), 1., atol=1e-5):
            raise ValueError('Unexpected frozen text normalization')
        self.vectors.setflags(write=False)

    def get(self, identity):
        if identity not in self.index:
            raise ValueError('No frozen rule vector for '+identity)
        return self.vectors[self.index[identity]]

    def encode_bindings(self, bindings):
        """Keep each referenced rule separately, with its physical parameter role.

        A contribution without a printed identity has zero text entries, not a
        fabricated zero-valued rule. Its full numeric record stays with the caller.
        """
        result = []
        for described in self.catalog.describe_bindings(bindings):
            ids = tuple(entry['id'] for entry in described['printed'])
            vectors = np.stack([self.get(i) for i in ids]) if ids else np.empty((0,384), np.float32)
            vectors.setflags(write=False)
            result.append(dict(parameter=described['parameter'], zone=described['zone'],
                               slot=described['slot'], identities=ids, vectors=vectors))
        return result
