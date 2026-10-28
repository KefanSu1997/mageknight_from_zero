"""Precompute immutable DTO metadata, preserving every validation check."""
import math
from solo_public_schema import PublicSchema


class CompiledPublicSchema(PublicSchema):
    def __init__(self, path):
        super().__init__(path)
        self._compiled = {}
        for name, node in self.document['types'].items():
            kind = node['kind']
            properties = tuple((p['name'], p['type'], p['reference_nullable'])
                               for p in node.get('properties', ()))
            keys = frozenset(p[0] for p in properties)
            enums = frozenset(v['value'] for v in node.get('values', ()))
            item = node.get('item')
            nullable_item = (item is not None and
                            self.document['types'][item]['kind'] in ('object', 'array', 'text'))
            self._compiled[name] = kind, properties, keys, enums, item, nullable_item

    def _walk(self, value, name, path, nullable=False):
        kind, properties, keys, enums, item, nullable_item = self._compiled[name]
        if value is None and (nullable or kind == 'nullable'):
            return
        valid = True
        if kind == 'nullable':
            return self._walk(value, item, path)
        if kind == 'object':
            valid = isinstance(value, dict) and value.keys() == keys
            if valid:
                for key, typename, optional in properties:
                    self._walk(value[key], typename, path + (key,), optional)
        elif kind == 'array':
            valid = isinstance(value, list)
            if valid:
                for i, entry in enumerate(value):
                    self._walk(entry, item, path + (i,), nullable_item)
        elif kind == 'text':
            valid = isinstance(value, str)
        elif kind == 'bool':
            valid = type(value) is bool
        elif kind == 'integer':
            valid = type(value) is int
        elif kind == 'number':
            valid = type(value) in (int, float) and math.isfinite(value)
        elif kind == 'enum':
            valid = type(value) is int and value in enums
        else:
            raise ValueError('Unsupported schema node ' + kind)
        if not valid:
            raise ValueError(f'Public schema mismatch at {path}: expected {name}')

    def validate(self, value, root='observation'):
        self._walk(value, self.document.get(root, root), ())
        return True
