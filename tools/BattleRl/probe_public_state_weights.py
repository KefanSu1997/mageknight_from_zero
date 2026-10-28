"""Read-only CPU inspection of saved public-state residual weights."""
import io,json,zipfile,hashlib
from pathlib import Path
import torch
torch.set_num_threads(1)
root=Path('remote_public_state71')
def read(name):
    raw=(root/name).read_bytes()
    with zipfile.ZipFile(io.BytesIO(raw)) as z:
        state=torch.load(io.BytesIO(z.read('policy.pth')),map_location='cpu',weights_only=True)
    return state,hashlib.sha256(raw).hexdigest()
initial,ish=read('migration_initial.zip')
current,csh=read('checkpoint.zip')
rows={}
for k,v in current.items():
    if 'public_state_residual' in k:
        rows[k]={'finite':bool(torch.isfinite(v).all()),'max_delta':float((v-initial[k]).abs().max()),'norm':float(v.norm())}
assert rows and all(v['finite'] for v in rows.values())
assert any(v['max_delta']>0 for v in rows.values())
report={'initial_sha':ish,'checkpoint_sha':csh,'parameters':rows,'scope':'Saved weight changes only; does not establish policy reward improvement.'}
target=root/('public_state_weights_'+csh[:12]+'.json')
target.write_text(json.dumps(report,indent=2))
print(json.dumps(report))
