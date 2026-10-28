"""Concurrent native processes with one in-flight request per environment.

Poll timeouts preserve running futures; they never reset or restart a task.
Each completion retains its worker/task identity, including failures.
"""
from concurrent.futures import ThreadPoolExecutor, wait, FIRST_COMPLETED
from dataclasses import dataclass
from pathlib import Path
import time
from solo_native_env import SoloNativeEnv


@dataclass(frozen=True)
class Completion:
    worker: int
    task_id: str
    operation: str
    wall_seconds: float
    result: object = None
    error: str = None


class SoloNativePool:
    def __init__(self, dll, repository, evidence_directory, workers=32, *, preparation=False):
        if type(workers) is not int or workers < 1:
            raise ValueError('Positive worker count required')
        self.envs = []
        self.pending = {}
        self.tasks = {}
        self.ready = set()
        self.executor = ThreadPoolExecutor(max_workers=workers)
        try:
            for i in range(workers):
                self.envs.append(SoloNativeEnv(dll, repository, Path(evidence_directory)/f'worker_{i:03d}',
                                               preparation=preparation))
        except BaseException:
            self.close()
            raise

    def submit(self, worker, task_id, operation, *args, **kwargs):
        if type(worker) is not int or not 0 <= worker < len(self.envs):
            raise ValueError('Unknown worker')
        if worker in self.pending:
            raise RuntimeError('Worker already has an in-flight request')
        if not isinstance(task_id, str) or not task_id:
            raise ValueError('Explicit task identity required')
        if operation not in ('reset', 'step', 'restore', 'save', 'export'):
            raise ValueError('Unsupported pool operation')
        if operation not in ('reset', 'restore') and self.tasks.get(worker) != task_id:
            raise ValueError('Task identity differs from the current worker assignment')
        if operation not in ('reset', 'restore') and worker not in self.ready:
            raise ValueError('Worker has no successfully initialized task')
        if operation in ('reset', 'restore') and any(t == task_id for w, t in self.tasks.items() if w != worker):
            raise ValueError('Task is already assigned to a different worker')
        # Reserve identity immediately; failed resets remain visible as failed tasks.
        if operation in ('reset', 'restore'):
            self.tasks[worker] = task_id
            self.ready.discard(worker)
        def execute():
            start = time.perf_counter()
            try:
                result = getattr(self.envs[worker], operation)(*args, **kwargs)
                return Completion(worker, task_id, operation, time.perf_counter()-start, result=result)
            except Exception as exc:
                return Completion(worker, task_id, operation, time.perf_counter()-start,
                                  error=f'{type(exc).__name__}: {exc}')
        future = self.executor.submit(execute)
        self.pending[worker] = (future, task_id, operation)

    def poll(self, timeout=None):
        if not self.pending:
            return []
        done, _ = wait([v[0] for v in self.pending.values()], timeout=timeout, return_when=FIRST_COMPLETED)
        rows = []
        for worker, (future, task, op) in list(self.pending.items()):
            if future not in done:
                continue
            row = future.result()
            rows.append(row)
            if op in ('reset', 'restore') and row.error is None:
                self.ready.add(worker)
            del self.pending[worker]
        return rows

    def close(self):
        # Finish existing work; only close processes owned by this pool.
        self.executor.shutdown(wait=True)
        for env in self.envs:
            env.close()

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.close()
