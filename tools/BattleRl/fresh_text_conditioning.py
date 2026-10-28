"""Fit fixed normalization from training-only frozen vectors, never old weights."""
import hashlib,json,sqlite3
from pathlib import Path
import numpy as np
import torch
from rollout_archive import load

def fit_fresh_conditioning(model,out):
    assert not model.policy.optimizer.state and model.completed_updates==0
    source=Path('remote_conditioned300')
    archive=sorted(source.glob('rollout_*.archive'))[-1]
    buffer=load(archive)
    ids=list(dict.fromkeys(int(i) for i in buffer.observations['context_id'].reshape(-1) if i))[:512]
    assert len(ids)>=128
    del buffer
    db=sqlite3.connect('file:'+str(source/'contexts/packed_vectors/offsets.sqlite')+'?mode=ro',uri=True)
    pooled=[]
    with (source/'contexts/packed_vectors/vectors.bin').open('rb') as stream:
        for context in ids:
            result=db.execute('SELECT metadata FROM entries WHERE context=? ORDER BY node LIMIT 1',(context,)).fetchone()
            assert result is not None
            meta=json.loads(result[0]);stream.seek(meta['offset']);raw=stream.read(meta['bytes'])
            assert hashlib.sha256(raw).hexdigest()==meta['sha256']
            array=np.frombuffer(raw,dtype='<f4').reshape(meta['shape'])
            # Fresh state/value attention is exactly uniform, no trained pooling.
            pooled.append(array[1:].mean(axis=0))
    db.close()
    for layer in [model.policy.features_extractor.state_attention,model.policy.value_attention]:
        assert torch.count_nonzero(layer[-1].weight)==0
    x=torch.as_tensor(np.stack(pooled),device=model.device)
    with torch.no_grad():
        before_state=model.policy.features_extractor.state_adapter(x).clone()
        before_value=model.policy.token_value(x).clone()
    report=model.policy.fit_conditioning(x,x)
    with torch.no_grad():
        error=max(float((before_state-model.policy.features_extractor.state_adapter(x)).abs().max()),float((before_value-model.policy.token_value(x)).abs().max()))
    assert error<1e-5 and not model.policy.optimizer.state
    report.update(source_archive=str(archive),training_context_ids=ids,initial_output_error=error,
                  old_checkpoint_loaded=False,validation_used=False,trainable_weights_fresh=True,
                  source_archive_sha=hashlib.sha256(archive.read_bytes()).hexdigest() if archive.is_file() else None)
    (out/'fresh_conditioning.json').write_text(json.dumps(report,indent=2))
