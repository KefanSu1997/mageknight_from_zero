"""Schema-bound, lossless ragged tensors for full-scenario public DTOs.

No slot cap, feature hashing, vocabulary fitted on evaluation, or string-to-zero
fallback. Text is UTF-8 bytes; a downstream learned encoder may consume it or
resolve printed identities through SoloTextAssets. This is not a PPO adapter.
"""
import numpy as np


KINDS = ('null', 'object', 'array', 'text', 'bool', 'integer', 'number', 'enum')


class PublicTensorizer:
    def __init__(self, schema):
        self.schema = schema
        self.types = tuple(sorted(schema.document['types']))
        self.type_ids = {name: i for i, name in enumerate(self.types)}
        self.fields = tuple(sorted({p['name'] for node in schema.document['types'].values()
                                    for p in node.get('properties', [])}))
        self.field_ids = {name: i for i, name in enumerate(self.fields)}

    def encode(self, value, root='observation'):
        self.schema.validate(value, root)
        rows, integers, numbers, text = [], [], [], bytearray()
        def walk(value, name, parent=-1, field=-1, index=-1):
            node = self.schema.document['types'][name]
            effective = node
            while effective['kind'] == 'nullable':
                effective = self.schema.document['types'][effective['item']]
            kind = 'null' if value is None else effective['kind']
            position = len(rows)
            # Parent precedes child. Array index is local, never a global ID.
            rows.append([parent, field, index, self.type_ids[name], KINDS.index(kind), 0, 0])
            integers.append(0); numbers.append(0.)
            if kind == 'object':
                for prop in effective['properties']:
                    walk(value[prop['name']], prop['type'], position, self.field_ids[prop['name']])
            elif kind == 'array':
                integers[position] = len(value)
                for i, item in enumerate(value):
                    walk(item, effective['item'], position, index=i)
            elif kind == 'text':
                encoded = value.encode('utf-8')
                rows[position][5:] = [len(text), len(encoded)]
                text.extend(encoded)
            elif kind in ('integer', 'enum', 'bool'):
                integers[position] = int(value)
            elif kind == 'number':
                numbers[position] = value
        walk(value, self.schema.document.get(root, root))
        return dict(schema_sha256=self.schema.signature,
                    nodes=np.asarray(rows, dtype=np.int64),
                    integers=np.asarray(integers, dtype=np.int64),
                    numbers=np.asarray(numbers, dtype=np.float64),
                    text=np.frombuffer(bytes(text), dtype=np.uint8))

    def decode(self, tensors):
        """Audit inverse; model inputs must still exclude transport Step counters."""
        if tensors['schema_sha256'] != self.schema.signature:
            raise ValueError('Public tensor schema mismatch')
        nodes = tensors['nodes']; values = []
        raw = tensors['text'].tobytes()
        for i, row in enumerate(nodes):
            parent, field, index, type_id, kind_id, offset, length = map(int, row)
            kind = KINDS[kind_id]
            if kind == 'object': value = {}
            elif kind == 'array': value = []
            elif kind == 'text': value = raw[offset:offset+length].decode('utf-8')
            elif kind == 'null': value = None
            elif kind == 'bool': value = bool(tensors['integers'][i])
            elif kind in ('integer', 'enum'): value = int(tensors['integers'][i])
            else: value = float(tensors['numbers'][i])
            values.append(value)
            if i == 0:
                if parent != -1: raise ValueError('Invalid root parent')
            else:
                if not 0 <= parent < i: raise ValueError('Invalid parent ordering')
                container = values[parent]
                if isinstance(container, list):
                    if index != len(container): raise ValueError('Invalid array order')
                    container.append(value)
                else:
                    key = self.fields[field]
                    if key in container: raise ValueError('Duplicate field')
                    container[key] = value
        result = values[0]
        self.schema.validate(result, self.types[int(nodes[0, 3])])
        return result

    def policy_node_mask(self, tensors):
        """Omit only root transport metadata, retaining all native clocks."""
        nodes = tensors['nodes']
        excluded = {'Step', 'Version', 'ExpectedStep', 'SchemaVersion'}
        return np.asarray([not (parent == 0 and field >= 0 and self.fields[field] in excluded)
                           for parent, field, *_ in nodes], dtype=np.bool_)
