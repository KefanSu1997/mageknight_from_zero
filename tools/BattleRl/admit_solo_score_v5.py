"""Bind current reward-corrected integration and unchanged forest equivalence."""
import hashlib
import json
import shutil
import sys
from pathlib import Path

root = Path('/root/autodl-tmp/solo_conquest_score_v5')
forest = Path('/root/autodl-tmp/solo_forest_trial')
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
gate = root / 'cuda_runtime_gate'
result = json.loads((gate / 'result.json').read_text())
assert result['passed'] and result['policy_class'] == 'SoloForestPolicy'
assert result['complete_scenarios'] == 7 and result['reload_parameter_exact']
for name in ('solo_structured_policy.py', 'solo_candidate_forest.py', 'solo_forest_policy.py'):
    assert sha(root / 'tools/BattleRl' / name) == sha(forest / name), name
telemetry = root / 'optimizer_telemetry_invariance.json'
assert telemetry.exists(), 'Independent updater invariance evidence required'
invariance = json.loads(telemetry.read_text())
assert invariance['passed'] and invariance['forward_outputs_exact']
assert invariance['gradient_tensors_exact'] == 75
e = root / 'admission_evidence'
e.mkdir(exist_ok=False)
shutil.copytree(gate / 'collection', e / 'collection')
for source, name in [(gate / 'result.json', 'cuda_gate.json'),
                     (gate / 'trajectory_review.json', 'trajectory_review.json'),
                     (forest / 'cuda_result.json', 'forest_tree_cuda.json'),
                     (forest / 'full_sequence_cuda.json', 'forest_sequence_cuda.json'),
                     (forest / 'ppo_gate/result.json', 'forest_ppo_cuda.json'),
                     (telemetry, 'optimizer_telemetry_invariance.json')]:
    shutil.copy2(source, e / name)
files = {p.relative_to(root).as_posix(): sha(p) for p in e.rglob('*') if p.is_file()}
for name, digest in json.loads((root / 'payload_manifest.json').read_text())['files'].items():
    assert sha(root / name) == digest, name
    files[name] = digest
probe = json.loads((root / 'deployment_probe.json').read_text())
record = dict(version=1, scope='experimental_solo_conquest', native_binding=probe['native_binding'],
              files=files, full_rules_certified=False,
              limitations=['Provisional twelve-ruin mappings remain explicitly documented.',
                           'All-wound combat admission uses a conservative ordering convention.',
                           'Seven complete scenarios establish integration, not learning or exhaustive rules coverage.',
                           'Historical forest equivalence tests cover unchanged model algorithms; current reward integration uses the v5 gate.'],
              cuda_gate='admission_evidence/cuda_gate.json',
              collection='admission_evidence/collection/collection.json',
              trajectory_review='admission_evidence/trajectory_review.json', candidate_batching_passed=True,
              forest_tree_cuda='admission_evidence/forest_tree_cuda.json',
              forest_sequence_cuda='admission_evidence/forest_sequence_cuda.json',
              forest_ppo_cuda='admission_evidence/forest_ppo_cuda.json')
(root / 'training_admission.json').write_text(json.dumps(record, indent=2))
sys.path.insert(0, str(root / 'tools/BattleRl'))
from solo_native_env import SoloNativeEnv
with SoloNativeEnv(root / 'native/SoloConquestRl.dll', root, root / 'formal_admission_probe') as env:
    print(json.dumps(dict(passed=True, admission_sha256=env.admission['sha256'],
                          native_binding=env.capabilities['binding_sha256'])))
