import sys,json,base64,time,hashlib
from pathlib import Path
import numpy as np
sys.path.insert(0,str(Path(__file__).resolve().parents[2]/'tools/BattleRl'))
import solo_a_binary_context_candidate as old
import solo_a_compact_context_candidate as new
path=Path(sys.argv[1]);contexts=[]
for row in json.loads(path.read_text()):
 d=old.decode(row['sha'],base64.b64decode(row['payload']))
 for k in ('descriptors','history_features'):d['support'][k]=np.asarray(d['support'][k],np.float32)
 contexts.append(d)
for d in contexts:
 sha,payload=new.encode(d['support'],d['document']);back=new.decode(sha,payload)
 assert back['document']==d['document'] and set(back['support'])==set(d['support'])
 for k,v in d['support'].items():
  if isinstance(v,np.ndarray):assert v.shape==back['support'][k].shape and v.tobytes()==back['support'][k].tobytes()
  else:assert v==back['support'][k],k
 try:new.decode('bad',payload)
 except ValueError:pass
 else:raise AssertionError('Missing SHA rejection')
samples=[]
for label,module in [('old',old),('new',new),('new',new),('old',old)]:
 t=time.perf_counter();encoded=[module.encode(d['support'],d['document']) for d in contexts];encoding=time.perf_counter()-t
 t=time.perf_counter();decoded=[module.decode(*row) for row in encoded];decoding=time.perf_counter()-t
 samples.append(dict(codec=label,encode_seconds=encoding,decode_seconds=decoding,bytes=sum(len(row[1]) for row in encoded)))
result=dict(passed=True,contexts=len(contexts),samples=samples,corpus_sha=hashlib.sha256(path.read_bytes()).hexdigest(),scope='CPU lossless codec gate only; runtime/PPO not deployed or verified')
Path(sys.argv[2]).write_text(json.dumps(result,indent=2));print(json.dumps(result))
