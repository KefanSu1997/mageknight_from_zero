"""Spawned CPU preparation experiment; no native environment or model execution."""
import os
import time
import multiprocessing
from concurrent.futures import ProcessPoolExecutor
from pathlib import Path

_inputs = _renderer = None
_tree_factory = None
_compact_packet = False


def initialize(config):
    global _inputs, _renderer, _tree_factory, _compact_packet
    os.environ.update(OMP_NUM_THREADS='1', OPENBLAS_NUM_THREADS='1', MKL_NUM_THREADS='1')
    from solo_a_schema_compiled_candidate import CompiledPublicSchema
    from solo_text_assets import SoloTextAssets
    from solo_phase_text import PhaseText
    import baseline_a_scenario_inputs as mod
    import solo_a_command_compile_v2_candidate as actions
    from solo_a_numeric_cache_candidate import catalog_argument_features
    from solo_support_blocks_candidate import prepare_support
    actions.catalog_argument_features = catalog_argument_features
    mod.prepare_support = prepare_support
    schema = CompiledPublicSchema(config['schema'])
    assets = SoloTextAssets(config['assets'], config['catalog_repo'])
    if schema.signature != config['schema_signature'] or assets.catalog.signature != config['catalog_signature']:
        raise ValueError('CPU worker source/data binding differs')
    _inputs = mod.ScenarioInputs(Path(config['repo']), schema, assets, config['entity_ids'])
    _renderer = PhaseText(schema, assets)
    _compact_packet = bool(config.get('compact_prepared_packet', False))
    if config.get('compact_choice_tree', False):
        from solo_compact_tree_candidate import CompactChoiceTree
        _tree_factory = CompactChoiceTree
    else:
        from hierarchical_option_policy import ChoiceTree
        _tree_factory = ChoiceTree


def prepare(request):
    if _inputs is None:
        raise RuntimeError('CPU worker not initialized')
    from solo_a_decoded_cache_v3_candidate import canonical
    from solo_a_compact_context_candidate import encode
    request_id, native, history = request
    started = time.perf_counter()
    observation, support = _inputs.encode(native, history)
    encoded = time.perf_counter()
    if support is None:
        raise ValueError('No preparation request for terminal state')
    document = canonical(_renderer.render(native['observation'], support))
    support = canonical(support)
    rendered = time.perf_counter()
    tree = _tree_factory(support['operations'])
    if tree.leaves != len(support['commands']):
        raise ValueError('Incomplete support')
    tree_done = time.perf_counter()
    if _compact_packet:
        from solo_prepared_codec_candidate import encode_with_view
        digest, payload, support = encode_with_view(support, document)
    else:
        digest, payload = encode(support, document)
    packed = time.perf_counter()
    return dict(request_id=request_id, step=native['observation']['Step'],
                native_support_sha256=native['support_sha256'], observation=observation,
                support=support, document=document, tree=tree, digest=digest, payload=payload,
                compact_prepared_packet=_compact_packet,
                preparation_timing=dict(input_encode=encoded-started,
                    render_canonical=rendered-encoded, tree_build=tree_done-rendered,
                    encode_payload=packed-tree_done))


class ParallelPrepare:
    def __init__(self, config, workers=4):
        if type(workers) is not int or not 1 <= workers <= 8:
            raise ValueError('CPU preparation worker budget exceeded')
        self.pool = ProcessPoolExecutor(max_workers=workers,
            mp_context=multiprocessing.get_context('spawn'), initializer=initialize, initargs=(config,))
        self.timing = dict(batches=0, requests=0, wait_wall_seconds=0.,
                          worker_seconds={})

    def prepare_many(self, requests):
        requests = list(requests)
        if len({r[0] for r in requests}) != len(requests):
            raise ValueError('Duplicate preparation request identity')
        started = time.perf_counter()
        result = list(self.pool.map(prepare, requests, chunksize=1))
        self.timing['wait_wall_seconds'] += time.perf_counter()-started
        self.timing['batches'] += 1
        self.timing['requests'] += len(result)
        # These are sums across workers, not additive parent wall-clock costs.
        for packet in result:
            for key, seconds in packet['preparation_timing'].items():
                sums = self.timing['worker_seconds']
                sums[key] = sums.get(key, 0.) + seconds
        for expected, actual in zip(requests, result):
            identity, native, _ = expected
            if (identity != actual['request_id'] or native['observation']['Step'] != actual['step']
                    or native['support_sha256'] != actual['native_support_sha256']):
                raise ValueError('CPU result assigned to the wrong decision')
        return result

    def close(self):
        self.pool.shutdown(wait=True)

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.close()
