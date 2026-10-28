"""Full-game phase credit with unchanged upstream A recurrent PPO per expert."""
import time
from baseline_a_scenario_ppo import update_complete
from solo_phase_credit import split_complete_episode


def update_phase_complete(models, episodes, runtimes):
    if set(models) != {'combat', 'world'} or set(runtimes) != set(models):
        raise ValueError('Two independent expert models and runtimes are required')
    for model in models.values():
        if model.gamma != 1. or model.gae_lambda != 1.:
            raise ValueError('Phase reward collapse requires gamma=lambda=1')
        if model.batch_size != 128 or model.n_epochs != 4:
            raise ValueError('Retain A minibatch128/four-epoch contract')
    started = time.perf_counter()
    grouped = {'world': [], 'combat': []}
    total = sum(len(rows) for rows in episodes)
    for rows in episodes:
        for name, selected in split_complete_episode(rows).items():
            if selected:
                grouped[name].append(selected)
    if sum(len(rows) for group in grouped.values() for rows in group) != total:
        raise ValueError('Phase partition lost or duplicated decisions')
    results = {}
    for name in ('world', 'combat'):
        if not grouped[name]:
            results[name] = dict(decisions=0, accepted_optimizer_steps=0,
                                 skipped_reason='No actual decisions for this expert')
        else:
            results[name] = update_complete(models[name], grouped[name], runtimes[name])
    return dict(episodes=len(episodes), decisions=total, experts=results,
                seconds=time.perf_counter()-started,
                scope='Complete game returns, four upstream PPO epochs per participating expert')
