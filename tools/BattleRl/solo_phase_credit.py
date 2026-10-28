"""Gamma=1 complete-game credit for independently recurrent phase experts."""
import copy
import math


def split_complete_episode(rows):
    """Collapse intervening rewards without turning battles into terminal games.

    Collection must save each expert's own recurrent state and episode_start.
    Those fields are preserved; this helper must never invent/reset memory.
    Each expert's last decision receives every remaining game reward. Therefore
    each selected decision's return equals its original whole-game return.
    This contract relies on gamma=lambda=1, as in the A experiment.
    """
    if not rows or not rows[-1]['terminated']:
        raise ValueError('Only complete games may be partitioned')
    if any(r['terminated'] for r in rows[:-1]):
        raise ValueError('Interior terminal boundary')
    if any(r['expert'] not in ('combat', 'world') for r in rows):
        raise ValueError('Unknown public phase expert')
    if any(not math.isfinite(float(r['reward'])) for r in rows):
        raise ValueError('Nonfinite game reward')
    result = {}
    for expert in ('combat', 'world'):
        indices = [i for i, row in enumerate(rows) if row['expert'] == expert]
        selected = []
        for j, i in enumerate(indices):
            stop = indices[j+1] if j+1 < len(indices) else len(rows)
            row = copy.copy(rows[i])
            row['reward'] = math.fsum(float(r['reward']) for r in rows[i:stop])
            row['terminated'] = stop == len(rows)
            row['game_step_index'] = i
            row['reward_span_end'] = stop
            selected.append(row)
        if selected:
            if not selected[0]['episode_start'] or any(r['episode_start'] for r in selected[1:]):
                raise ValueError('Expert recurrent state must span its whole-game subsequence')
        result[expert] = selected
    return result
