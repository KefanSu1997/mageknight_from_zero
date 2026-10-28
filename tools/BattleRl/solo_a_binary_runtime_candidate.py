"""Opt-in new-runtime storage experiment; refuses implicit legacy migration."""
import hashlib
import os
import shutil
from baseline_a_scenario_runtime import ScenarioRuntime, packed
from hierarchical_option_policy import ChoiceTree
from solo_a_binary_context_candidate import MAGIC, encode, decode


class BinaryScenarioRuntime(ScenarioRuntime):
    def __init__(self, path, binding):
        if not binding:
            raise ValueError('Explicit experiment binding required')
        super().__init__(path, dict(binding, context_codec='SAB1_float32_lossless'))

    def add(self, support, document):
        if set(document) != {'state', 'rules', 'question'} or not all(isinstance(x, str) for x in document.values()):
            raise ValueError('Explicit public semantic document required')
        if not document['state'] or not document['question']:
            raise ValueError('Missing decision context')
        if hashlib.sha256(packed(list(support['commands']))).hexdigest() != support['commands_sha256']:
            raise ValueError('Support command binding mismatch')
        if ChoiceTree(support['operations']).leaves != len(support['commands']):
            raise ValueError('Complete leaf/support mismatch')
        digest, payload = encode(support, document)
        if os.name != 'nt' and shutil.disk_usage(self.path).free < 3*2**30 + 2*len(payload):
            raise RuntimeError('3GiB context-write protection')
        with self.db:
            context = self.db.execute('SELECT COALESCE(MAX(id),0)+1 FROM contexts').fetchone()[0]
            if context >= 2**24:
                raise ValueError('Context exceeds exact float32 integer range')
            self.db.execute('INSERT INTO contexts VALUES (?,?,?)', (context, digest, payload))
        return context

    def _read(self, context):
        row = self.db.execute('SELECT sha,payload FROM contexts WHERE id=?', (int(context),)).fetchone()
        if row is None:
            raise ValueError('Missing immutable context')
        if bytes(row[1]).startswith(MAGIC):
            return decode(row[0], bytes(row[1]))
        return super()._read(context)
