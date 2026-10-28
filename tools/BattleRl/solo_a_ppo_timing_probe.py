"""Opt-in observational wrapper for a future isolated PPO run, never hot-patched."""
import json
import time
from pathlib import Path


def measured_update(update, model, episodes, runtime, output):
    """Preserve upstream batches/optimizer/RNG; report CPU wall around each yield.

    No device synchronization or tensor copy is added. These measurements include
    existing synchronization and must not be described as CUDA kernel timings.
    """
    original_train = model.train
    had_instance_train = 'train' in vars(model)
    instance_train = vars(model).get('train')
    records = []
    output = Path(output)

    def snapshot():
        return {key: getattr(runtime, key, None) for key in
                ('decoded_hits', 'decoded_misses', 'decoded_bytes', 'decoded_limit')}

    def train():
        buffer = model.rollout_buffer
        original_get = buffer.get
        had_instance_get = 'get' in vars(buffer)
        instance_get = vars(buffer).get('get')

        def get(*args, **kwargs):
            iterator = iter(original_get(*args, **kwargs))
            while True:
                prepared = time.perf_counter()
                try:
                    batch = next(iterator)
                except StopIteration:
                    break
                ready = time.perf_counter()
                before = snapshot()
                try:
                    yield batch
                finally:
                    done = time.perf_counter()
                    records.append(dict(batch=len(records),
                        prepare_seconds=ready-prepared,
                        consume_seconds=done-ready,
                        padded_rows=int(batch.actions.numel()),
                        cache_before=before, cache_after=snapshot()))
        buffer.get = get
        try:
            return original_train()
        finally:
            if had_instance_get:
                buffer.get = instance_get
            else:
                del buffer.get

    model.train = train
    start = time.perf_counter()
    try:
        return update(model, episodes, runtime)
    finally:
        if had_instance_train:
            model.train = instance_train
        else:
            del model.train
        output.write_text(json.dumps(dict(
            seconds=time.perf_counter()-start, batches=records,
            cache_final=snapshot(),
            scope='Observational CPU wall; no CUDA sync added; full update wall remains authoritative'),indent=2))
