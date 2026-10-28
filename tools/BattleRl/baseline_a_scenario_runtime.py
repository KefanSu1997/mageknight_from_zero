"""Immutable, restartable solo contexts for A's unchanged joint option reader."""
import hashlib,json,sqlite3,zlib,shutil,os
from collections import OrderedDict
from pathlib import Path
import numpy as np
from candidate_option_runtime import Runtime as OptionRuntime
from hierarchical_option_policy import ChoiceTree


def packed(value):
    return json.dumps(value,ensure_ascii=False,sort_keys=True,separators=(',',':'),allow_nan=False).encode()


class ScenarioRuntime(OptionRuntime):
    def __init__(self,path,binding):
        path=Path(path);path.mkdir(parents=True,exist_ok=True)
        if not binding:raise ValueError('Source/model/input binding required')
        self.db=sqlite3.connect(path/'scenario_contexts.sqlite')
        self.db.execute('PRAGMA journal_mode=WAL')
        self.db.execute('PRAGMA synchronous=FULL')
        self.db.execute('CREATE TABLE IF NOT EXISTS binding (id INTEGER PRIMARY KEY, payload BLOB NOT NULL)')
        self.db.execute('CREATE TABLE IF NOT EXISTS contexts (id INTEGER PRIMARY KEY, sha TEXT NOT NULL, payload BLOB NOT NULL)')
        if any(row[2] for row in self.db.execute('PRAGMA index_list(contexts)')):
            self.db.close()
            raise ValueError('Legacy deduplicated context schema requires explicit migration')
        expected=packed(binding);found=self.db.execute('SELECT payload FROM binding WHERE id=1').fetchone()
        if found is not None and bytes(found[0])!=expected:
            self.db.close()
            raise ValueError('Runtime source/model/input binding mismatch')
        self.db.execute('INSERT OR IGNORE INTO binding VALUES (1,?)',(expected,));self.db.commit()
        self.supports=OrderedDict()
        super().__init__(path)

    def add(self,support,document):
        if set(document)!={'state','rules','question'} or not all(isinstance(v,str) for v in document.values()):
            raise ValueError('Explicit public semantic document required')
        if not document['state'] or not document['question']:raise ValueError('Missing decision context')
        serialized={k:v.tolist() if isinstance(v,np.ndarray) else v for k,v in support.items()}
        commands=list(support['commands'])
        if hashlib.sha256(packed(commands)).hexdigest()!=support['commands_sha256']:
            raise ValueError('Support command binding mismatch')
        tree=ChoiceTree(support['operations'])
        if tree.leaves!=len(commands):raise ValueError('Complete leaf/support mismatch')
        payload=packed(dict(support=serialized,document=document));sha=hashlib.sha256(payload).hexdigest()
        if os.name!='nt' and shutil.disk_usage(self.path).free<3*2**30+2*len(payload):
            raise RuntimeError('3GiB context-write protection; preserve partial trajectory')
        with self.db:
            # Even identical public documents may have different recurrent
            # histories. A context is a decision occurrence, not a text hash.
            next_id=self.db.execute('SELECT COALESCE(MAX(id),0)+1 FROM contexts').fetchone()[0]
            if next_id>=2**24:raise ValueError('Context exceeds exact float32 integer range')
            # Lossless storage only: SHA remains over the exact canonical bytes.
            # Legacy JSON rows remain readable; no candidate or attribute is dropped.
            stored=b'ZL1\0'+zlib.compress(payload,level=1)
            self.db.execute('INSERT INTO contexts VALUES (?,?,?)',(next_id,sha,stored))
        return next_id

    def _read(self,context):
        row=self.db.execute('SELECT sha,payload FROM contexts WHERE id=?',(int(context),)).fetchone()
        if row is None:raise ValueError('Missing immutable context '+str(context))
        payload=bytes(row[1])
        if payload.startswith(b'ZL1\0'):
            try:payload=zlib.decompress(payload[4:])
            except zlib.error as exc:raise ValueError('Corrupted compressed context') from exc
        if hashlib.sha256(payload).hexdigest()!=row[0]:raise ValueError('Context SHA mismatch')
        return json.loads(payload)

    def support(self,context):
        if not context:
            return dict(commands=('padding',),sequences=[[0]],weights=[[1.]],descriptors=np.zeros((1,480),np.float32))
        if context not in self.supports:
            s=self._read(context)['support'];s['commands']=tuple(s['commands'])
            for key in ('descriptors','history_features'):
                s[key]=np.asarray(s[key],np.float32);s[key].setflags(write=False)
            self.supports[context]=s
        self.supports.move_to_end(context)
        while len(self.supports)>256:self.supports.popitem(last=False)
        return self.supports[context]

    def tree(self,context):
        if context not in self.trees:
            doc=self._read(context)
            self.trees[context]=(ChoiceTree(doc['support']['operations']),doc['document'])
        self.trees.move_to_end(context)
        while len(self.trees)>256:self.trees.popitem(last=False)
        return self.trees[context]

    def vectors(self,requests):
        requests=[(int(c),int(n)) for c,n in requests]
        result=super().vectors(requests)
        for (context,node),value in zip(requests,result):
            count=len(self.tree(context)[0].nodes[node]['children']) if context else 1
            # Reader includes one root row, then one row per option. A leaf
            # document has a single placeholder, but it is never a decision.
            if value.shape!=(max(count,1)+1,384) or not np.isfinite(value).all():
                raise ValueError('Cached joint marker vectors violate complete option binding')
        return result

    def close(self):
        self.db.execute('PRAGMA wal_checkpoint(TRUNCATE)');self.db.close()
        self.cache.db.execute('PRAGMA wal_checkpoint(TRUNCATE)');self.cache.db.close()
        self.cache.data.close();self.cache.index.close()
