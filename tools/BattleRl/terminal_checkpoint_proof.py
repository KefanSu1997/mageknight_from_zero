"""Narrow impossibility proof for a formally replayed terminal continuation.

No legal actions and no future formal victory imply this exact continuation is
unsolvable. This NEVER proves that the original initial task was unsolvable.
"""
import json
from pathlib import Path
from task_feasibility import digest, file_hash


def certify_terminal_checkpoint(rules, path, dll):
    path = Path(path).resolve()
    document = json.loads(path.read_text(encoding='utf-8-sig'))
    if document.get('task_definition'):
        current = rules.reset_task(document['task_definition'])
    else:
        current = rules.reset(document['seed'])
    keys = {a['Key']: i for i, a in enumerate(rules.actions)}
    for index, row in enumerate(document['trace']):
        action = keys.get(row['action']['Key'])
        if (current['state'] != row['before'] or action not in current['legal']
                or rules.actions[action] != row['action']):
            raise ValueError(f'Checkpoint prefix is not exact and legal at {index}')
        current = rules.step(action)
        if current['state'] != row['after']:
            raise ValueError(f'Checkpoint after-state differs at {index}')
    if not current['done'] or current['legal'] or current['state']['won']:
        raise ValueError('Only a nonwinning native terminal with zero legal successors is proved')
    return dict(status='proven_unsolvable_continuation',
                goal='future_formal_group_win_after_exact_checkpoint',
                rules_sha256=file_hash(dll),prefix_path=str(path),prefix_sha256=file_hash(path),
                checkpoint_sha256=digest(current['state']),operations=len(document['trace']),
                native_terminal=True,legal_successor_count=0,
                excluded_from_continuation_victory_pool=True,
                initial_task_unsolvable=False,
                scope='Exact terminal continuation only; preserve original initial task and policy failure')
