"""Bounded preparation caches for immutable journal contexts; opt-in candidate."""
import json
from collections import OrderedDict
from solo_split_text_runtime import SplitTextRuntime


class PreparedSplitRuntime(SplitTextRuntime):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.prepared_documents = OrderedDict()
        self.prepared_rules = OrderedDict()
        self.document_bytes = self.rule_bytes = 0

    def dynamic_document(self, context, node):
        key = (int(context), int(node))
        if key in self.prepared_documents:
            self.prepared_documents.move_to_end(key)
            return self.prepared_documents[key][0]
        document = super().dynamic_document(*key)
        size = len(json.dumps(document, ensure_ascii=False).encode('utf-8'))
        # Conservative allowance for Python containers/strings, with a count cap
        # as well as content bytes. This is not a whole-process memory bound.
        charged = size * 4 + 1024 + 128 * len(document['options'])
        if charged <= 16 * 2**20:
            self.prepared_documents[key] = (document, charged)
            self.document_bytes += charged
        while self.document_bytes > 16 * 2**20 or len(self.prepared_documents)>4096:
            _, (_, cost) = self.prepared_documents.popitem(last=False)
            self.document_bytes -= cost
        return document

    def rule_vectors(self, context):
        context = int(context)
        if context in self.prepared_rules:
            self.prepared_rules.move_to_end(context)
            return self.prepared_rules[context]
        vectors = super().rule_vectors(context)
        vectors.setflags(write=False)
        if vectors.nbytes <= 16 * 2**20:
            self.prepared_rules[context] = vectors
            self.rule_bytes += vectors.nbytes
        while self.rule_bytes > 16 * 2**20 or len(self.prepared_rules)>1024:
            _, old = self.prepared_rules.popitem(last=False)
            self.rule_bytes -= old.nbytes
        return vectors
