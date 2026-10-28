"""Reuse bound evidence; UNKNOWN is a budget result, never an exclusion proof."""
import json
from pathlib import Path

from task_feasibility import SEQUENCE_GOAL, digest, eligible, file_hash


def read_prior(paths, dll):
    certificates, attempts, rejected = {}, [], []
    for path in paths:
        pool = json.loads(Path(path).read_text(encoding='utf-8-sig'))
        for certificate in pool.get('tasks', []):
            if eligible(certificate, dll, SEQUENCE_GOAL):
                definition = certificate['task'].get('definition')
                if definition is not None:
                    certificates[digest(definition)] = certificate
            else:
                rejected.append(dict(path=str(path), task_id=certificate.get('task_id'),
                                     reason='stale_or_invalid_certificate'))
        # Keep old evidence unchanged; only fully bound attempts can suppress work.
        attempts.extend(pool.get('quarantine', []))
    return certificates, attempts, rejected


def same_budget_unknown(attempts, task, dll, search_sha, budget):
    return any(a.get('status') == 'unknown'
               and a.get('initial_prefix_operations',0)==0
               and a.get('task_definition') == task
               and a.get('rules_sha256') == file_hash(dll)
               and a.get('search_code_sha256') == search_sha
               and a.get('budget') == budget
               and a.get('impossibility_proved') is False for a in attempts)
