"""Read-only diagnostics of the preserved October 11 run; not held-out evaluation."""
import copy
import json
from pathlib import Path
import numpy as np
from trajectory_audit import audit


def main():
    root = Path('AutomationOutputs/BattleRl/20261011_single32_batch128_updates100')
    failure = json.loads((root/'worker_16_episode_3690_failure.json').read_text())
    trace = failure['trace']
    report = audit(trace)
    assert not report['errors'], report['errors']
    rule = 'original 021 Polarization stock pays exactly one selected opposite-color fee'
    negatives = {}
    for case in ('free', 'double', 'wrong_color', 'night_gold', 'black_cost_gold'):
        altered = copy.deepcopy(trace)
        step = altered[18]
        if case == 'free':
            step['after']['tokens']['Gold'] = 1
        elif case == 'double':
            step['after']['crystals']['Green'] -= 1
        elif case == 'wrong_color':
            step['after']['tokens']['Gold'] = 1
            step['after']['crystals']['Blue'] -= 1
        elif case == 'night_gold':
            step['before']['dayPart'] = step['after']['dayPart'] = 'Night'
        else:
            step['before']['paymentSelection']['source'] = 'Black'
        errors = audit(altered)['errors']
        negatives[case] = any(e['step'] == 18 and e['rule'] == rule for e in errors)
        assert negatives[case], case
    rows = [json.loads(line) for line in (root/'metrics.jsonl').read_text().splitlines()]
    windows = {}
    for label, window in [('first4', rows[:4]), ('last4', rows[-4:])]:
        windows[label] = dict(episodes=sum(r['batch_episodes'] for r in window),
            wins=sum(r['completed_declared_tasks'] for r in window),
            training_reward=float(np.mean([r['mean_training_return'] for r in window])),
            ev_after=float(np.mean([r['value_exp_var_after'] for r in window])))
    ev_checks = []
    for row in rows:
        with np.load(root/f"value_update_{row['update']:04d}.npz") as data:
            actual = 1 - np.var(data['targets'] - data['after']) / np.var(data['targets'])
            assert abs(actual-row['value_exp_var_after']) < 1e-12
            ev_checks.append(float(actual))
    result = dict(scope='saved trajectory audit and descriptive training metrics; no generalization claim',
        audit_checks=report['checks'], audit_errors=report['errors'], negative_cases=negatives,
        windows=windows, independently_recomputed_ev=ev_checks,
        optimizer_steps_added=rows[-1]['optimizer_steps']-747,
        kl_min=min(r['updated_full_sequence_kl'] for r in rows),
        kl_max=max(r['updated_full_sequence_kl'] for r in rows))
    out = Path('AutomationOutputs/BattleRl/20261011_rl_evidence_diagnostics.json')
    out.write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result))


if __name__ == '__main__':
    main()
