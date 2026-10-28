"""Adopt a trusted local CPU worker packet after request identity validation.

Only the parent writes SQLite. Worker processes never sample, assign context IDs,
or access model RNG. The packet graph is transferred, not shared with its worker.
"""
import os
import shutil
import time


class PreparedJournalMixin:
    def add_prepared(self, packet):
        return self.add_prepared_many([packet])[0]

    def add_prepared_many(self, packets):
        """Commit a ready inference batch before any sampled action is returned."""
        packets = list(packets)
        if not packets:
            return []
        started = time.perf_counter()
        for packet in packets:
            self._validate_prepared(packet)
        payload_bytes = sum(len(p['payload']) for p in packets)
        if os.name != 'nt' and shutil.disk_usage(self.path).free < 3*2**30+2*payload_bytes:
            raise RuntimeError('3GiB protection')
        validated = time.perf_counter()
        with self.db:
            first = self.db.execute('SELECT COALESCE(MAX(id),0)+1 FROM contexts').fetchone()[0]
            if first + len(packets) > 2**24:
                raise ValueError('Context float32 range')
            ids = list(range(first, first + len(packets)))
            self.db.executemany('INSERT INTO contexts VALUES (?,?,?)',
                                [(i,p['digest'],p['payload']) for i,p in zip(ids,packets)])
        committed = time.perf_counter()
        # Only publish cached records after the whole transaction commits.
        for context, packet in zip(ids, packets):
            support, document = packet['support'], packet['document']
            for key in ('descriptors','history_features'):
                support[key].setflags(write=False)
            value = dict(support=support, document=document)
            if packet.get('compact_prepared_packet', False):
                self.remember_compact_prepared(context, value)
            else:
                self._remember(context, value)
            remember_tree = getattr(self, 'remember_prepared_tree', None)
            if remember_tree is not None:
                remember_tree(context, packet['tree'], document)
            else:
                self.trees[context] = (packet['tree'], document)
                while len(self.trees)>1024:self.trees.popitem(last=False)
        finished = time.perf_counter()
        if not hasattr(self, 'journal_timing'):
            self.journal_timing = dict(batches=0, contexts=0, payload_bytes=0,
                                      validation_seconds=0., commit_seconds=0., cache_seconds=0.)
        stats = self.journal_timing
        stats['batches'] += 1
        stats['contexts'] += len(packets)
        stats['payload_bytes'] += payload_bytes
        stats['validation_seconds'] += validated-started
        stats['commit_seconds'] += committed-validated
        stats['cache_seconds'] += finished-committed
        return ids

    def _validate_prepared(self, packet):
        if packet.get('compact_prepared_packet', False) and not hasattr(self, 'remember_compact_prepared'):
            raise ValueError('Runtime cannot adopt compact worker packets')
        support, document, tree = packet['support'], packet['document'], packet['tree']
        if tree.leaves != len(support['commands']):
            raise ValueError('Incomplete prepared tree')
        payload, digest = packet['payload'], packet['digest']
        if not isinstance(payload, bytes) or len(digest) != 64:
            raise ValueError('Invalid prepared persistence packet')
