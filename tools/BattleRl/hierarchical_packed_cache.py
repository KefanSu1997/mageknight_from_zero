"""Append-only verified vector cache; no per-node file creation."""
import hashlib,json,sqlite3
from pathlib import Path
import numpy as np
import hierarchical_option_policy as h
import hierarchical_batch_runtime as batch
from joint_option_policy import ContextStore

class PackedVectors:
    def __init__(self,path):
        path=Path(path);path.mkdir(parents=True,exist_ok=True)
        self.data=(path/'vectors.bin').open('a+b');self.index=(path/'vectors.index.jsonl').open('a+b')
        self.db=sqlite3.connect(path/'offsets.sqlite')
        self.db.execute('PRAGMA cache_size=-8192')
        self.db.execute('PRAGMA journal_mode=WAL')
        self.db.execute('PRAGMA synchronous=NORMAL')
        self.db.execute('CREATE TABLE IF NOT EXISTS entries (context INTEGER,node INTEGER,metadata TEXT,PRIMARY KEY(context,node))')
        self.data.seek(0,2);size=self.data.tell()
        self.index.seek(0)
        for line in self.index:
            if not line.endswith(b'\n'):raise ValueError('Incomplete vector cache index; preserve evidence')
            row=json.loads(line)
            if row['offset']+row['bytes']>size:raise ValueError('Truncated vector data')
            self.db.execute('INSERT OR IGNORE INTO entries VALUES (?,?,?)',(*row['key'],line.decode()))
        self.db.commit()
    def get(self,key):
        found=self.db.execute('SELECT metadata FROM entries WHERE context=? AND node=?',key).fetchone()
        if found is None:return None
        row=json.loads(found[0])
        self.data.seek(row['offset']);payload=self.data.read(row['bytes'])
        if hashlib.sha256(payload).hexdigest()!=row['sha256']:raise ValueError('Vector cache SHA mismatch')
        return np.frombuffer(payload,dtype='<f4').reshape(row['shape']).copy()
    def put_many(self,values):
        rows=[];self.data.seek(0,2)
        for key,value in values:
            if self.db.execute('SELECT 1 FROM entries WHERE context=? AND node=?',key).fetchone():continue
            a=np.asarray(value,dtype='<f4');payload=a.tobytes()
            row=dict(key=key,offset=self.data.tell(),bytes=len(payload),shape=a.shape,sha256=hashlib.sha256(payload).hexdigest())
            self.data.write(payload);rows.append(row)
        self.data.flush();self.index.seek(0,2)
        for row in rows:
            text=json.dumps(row);self.index.write((text+'\n').encode())
            self.db.execute('INSERT INTO entries VALUES (?,?,?)',(*row['key'],text))
        self.index.flush()
        self.db.commit()

stores={}
def packed(path):
    if path not in stores:stores[path]=PackedVectors(Path(path)/'packed_vectors')
    return stores[path]

def prefetch(path,requests):
    cache=packed(path);pending=[];docs=[]
    for context,node in dict.fromkeys(requests):
        if not context:continue
        key=(path,context,node)
        if key in h._vectors:continue
        value=cache.get((context,node))
        if value is not None:h._vectors[key]=value;continue
        t,base=h.tree(path,context);doc=t.document(base,node)
        if not doc['options']:doc['options']=['唯一完整操作，直接执行']
        pending.append((context,node));docs.append(doc)
    if docs:
        if batch.reader is None:batch.reader=batch.BatchedReader()
        values=batch.reader.encode_many(docs);cache.put_many(zip(pending,values))
        for (context,node),value in zip(pending,values):h._vectors[(path,context,node)]=value
    while len(h._vectors)>512:h._vectors.popitem(last=False)

def vectors(path,context,node):
    if not context:return np.zeros((2,384),np.float32)
    key=(path,context,node)
    if key not in h._vectors:prefetch(path,[(context,node)])
    h._vectors.move_to_end(key);return h._vectors[key]

def journal(self):
    if not hasattr(self,'_packed_journal'):self._packed_journal=self.journal.open('a+b')
    return self._packed_journal
def add(self,doc):
    with self.lock:
        if len(self.offsets)>=2**24-1:raise ValueError('Context ID float precision capacity')
        payload=(json.dumps(doc,ensure_ascii=False,separators=(',',':'))+'\n').encode('utf-8')
        stream=journal(self);stream.seek(0,2);self.offsets.append(stream.tell());stream.write(payload);stream.flush()
        return len(self.offsets)
def read(self,i):
    with self.lock:
        stream=journal(self);stream.seek(self.offsets[i-1]);return json.loads(stream.readline())

def install():
    batch.install();batch.prefetch=prefetch;h.node_vectors=vectors
    ContextStore.add=add;ContextStore.read=read
