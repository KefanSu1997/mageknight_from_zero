"""Evidence-driven review triggers, not policy-quality certification.

Input is one immutable completed-update record per update from a single run.
This module never restarts a process or changes a reward from a noisy metric.
"""
import math
import statistics


def finite(value):
    return type(value) in (int, float) and math.isfinite(value)


def inspect_updates(rows, *, ev_threshold=.5, ev_review_update=20, reward_review_update=50):
    if not rows:
        return dict(status='awaiting_first_complete_update', alerts=[], training_observed=False)
    alerts = []
    required = ('update', 'run_id', 'rollout', 'pre_update_ev', 'mse', 'bias', 'target_variance',
                'reward', 'accepted_optimizer_steps', 'valid_samples', 'timing_seconds')
    run = rows[0].get('run_id')
    for i, row in enumerate(rows):
        if any(key not in row for key in required):
            raise ValueError('Incomplete update evidence')
        if not run or row['run_id'] != run or type(row['update']) is not int or row['update'] != i+1:
            raise ValueError('Require contiguous completed updates from one fresh run')
        if not isinstance(row['rollout'], str) or not row['rollout']:
            raise ValueError('Missing archived rollout reference')
        for key in ('mse', 'bias', 'target_variance', 'reward'):
            if not finite(row[key]):
                alerts.append(dict(code='nonfinite_'+key, update=row['update'], rollout=row['rollout']))
        if row['mse'] < 0 or row['target_variance'] < 0:
            raise ValueError('MSE and target variance cannot be negative')
        ev = row['pre_update_ev']
        if ev is not None and not finite(ev):
            alerts.append(dict(code='nonfinite_ev', update=row['update'], rollout=row['rollout']))
        if row['target_variance'] == 0 and ev is not None:
            raise ValueError('Zero-variance target must report EV as null')
        for key in ('valid_samples', 'accepted_optimizer_steps'):
            if type(row[key]) is not int or row[key] < 0:
                raise ValueError('Invalid optimizer/sample count')
            if row[key] == 0:
                alerts.append(dict(code='zero_'+key, update=row['update'], rollout=row['rollout']))
        timing = row['timing_seconds']
        for key in ('collection', 'ppo', 'save', 'archive', 'full_update_wall'):
            if key not in timing or not finite(timing[key]) or timing[key] < 0:
                raise ValueError('Missing or invalid timing: '+key)
            # Stages may overlap; never infer wall time by summing them.
            if key!='archive' and timing[key] > timing['full_update_wall'] + .001:
                raise ValueError('Stage exceeds measured update wall interval')
    recent = rows[-5:]
    evs = [r['pre_update_ev'] for r in recent if finite(r['pre_update_ev'])]
    latest = rows[-1]['update']
    if latest >= ev_review_update and (len(evs) != len(recent) or statistics.median(evs) < ev_threshold):
        alerts.append(dict(code='critic_ev_review', updates=[r['update'] for r in recent],
            rollouts=[r['rollout'] for r in recent], median_ev=statistics.median(evs) if evs else None,
            threshold=ev_threshold, inspect=['return_reconciliation','recurrent_boundaries','critic_optimizer_coverage','prediction_target_scale']))
    trend = None
    if latest >= reward_review_update:
        window = rows[-reward_review_update:]
        rewards = [r['reward'] for r in window]
        if all(map(finite, rewards)):
            n = len(rewards); xmean = (n-1)/2; ymean = statistics.mean(rewards)
            slope = sum((i-xmean)*(y-ymean) for i,y in enumerate(rewards))/sum((i-xmean)**2 for i in range(n))
            delta = statistics.mean(rewards[-10:])-statistics.mean(rewards[:10])
            trend = dict(window_updates=[window[0]['update'],latest], slope=slope, last10_minus_first10=delta)
            if slope <= 0 or delta <= 0:
                alerts.append(dict(code='reward_growth_review', **trend,
                    rollouts=[r['rollout'] for r in window],
                    inspect=['task_composition','failed_scenarios','high_reward_cycles','wounds_and_clock','fixed_validation']))
    if len(rows) >= 10:
        initial = statistics.median(r['timing_seconds']['full_update_wall'] for r in rows[:5])
        current = statistics.median(r['timing_seconds']['full_update_wall'] for r in recent)
        if initial > 0 and current > 1.5*initial:
            alerts.append(dict(code='wall_time_regression', initial_median=initial, recent_median=current,
                inspect=['collection','ppo','save','archive','concurrent_evaluation']))
    return dict(status='review_required' if alerts else 'no_trigger_in_observed_updates',
                training_observed=True, run_id=run, completed_updates=latest,
                reward_trend=trend, alerts=alerts, proves_policy_quality=False)


def inspect_evaluation(record):
    """Keep denominator failures; queue wait and evaluation wall are separate."""
    if record['split'] not in ('validation', 'test') or record['worlds'] != 100 or record['episodes'] != 700:
        raise ValueError('Evaluation must retain 100 worlds × seven heroes')
    for key in ('evaluation_wall', 'evaluation_queue_wait'):
        if not finite(record[key]) or record[key] < 0:
            raise ValueError('Invalid evaluation timing')
    if any(type(record[k]) is not int or record[k] < 0 for k in ('completed', 'errors')):
        raise ValueError('Invalid evaluation counts')
    if record['completed'] + record['errors'] != 700:
        raise ValueError('Evaluation denominator is incomplete')
    return dict(status='review_required' if record['errors'] else 'completed',
                errors=record['errors'], denominator=700,
                evaluation_wall=record['evaluation_wall'],
                evaluation_queue_wait=record['evaluation_queue_wait'])
