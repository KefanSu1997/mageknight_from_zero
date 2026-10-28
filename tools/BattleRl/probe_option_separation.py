"""CPU-only inspection of cached actual sibling options and saved option head."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
import io,json,sqlite3,zipfile,hashlib
from pathlib import Path
import numpy as np
import torch
torch.set_num_threads(1)
root=Path('remote_public_state71');raw=(root/'checkpoint.zip').read_bytes()
with zipfile.ZipFile(io.BytesIO(raw)) as z:state=torch.load(io.BytesIO(z.read('policy.pth')),map_location='cpu',weights_only=True)
head=torch.nn.Sequential(torch.nn.LayerNorm(384),torch.nn.Linear(384,128),torch.nn.GELU())
head.load_state_dict({k[len('option_head.'):]:v for k,v in state.items() if k.startswith('option_head.')})
db=sqlite3.connect('file:'+str(root/'contexts/packed_vectors/offsets.sqlite')+'?mode=ro',uri=True)
rows=db.execute('SELECT context,node,metadata FROM entries ORDER BY context,node LIMIT 512').fetchall()
results=[]
with (root/'contexts/packed_vectors/vectors.bin').open('rb') as f,torch.no_grad():
    for ctx,node,metadata in rows:
        m=json.loads(metadata);f.seek(m['offset']);payload=f.read(m['bytes']);assert hashlib.sha256(payload).hexdigest()==m['sha256']
        x=torch.from_numpy(np.frombuffer(payload,dtype='<f4').reshape(m['shape']).copy())[1:]
        if len(x)<2:continue
        item=dict(context=ctx,node=node,options=len(x),vector_sha=m['sha256'])
        for label,v in [('encoder',x),('trained_head',head(x))]:
            pairs=torch.triu_indices(len(v),len(v),offset=1)
            n=torch.nn.functional.normalize(v,dim=-1);cos=(n@n.T)[pairs[0],pairs[1]]
            item[label]=dict(cosine_median=float(cos.median()),cosine_max=float(cos.max()),exact_duplicate_pairs=int(((v[pairs[0]]-v[pairs[1]]).abs().amax(-1)==0).sum()),relative_sibling_rms=float((v-v.mean(0)).square().mean().sqrt()/v.square().mean().sqrt().clamp_min(1e-12)))
        results.append(item)
assert results
summary={label:{k:float(np.median([r[label][k] for r in results])) for k in ('cosine_median','relative_sibling_rms')} for label in ('encoder','trained_head')}
report=dict(checkpoint_sha=hashlib.sha256(raw).hexdigest(),branches=len(results),summary=summary,duplicate_pairs=sum(r['encoder']['exact_duplicate_pairs'] for r in results),rows=results,scope='First 512 saved cache entries, includes selected-path sampling bias. Similarity alone is not policy failure or proof of language understanding. No model updates/encoder calls/environment changes.')
target=root/('option_separation_'+report['checkpoint_sha'][:12]+'.json');target.write_text(json.dumps(report,indent=2))
print(json.dumps({k:v for k,v in report.items() if k!='rows'}))
