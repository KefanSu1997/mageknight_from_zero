"""Independent short-text encoder cache. No bidirectional prefix reuse."""
import hashlib
import json
import time
from collections import OrderedDict
from pathlib import Path
import numpy as np
from solo_a_compact_runtime_candidate import CompactRuntime
from solo_a_document_cache_candidate import node_document
from solo_a_reader_once_candidate import SingleTokenizationReader


class SplitTextRuntime(CompactRuntime):
    def __init__(self, path, binding, assets, text_cache_bytes=128*2**20):
        super().__init__(path, dict(binding, split_text_version=1,
                                   printed_catalog=assets.catalog.signature,
                                   printed_vectors=assets.manifest['assets_sha256']['entity_vectors.npy']))
        self.assets = assets
        self.content_vectors = OrderedDict()
        if type(text_cache_bytes) is not int or text_cache_bytes < 1:
            raise ValueError('Positive text cache byte budget required')
        self.text_cache_limit = text_cache_bytes
        self.text_cache_bytes = 0
        self.text_cache_hits = self.text_cache_misses = 0

    def rule_vectors(self, context):
        if not context:
            return np.empty((0,384), np.float32)
        # Each referenced identity survives separately, never a mean rule vector.
        ids = sorted({i for candidate in self.support(context)['rule_bindings']
                      for binding in candidate for i in binding['identities']})
        return np.stack([self.assets.get(i) for i in ids]) if ids else np.empty((0,384), np.float32)

    def dynamic_document(self, context, node):
        tree, base = self.tree(context)
        # Full structured state still feeds actor/critic. Only current locked
        # prefix and all named option descriptions need online text encoding.
        short_base = dict(base, state='当前公开数值状态由独立结构化输入提供。')
        document = node_document(tree, short_base, node)
        document['rules'] = ''
        return document

    def vectors(self, requests):
        keys = []; missing = OrderedDict()
        for context, node in requests:
            if not context:
                keys.append(None); continue
            doc = self.dynamic_document(int(context), int(node))
            raw = json.dumps(doc, ensure_ascii=False, sort_keys=True, separators=(',', ':')).encode()
            key = hashlib.sha256(raw).hexdigest(); keys.append(key)
            if key not in self.content_vectors:
                missing[key] = doc
                self.text_cache_misses += 1
            else:
                self.text_cache_hits += 1
        if missing:
            if self.reader is None:
                self.reader = SingleTokenizationReader()
            start = time.perf_counter()
            values = self.reader.encode_many(list(missing.values()))
            if len(values) != len(missing):
                raise ValueError('Missing dynamic encoding')
            for (key, doc), value in zip(missing.items(), values):
                if value.shape != (len(doc['options'])+1,384) or not np.isfinite(value).all():
                    raise ValueError('Incomplete/nonfinite split text vectors')
                self.content_vectors[key] = value
                self.text_cache_bytes += value.nbytes
            with (Path(self.path)/'split_encoder_timing.jsonl').open('a') as f:
                f.write(json.dumps(dict(documents=len(missing), tokens=sum(self.reader.last_lengths),
                                        seconds=time.perf_counter()-start))+'\n')
        result = [self.content_vectors[k] if k is not None else np.zeros((2,384),np.float32) for k in keys]
        for k in keys:
            if k is not None: self.content_vectors.move_to_end(k)
        # Return values remain alive for the current inference batch, but no
        # completed batch can leave the retained cache above its byte budget.
        while self.text_cache_bytes > self.text_cache_limit or len(self.content_vectors)>4096:
            _, old = self.content_vectors.popitem(last=False)
            self.text_cache_bytes -= old.nbytes
        return result
