"""AutoDL-only frozen CUDA encoding gate; no PPO/environment/evaluation run."""
import hashlib
import json
import sys
import time
from pathlib import Path
import numpy as np
import torch
sys.path[:0] = ['optimization_stage_20261014', 'tools/BattleRl']
from structured_option_text import ReferencedOptionRenderer
from joint_option_text import tokenizer, encode_document, SNAPSHOT
from joint_option_policy import ContextReader

out = Path('optimization_stage_20261014/structured_option_encoding_gate')
out.mkdir(exist_ok=False)  # Preserve an existing attempt; never overwrite evidence.
torch.set_num_threads(1)
renderer = ReferencedOptionRenderer.__new__(ReferencedOptionRenderer)
renderer.rules = json.loads(Path('MageKnight_RL_Runs/20261013_mmbert_pair/text_assets/texts.json').read_text(encoding='utf-8'))['entities']
tok = tokenizer()
documents = []
for line in Path('remote_public_state71/contexts/contexts.jsonl').open(encoding='utf-8'):
    raw = json.loads(line)
    if not raw.get('operations'):
        continue
    doc = {key: raw[key] for key in ('state', 'rules', 'question', 'options')}
    doc['options'] = ['；随后'.join(renderer.primitive(a) for a in seq) + '。完成以上全部步骤。'
                      for seq in raw['operations']]
    ids, positions = encode_document(tok, doc, max_tokens=8192)
    assert len(positions) == len(raw['operations'])
    documents.append((len(ids), doc))
    if len(documents) == 64:
        break
ordered = sorted(range(len(documents)), key=lambda i: documents[i][0])
indices = list(dict.fromkeys([ordered[0], ordered[len(ordered)//2], ordered[-1]]))
reader = ContextReader()
assert not reader.model.training and not any(p.requires_grad for p in reader.model.parameters())
reader.max_tokens = 8192
cases = []
for index in indices:
    length, doc = documents[index]
    torch.cuda.synchronize()
    started = time.perf_counter()
    vectors = reader.encode(doc)
    torch.cuda.synchronize()
    elapsed = time.perf_counter() - started
    assert vectors.shape == (1 + len(doc['options']), 384) and np.isfinite(vectors).all()
    path = out/f'vectors_{index:04d}.npy'
    np.save(path, vectors, allow_pickle=False)
    restored = np.load(path, allow_pickle=False)
    np.testing.assert_array_equal(vectors, restored)
    source = json.dumps(doc, ensure_ascii=False, sort_keys=True).encode('utf-8')
    cases.append(dict(index=index, tokens=length, options=len(doc['options']),
                      seconds=elapsed, document_sha256=hashlib.sha256(source).hexdigest(),
                      vectors_sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
report = dict(passed=True, device=torch.cuda.get_device_name(), snapshot=str(SNAPSHOT),
              encoder_frozen=True, cases=cases,
              peak_cuda_allocated=torch.cuda.max_memory_allocated(),
              scope='Actual frozen CUDA mmBERT on min/median/max length among first64 archived full-support candidate documents; finite complete marker vectors and exact disk roundtrip. Not native state binding, PPO update, checkpoint continuation, policy benefit or whole-run throughput.')
(out/'gate.json').write_text(json.dumps(report, indent=2))
print(json.dumps(report), flush=True)
