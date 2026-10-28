"""Bind actual seven-game A CUDA evidence to the experimental solo adapter."""
import argparse,hashlib,json,os,shutil,sys
from pathlib import Path
if __name__=='__main__' and '--repo' in sys.argv:os.chdir(sys.argv[sys.argv.index('--repo')+1])
os.environ['SOLO_BASELINE_A_OBSERVATION']='1'
from solo_native_env import SoloNativeEnv
from solo_episode_data import SoloEpisodeData
from solo_rollout_review import review_episodes
from solo_training_admission import verify_admission

def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def run(a):
    root=Path(a.repo).resolve();gate=Path(a.gate).resolve()
    result=json.loads((gate/'recovered_result.json').read_text())
    assert result['passed'] and result['reload_exact'] and result['update']['episodes']==7
    assert result['update']['accepted_optimizer_steps']>0
    contract=json.loads((gate/'contract.json').read_text())
    changed=[n for n,h in contract['sources'].items() if sha(root/'tools/BattleRl'/n)!=h]
    # These two engineering-only changes require explicit independent evidence.
    assert set(changed).issubset({'baseline_a_scenario_runtime.py','baseline_a_scenario_ppo.py'}),changed
    evidence=root/'admission_evidence_A';evidence.mkdir(exist_ok=False)
    shutil.copy2(gate/'recovered_result.json',evidence/'actual_cuda_result.json')
    shutil.copy2(gate/'contract.json',evidence/'actual_cuda_contract.json')
    shutil.copy2(gate/'collection/collection.json',evidence/'collection.json')
    episodes=[SoloEpisodeData(gate/f'collection/episode_{i:06d}/result.json') for i in range(7)]
    review=review_episodes(episodes);assert review['status']=='no_positive_public_cycle_detected'
    (evidence/'trajectory_review.json').write_text(json.dumps(review))
    if changed:
        proofs=json.loads(Path(a.engineering_proofs).read_text())
        assert proofs['compression']['all_exact'] and proofs['compression']['contexts']==result['update']['decisions']
        assert proofs['runtime_gate']['passed'] and proofs['runtime_gate']['persisted_supports_exact']
        assert proofs['zero_variance_metric_gate']['passed']
        assert all(sha(root/'tools/BattleRl'/n)==proofs['source_sha256'][n] for n in changed)
        shutil.copy2(a.engineering_proofs,evidence/'engineering_equivalence.json')
    normalized=dict(passed=True,complete_scenarios=7,accepted_optimizer_steps=result['update']['accepted_optimizer_steps'],
        reload_parameter_exact=True,source_result_sha256=sha(gate/'recovered_result.json'),
        scope='Actual seven complete games and one CUDA update, not learning success or full rules coverage')
    (evidence/'cuda_gate.json').write_text(json.dumps(normalized))
    with SoloNativeEnv(root/'native/SoloConquestRl.dll',root,evidence/'native_probe',preparation=True) as native:
        capabilities=native.capabilities
    files={}
    for folder in (root/'tools/BattleRl',root/'native',root/'Assets/Logic',evidence):
        for p in folder.rglob('*'):
            if p.is_file() and '__pycache__' not in p.parts and (folder.name!='BattleRl' or p.suffix=='.py'):
                files[p.relative_to(root).as_posix()]=sha(p)
    record=dict(version=1,scope='experimental_solo_conquest',native_binding=capabilities['binding_sha256'],
        full_rules_certified=False,candidate_batching_passed=False,files=files,
        cuda_gate='admission_evidence_A/cuda_gate.json',collection='admission_evidence_A/collection.json',
        trajectory_review='admission_evidence_A/trajectory_review.json',
        limitations=['Seven initial training tasks prove integration only; all seven scored zero.',
            'Rare units, offers and pending rules have typed fixtures, not complete real-play coverage.',
            'No claim of EV learning, no reward-growth or fixed-validation success yet.',
            'Full 128-game throughput and independent 700-game evaluation still require measurement.'])
    (root/'training_admission.json').write_text(json.dumps(record,indent=2))
    checked=verify_admission(root,capabilities);print(json.dumps(dict(passed=True,admission_sha256=checked['sha256'],changed=changed)))
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--repo',required=True);p.add_argument('--gate',required=True)
    p.add_argument('--engineering-proofs',required=True);run(p.parse_args())
