"""Read-only full real-journal compression probe; no policy or source mutations."""
import hashlib,json,sqlite3,time,zlib
from pathlib import Path
p=Path('/root/solo_A_aligned_cuda_gate_20261022/contexts/scenario_contexts.sqlite')
db=sqlite3.connect('file:'+str(p)+'?mode=ro',uri=True)
total=compressed=count=0;compress_seconds=decode_seconds=0
for expected,payload in db.execute('SELECT sha,payload FROM contexts ORDER BY id'):
    assert hashlib.sha256(payload).hexdigest()==expected
    t=time.perf_counter();packed=zlib.compress(payload,1);compress_seconds+=time.perf_counter()-t
    t=time.perf_counter();restored=zlib.decompress(packed);decode_seconds+=time.perf_counter()-t
    assert restored==payload
    count+=1;total+=len(payload);compressed+=len(packed)+4
db.close()
result=dict(contexts=count,original_bytes=total,compressed_bytes=compressed,ratio=compressed/total,
    encode_seconds=compress_seconds,decode_seconds=decode_seconds,all_exact=True,source_mutated=False)
Path('/root/solo_context_compression_probe.json').write_text(json.dumps(result,indent=2));print(json.dumps(result))
