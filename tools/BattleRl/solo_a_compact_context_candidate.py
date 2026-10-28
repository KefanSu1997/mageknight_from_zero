"""Lossless row-deduplicated context codec candidate; not deployed."""
import base64,hashlib,json,zlib
import numpy as np
MAGIC=b'SAB2\0'
def encode(support,document):
    stored=dict(support);arrays={}
    for key in ('descriptors','history_features'):
        value=np.asarray(stored.pop(key))
        if value.dtype!=np.float32 or not np.isfinite(value).all():raise ValueError('Expected finite float32')
        value=value.astype('<f4',copy=False)
        info=dict(shape=list(value.shape),dtype='<f4')
        if key=='descriptors':
            unique=[];lookup={};indices=[]
            for row in value:
                raw=row.tobytes()
                index=lookup.get(raw)
                if index is None:index=len(unique);lookup[raw]=index;unique.append(raw)
                indices.append(index)
            info.update(data=base64.b64encode(b''.join(unique)).decode('ascii'),row_count=len(unique),indices=base64.b64encode(np.asarray(indices,dtype='<u4').tobytes()).decode('ascii'))
        else:info['data']=base64.b64encode(value.tobytes()).decode('ascii')
        arrays[key]=info
    raw=json.dumps(dict(version=2,support=stored,arrays=arrays,document=document),ensure_ascii=False,sort_keys=True,separators=(',',':'),allow_nan=False).encode()
    return hashlib.sha256(raw).hexdigest(),MAGIC+zlib.compress(raw,1)
def decode(digest,payload):
    if not payload.startswith(MAGIC):raise ValueError('Wrong codec')
    raw=zlib.decompress(payload[len(MAGIC):])
    if hashlib.sha256(raw).hexdigest()!=digest:raise ValueError('SHA mismatch')
    d=json.loads(raw)
    if d['version']!=2 or set(d['arrays'])!={'descriptors','history_features'}:raise ValueError('Schema mismatch')
    for key,info in d['arrays'].items():
        if info['dtype']!='<f4':raise ValueError('dtype')
        values=np.frombuffer(base64.b64decode(info['data'],validate=True),dtype='<f4')
        if key=='descriptors':
            if len(info['shape'])!=2:raise ValueError('descriptor rank')
            unique=values.reshape(info['row_count'],info['shape'][1]);indices=np.frombuffer(base64.b64decode(info['indices'],validate=True),dtype='<u4')
            if len(indices)!=info['shape'][0] or (len(indices) and int(indices.max())>=len(unique)):raise ValueError('indices')
            values=unique[indices]
        values=values.reshape(info['shape'])
        if not np.isfinite(values).all():raise ValueError('nonfinite')
        values.setflags(write=False);d['support'][key]=values
    return dict(support=d['support'],document=d['document'])
