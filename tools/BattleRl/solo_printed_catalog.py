"""Version-bound original rule text for public solo candidate entities.

No hidden state, generated effects, hash-vector fallback, or remote lookup.
This catalog is independent of the legacy 6314-coordinate battle vocabulary.
"""
import hashlib
import json
from pathlib import Path

SOURCES = ('basic_card', 'advanced_card', 'magic', 'items', 'army', 'army_2', 'monster', 'skill')
OMIT = frozenset(('id', 'set', 'set_idx', 'image', 'en_image', 'wiki', 'cn_raw', 'en_card_name'))


def public_fields(value):
    if isinstance(value, dict):
        return {k: public_fields(v) for k, v in sorted(value.items()) if k not in OMIT}
    if isinstance(value, list):
        return [public_fields(v) for v in value]
    return value


class SoloPrintedCatalog:
    def __init__(self, root):
        self.entries = {}
        self.sources = {}
        self.unavailable = {}
        for name in SOURCES:
            path = Path(root) / 'resources' / 'text_json' / (name+'.json')
            raw = path.read_bytes()
            self.sources[name] = hashlib.sha256(raw).hexdigest()
            for row in json.loads(raw.decode('utf-8-sig')):
                records = [(row['id'], row)] if name != 'skill' else [
                    (f"{row['id']}:{i:02d}", skill) for i, skill in enumerate(row['skills'])]
                for identity, record in records:
                    if identity in self.entries:
                        raise ValueError('Ambiguous printed identity: '+identity)
                    text = json.dumps(public_fields(record), ensure_ascii=False, sort_keys=True,
                                      separators=(',', ':'), allow_nan=False)
                    if text == '{}':
                        # Some source rows are explicitly empty placeholders. Record
                        # the gap; attempting to use one still fails in get().
                        self.unavailable[identity] = dict(source=name, reason='empty_source_row')
                        continue
                    self.entries[identity] = dict(id=identity, text=text,
                        source=name, source_sha256=self.sources[name],
                        text_sha256=hashlib.sha256(text.encode('utf-8')).hexdigest())
        supplement_path = Path(root)/'resources/policy_text/solo_native_rules.json'
        raw = supplement_path.read_bytes()
        self.sources['solo_native_rules'] = hashlib.sha256(raw).hexdigest()
        supplements = json.loads(raw.decode('utf-8-sig'))
        if supplements['version'] != 1:
            raise ValueError('Unsupported native rule supplement')
        for row in supplements['entries']:
            identity = row['id']
            if identity in self.entries or not row['text'] or not row['sources']:
                raise ValueError('Invalid or duplicate native rule supplement')
            provenance = {}
            for relative in row['sources']:
                source = (Path(root)/relative).resolve()
                if not source.is_relative_to(Path(root).resolve()):
                    raise ValueError('Native source outside repository')
                digest = hashlib.sha256(source.read_bytes()).hexdigest()
                self.sources[relative] = digest
                provenance[relative] = digest
            text = row['text']
            self.entries[identity] = dict(id=identity, text=text, source='solo_native_rules',
                source_sha256=self.sources['solo_native_rules'], origin=row['origin'],
                native_source_sha256=provenance,
                text_sha256=hashlib.sha256(text.encode('utf-8')).hexdigest())
        self.signature = hashlib.sha256(json.dumps(self.sources, sort_keys=True).encode()).hexdigest()

    def get(self, identity):
        if identity not in self.entries:
            raise ValueError('Missing original printed text; no zero fallback: '+identity)
        return dict(self.entries[identity])

    def describe_bindings(self, bindings):
        result = []
        for binding in bindings:
            entity = binding['entity']
            identities = []
            # These fields carry original identities in the native public records.
            # The numeric command and role/slot binding remain separate inputs.
            for field in ('Id', 'SourceCardId', 'Skill', 'Banner', 'DefinitionId'):
                identity = entity.get(field)
                if identity is not None and identity not in identities:
                    identities.append(identity)
            result.append(dict(parameter=binding['parameter'], zone=binding['zone'],
                slot=binding['slot'], printed=[self.get(i) for i in identities]))
        return result
