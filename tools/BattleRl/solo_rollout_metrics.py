"""Tracking from immutable audited native trajectories, without another forward.

pre_update_ev measures the behavior critic against gamma=lambda=1 targets.
It must not be relabeled post-update EV or fixed-world validation performance.
"""
import numpy as np
from solo_episode_data import SoloEpisodeData


def summarize_complete_episodes(episodes):
    if not episodes or any(not isinstance(e,SoloEpisodeData) for e in episodes):
        raise ValueError('Audited complete native episodes required')
    predictions=np.concatenate([e.values for e in episodes])
    targets=np.concatenate([e.returns for e in episodes])
    if not np.isfinite(predictions).all() or not np.isfinite(targets).all():
        raise ValueError('Nonfinite critic/target')
    residual=predictions-targets
    # Constant arrays can have tiny nonzero np.var from mean rounding.
    variance=0. if np.ptp(targets)==0 else float(np.var(targets))
    ev=None if variance==0 else 1.-float(np.var(residual))/variance
    finals=[e.records[-1]['Transition'] for e in episodes]
    scores=[f['Score'] for f in finals]
    warnings=[dict(task_id=e.result['task_id'],warnings=e.result['audit'].get('warnings',[]))
              for e in episodes if e.result['audit'].get('warnings')]
    mean=lambda values:float(np.mean(values))
    return dict(pre_update_ev=ev,mse=mean(residual**2),bias=mean(residual),target_variance=variance,
        valid_samples=len(targets),episodes=len(episodes),
        reward=mean([float(e.rewards.sum()) for e in episodes]),
        mean_official_score=mean([s['Total'] for s in scores]),
        mean_fame=mean([s['Inventory']['Fame'] for s in scores]),
        mean_remaining_wounds=mean([s['Inventory']['DeckWounds'] for s in scores]),
        mean_conquered_cities=mean([s['Cities'] for s in scores]),
        mean_ending_round=mean([f['Round'] for f in finals]),
        mean_completed_turns=mean([f['CompletedTurns'] for f in finals]),
        trajectory_reviews=warnings,
        value_metric_scope='pre_update_behavior_predictions_vs_complete_scenario_returns',
        full_semantic_hacking_audit_complete=False)


def completed_update_record(episodes,optimization,*,run_id,update,rollout,timing_seconds):
    if not run_id or type(update)is not int or update<1 or not rollout:
        raise ValueError('Explicit run/update/rollout identity required')
    required={'collection','ppo','save','archive','full_update_wall'}
    if not required.issubset(timing_seconds):
        raise ValueError('Actual stage and complete wall timings required')
    for key in required:
        value=timing_seconds[key]
        if type(value)not in (int,float) or not np.isfinite(value) or value<0:
            raise ValueError('Invalid measured timing')
        # Archive is summed across concurrent workers, not a wall-clock stage.
        if key!='archive' and value>timing_seconds['full_update_wall']+.001:
            raise ValueError('Stage exceeds full measured wall interval')
    if abs(timing_seconds['ppo']-optimization['optimization_seconds'])>.001:
        raise ValueError('PPO time differs from actual optimizer report')
    return dict(run_id=run_id,update=update,rollout=str(rollout),timing_seconds=dict(timing_seconds),
        accepted_optimizer_steps=optimization['accepted_optimizer_steps'],
        upstream_metrics=optimization['upstream_metrics'],**summarize_complete_episodes(episodes))
