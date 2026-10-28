"""Read real archived legal actions and verify candidate source-text bindings."""
import hashlib
import json
import sys
from pathlib import Path
sys.path[:0] = ['optimization_stage_20261014', 'tools/BattleRl']
from structured_option_text import StructuredOptionRenderer
from joint_option_text import JointRenderer

root = Path('MageKnight_RL_Runs/20261013_mmbert_pair/text_assets/texts.json')
renderer = StructuredOptionRenderer.__new__(StructuredOptionRenderer)
renderer.rules = json.loads(root.read_text(encoding='utf-8'))['entities']
contexts = Path('remote_public_state71/contexts/contexts.jsonl')
checked = annotated = extra_chars = documents = 0
for line in contexts.open(encoding='utf-8'):
    doc = json.loads(line)
    if not doc.get('operations'):
        continue
    for sequence in doc['operations']:
        for action in sequence:
            original = JointRenderer.primitive(renderer, action)
            candidate = renderer.primitive(action)
            assert candidate.startswith(original)
            refs = renderer.referenced_rules(action)
            for identity, source_text in refs:
                assert source_text == renderer.rules[identity]
                assert identity + '：' + source_text in candidate
            annotated += bool(refs)
            extra_chars += len(candidate) - len(original)
            checked += 1
    documents += 1
    if documents == 64:
        break
assert documents == 64 and annotated > 0
report = dict(passed=True, documents=documents, primitives=checked,
              annotated_primitives=annotated, added_characters=extra_chars,
              rules_sha256=hashlib.sha256(root.read_bytes()).hexdigest(),
              scope='64 archived candidate sets, exact printed-source bindings and original parameter text retained. No physical-slot resolution, tokenizer-length, pretrained encoding, CUDA update or policy benefit gate.')
Path('optimization_stage_20261014/structured_option_text_gate.json').write_text(json.dumps(report, indent=2))
print(json.dumps(report))
