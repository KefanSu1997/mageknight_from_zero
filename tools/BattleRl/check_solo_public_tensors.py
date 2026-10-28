"""Round-trip actual native fixture observations and legal input payloads."""
import argparse
import hashlib
import json
from pathlib import Path
import numpy as np
from solo_public_schema import PublicSchema
from solo_public_tensors import PublicTensorizer


def check(directory):
    directory = Path(directory)
    tensorizer = PublicTensorizer(PublicSchema(directory/'public_dto_schema.json'))
    unit = json.loads((directory/'native_unit_binding_fixtures.json').read_text(encoding='utf-8'))
    skills = json.loads((directory/'native_skill_binding_fixtures.json').read_text(encoding='utf-8'))
    cases = [(row['observation'], 'observation') for row in [unit, *skills]]
    cases += [(command, 'MK.Logic.Runtime.Battles.SoloUnitInput') for command in unit['commands']]
    cases += [(command, 'MK.Logic.Runtime.Battles.SoloSkillInput')
              for row in skills for command in row['commands']]
    counts = []
    for value, root in cases:
        packed = tensorizer.encode(value, root)
        assert tensorizer.decode(packed) == value
        again = tensorizer.encode(value, root)
        for key in ('nodes', 'integers', 'numbers', 'text'):
            assert np.array_equal(packed[key], again[key])
        # Actual native clocks must survive; only the root transport metadata is excluded.
        mask = tensorizer.policy_node_mask(packed)
        omitted = [tensorizer.fields[int(row[1])] for row in packed['nodes'][~mask]]
        assert set(omitted) <= {'Step', 'Version', 'ExpectedStep', 'SchemaVersion'}
        counts.append(dict(root=root, nodes=len(packed['nodes']), text_bytes=len(packed['text']),
                           bytes=sum(packed[k].nbytes for k in ('nodes','integers','numbers','text'))))
    # No categorical vocabulary loss for arbitrary public Chinese text.
    arbitrary = '手牌效果：移动２／治疗伤牌。\n未知名称也须保留🙂'
    assert tensorizer.decode(tensorizer.encode(arbitrary, 'System.String')) == arbitrary
    # A schema mismatch cannot silently reinterpret an archive.
    broken = dict(packed, schema_sha256='wrong')
    try: tensorizer.decode(broken)
    except ValueError: pass
    else: raise AssertionError('Accepted wrong schema')
    result = dict(passed=True, actual_native_cases=len(cases), cases=counts,
                  schema_sha256=tensorizer.schema.signature,
                  implementation_sha256=hashlib.sha256(Path(__file__).with_name('solo_public_tensors.py').read_bytes()).hexdigest(),
                  scope='lossless public tensor serialization; not neural or PPO integration',
                  training_started=False)
    (directory/'public_tensor_gate.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps(result))


if __name__ == '__main__':
    parser=argparse.ArgumentParser();parser.add_argument('directory')
    check(parser.parse_args().directory)
