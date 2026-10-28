"""Canonical construction owns its graph; copy arrays once, not the graph twice."""
import hashlib
import os
import shutil
import numpy as np
from baseline_a_scenario_runtime import packed
from hierarchical_option_policy import ChoiceTree
from solo_a_compact_context_candidate import encode
from solo_a_decoded_cache_v3_candidate import canonical


class OwnedContextMixin:
    def add(self, support, document):
        # canonical recursively allocates every dict/list/tuple in this schema.
        # Numeric arrays are the only remaining caller-owned mutable leaves.
        support = canonical(support)
        document = canonical(document)
        for key in ('descriptors', 'history_features'):
            support[key] = np.array(support[key], copy=True)
            support[key].setflags(write=False)
        if set(document) != {'state','rules','question'} or not all(isinstance(v,str) for v in document.values()):
            raise ValueError('Public document schema')
        if not document['state'] or not document['question']:
            raise ValueError('Missing context')
        if hashlib.sha256(packed(list(support['commands']))).hexdigest()!=support['commands_sha256']:
            raise ValueError('Command binding mismatch')
        tree=ChoiceTree(support['operations'])
        if tree.leaves!=len(support['commands']):raise ValueError('Incomplete support')
        digest,payload=encode(support,document)
        if os.name!='nt' and shutil.disk_usage(self.path).free<3*2**30+2*len(payload):
            raise RuntimeError('3GiB protection')
        with self.db:
            context=self.db.execute('SELECT COALESCE(MAX(id),0)+1 FROM contexts').fetchone()[0]
            if context>=2**24:raise ValueError('Context float32 range')
            self.db.execute('INSERT INTO contexts VALUES (?,?,?)',(context,digest,payload))
        self._remember(context,dict(support=support,document=document))
        self.trees[context]=(tree,document)
        while len(self.trees)>1024:self.trees.popitem(last=False)
        return context
