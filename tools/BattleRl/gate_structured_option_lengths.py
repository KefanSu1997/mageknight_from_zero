"""Remote tokenizer gate for actual archived full-support option documents."""
import argparse
import json
import sys
from pathlib import Path
import numpy as np
sys.path[:0] = ['optimization_stage_20261014', 'tools/BattleRl']
from structured_option_text import StructuredOptionRenderer, ReferencedOptionRenderer
from joint_option_text import tokenizer, encode_document

parser = argparse.ArgumentParser()
parser.add_argument('--referenced', action='store_true')
args = parser.parse_args()
renderer_class = ReferencedOptionRenderer if args.referenced else StructuredOptionRenderer
renderer = renderer_class.__new__(renderer_class)
renderer.rules = json.loads(Path('MageKnight_RL_Runs/20261013_mmbert_pair/text_assets/texts.json').read_text(encoding='utf-8'))['entities']
tok = tokenizer()
cases = []
for line in Path('remote_public_state71/contexts/contexts.jsonl').open(encoding='utf-8'):
    doc = json.loads(line)
    if not doc.get('operations'):
        continue
    original = {key: doc[key] for key in ('state', 'rules', 'question', 'options')}
    candidate = dict(original)
    candidate['options'] = ['；随后'.join(renderer.primitive(a) for a in sequence) + '。完成以上全部步骤。'
                            for sequence in doc['operations']]
    for sequence in doc['operations']:
        for action in sequence:
            for identity, text in renderer.referenced_rules(action):
                assert identity + '：' + text in candidate['rules'], 'Reference missing from shared full rules'
    assert len(candidate['options']) == len(original['options'])
    counts = {}
    for name, document in [('original', original), ('candidate', candidate)]:
        raw_ids, positions = encode_document(tok, document, max_tokens=None)
        assert len(positions) == len(document['options'])
        try:
            ids, final_positions = encode_document(tok, document, max_tokens=8192)
            assert len(final_positions) == len(document['options'])
            counts[name] = dict(raw_tokens=len(raw_ids), tokens=len(ids), accepted=True)
        except ValueError as error:
            counts[name] = dict(raw_tokens=len(raw_ids), accepted=False, error=str(error))
    cases.append(dict(options=len(candidate['options']), **counts))
    if len(cases) == 64:
        break
assert len(cases) == 64
report = dict(cases=cases, documents=len(cases),
              original_rejected=sum(not c['original']['accepted'] for c in cases),
              candidate_rejected=sum(not c['candidate']['accepted'] for c in cases),
              original_raw_mean=float(np.mean([c['original']['raw_tokens'] for c in cases])),
              candidate_raw_mean=float(np.mean([c['candidate']['raw_tokens'] for c in cases])),
              scope='Actual frozen tokenizer on 64 archived complete candidate documents. No encoder forward, CUDA PPO or policy benefit.')
report['variant'] = 'referenced' if args.referenced else 'inline'
suffix = '_referenced' if args.referenced else ''
Path(f'optimization_stage_20261014/structured_option_lengths{suffix}_gate.json').write_text(json.dumps(report, indent=2))
print(json.dumps({k: v for k, v in report.items() if k != 'cases'}), flush=True)
