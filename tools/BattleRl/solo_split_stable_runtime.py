"""Schedule-independent short text encoding; opt-in new runtime binding.

The BF16 reader produced different representations for an actual two-document
batch. Keep CPU preparation and policy scoring batched, but encode each unique
short document alone. Cached values are therefore independent of ready batches.
"""
from solo_a_reader_once_candidate import SingleTokenizationReader
from solo_split_prepared_runtime import PreparedSplitRuntime


class SingleDocumentReader(SingleTokenizationReader):
    def encode_many(self, documents, batch_size=None):
        if batch_size not in (None, 1):
            raise ValueError('This encoder contract requires single-document batches')
        return super().encode_many(documents, batch_size=1)


class StableSplitRuntime(PreparedSplitRuntime):
    def __init__(self, path, binding, assets, **kwargs):
        super().__init__(path, dict(binding, short_text_encoding='single_document_bf16_v1'),
                         assets, **kwargs)

    def vectors(self, requests):
        requests = list(requests)
        if self.reader is None and any(context for context, _ in requests):
            self.reader = SingleDocumentReader()
        return super().vectors(requests)
