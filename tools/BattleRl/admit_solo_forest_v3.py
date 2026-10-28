"""Create evidence-bound experimental admission for this tested immutable runtime."""
import hashlib,json,shutil,sys
from pathlib import Path
ROOT=Path('/root/autodl-tmp/solo_conquest_A_forest_v3')
BASE=Path('/root/autodl-tmp/solo_conquest_runtime_v2')
FOREST=Path('/root/autodl-tmp/solo_forest_trial')
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
for name in ('solo_structured_policy.py','solo_candidate_forest.py','solo_forest_policy.py'):
    assert sha(ROOT/'tools/BattleRl'/name)==sha(FOREST/name),name
assert sha(ROOT/'tools/BattleRl/solo_ppo_update.py')==sha(BASE/'tools/BattleRl/solo_ppo_update.py')
assert json.loads((FOREST/'ppo_gate/result.json').read_text())['passed']
e=ROOT/'admission_evidence';e.mkdir(exist_ok=False)
shutil.copytree(BASE/'cuda_runtime_gate/collection',e/'collection')
for source,name in [(BASE/'cuda_runtime_gate/result.json','cuda_gate.json'),
    (BASE/'cuda_runtime_gate/trajectory_review.json','trajectory_review.json'),
    (FOREST/'cuda_result.json','forest_tree_cuda.json'),(FOREST/'full_sequence_cuda.json','forest_sequence_cuda.json'),
    (FOREST/'ppo_gate/result.json','forest_ppo_cuda.json')]:shutil.copy2(source,e/name)
files={p.relative_to(ROOT).as_posix():sha(p) for p in e.rglob('*') if p.is_file()}
payload=json.loads((ROOT/'payload_manifest.json').read_text())
for name,digest in payload['files'].items():
    assert sha(ROOT/name)==digest;files[name]=digest
probe=json.loads((ROOT/'deployment_probe.json').read_text())
record=dict(version=1,scope='experimental_solo_conquest',native_binding=probe['native_binding'],files=files,
    full_rules_certified=False,limitations=['Provisional twelve-ruin token mappings; see bound provenance',
        'Solo all-wound combat admission uses documented conservative ordering convention',
        'Seven complete scenarios are integration evidence, not exhaustive card or hacking certification'],
    cuda_gate='admission_evidence/cuda_gate.json',collection='admission_evidence/collection/collection.json',
    trajectory_review='admission_evidence/trajectory_review.json',candidate_batching_passed=True,
    forest_tree_cuda='admission_evidence/forest_tree_cuda.json',forest_sequence_cuda='admission_evidence/forest_sequence_cuda.json',
    forest_ppo_cuda='admission_evidence/forest_ppo_cuda.json')
(ROOT/'training_admission.json').write_text(json.dumps(record,indent=2))
sys.path.insert(0,str(ROOT/'tools/BattleRl'))
from solo_native_env import SoloNativeEnv
with SoloNativeEnv(ROOT/'native/SoloConquestRl.dll',ROOT,ROOT/'formal_admission_probe') as env:
    print(json.dumps(dict(passed=True,admission_sha256=env.admission['sha256'],native_binding=env.capabilities['binding_sha256'])))
