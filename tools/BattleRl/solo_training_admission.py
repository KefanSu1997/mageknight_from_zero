"""Evidence-bound experimental admission, distinct from full rules certification."""
import hashlib
import json
from pathlib import Path


def verify_admission(repository, capabilities):
    root=Path(repository).resolve();path=root/'training_admission.json'
    raw=path.read_bytes();record=json.loads(raw)
    if record.get('version')!=1 or record.get('scope')!='experimental_solo_conquest':
        raise ValueError('Unsupported training admission')
    if record.get('native_binding')!=capabilities['binding_sha256']:
        raise ValueError('Admission belongs to different native rules')
    if not record.get('limitations') or record.get('full_rules_certified') is not False:
        raise ValueError('Experimental admission must disclose remaining limitations')
    for relative,sha in record['files'].items():
        target=(root/relative).resolve()
        if not target.is_relative_to(root) or hashlib.sha256(target.read_bytes()).hexdigest()!=sha:
            raise ValueError('Admission evidence changed: '+relative)
    gate_name=record['cuda_gate']
    if gate_name not in record['files']:raise ValueError('Unbound CUDA result')
    gate=json.loads((root/gate_name).read_text())
    if (gate.get('passed') is not True or gate.get('complete_scenarios')!=7
        or gate.get('accepted_optimizer_steps',0)<1 or gate.get('reload_parameter_exact') is not True):
        raise ValueError('CUDA full-scenario integration incomplete')
    for name in ('collection','trajectory_review'):
        relative=record[name]
        if relative not in record['files']:raise ValueError('Unbound '+name)
    collection=json.loads((root/record['collection']).read_text())
    if collection.get('complete')!=7 or len(collection.get('episodes',[]))!=7:
        raise ValueError('Incomplete seven-hero evidence')
    if any(r.get('status')!='complete' or not r.get('eligible_complete_episode')
           for r in collection['episodes']):
        raise ValueError('Unfinished scenario in admission')
    review=json.loads((root/record['trajectory_review']).read_text())
    if (review.get('status')!='no_positive_public_cycle_detected' or review.get('episodes')!=7
        or review.get('positive_public_cycle_candidates')):
        raise ValueError('Unresolved trajectory review')
    if record.get('candidate_batching_passed'):
        for kind in ('forest_tree_cuda','forest_sequence_cuda','forest_ppo_cuda'):
            relative=record[kind]
            if relative not in record['files']:raise ValueError('Unbound '+kind)
            evidence=json.loads((root/relative).read_text())
            if evidence.get('passed') is not True:raise ValueError('Failed '+kind)
            if kind=='forest_ppo_cuda' and (evidence.get('complete_scenarios')!=7
                or evidence.get('old_new_final_parameter_relative_l2',1)>=1e-4
                or evidence.get('reload_parameter_exact') is not True):
                raise ValueError('Incomplete same-batch PPO equivalence')
    return dict(sha256=hashlib.sha256(raw).hexdigest(),record=record)
