"""Assemble schema-bound public tensors and frozen rules from native supports.

The native signature authenticates ordering/observation consistency within an
archive, not adversarial authenticity or complete rule implementation coverage.
No private replay/checkpoint field is accepted as a policy input.
"""
import hashlib
import json
from copy import deepcopy
from solo_candidate_entities import candidate_entities, SUPPORTED_OPERATIONS
from solo_public_tensors import PublicTensorizer


class SoloPolicyInputs:
    def __init__(self, schema, text_assets):
        self.schema=schema
        self.tensors=PublicTensorizer(schema)
        self.text_assets=text_assets
        actual={p['name'] for p in schema.document['types'][schema.document['command']]['properties']}
        if actual-{'SchemaVersion','ExpectedStep'} != SUPPORTED_OPERATIONS:
            raise ValueError('Native command schema changed; explicit input migration required')

    def prepare_record(self, record):
        # Reconstruct the original public signing envelope, then verify its
        # recorded SHA. A serialization mismatch must fail, not mint a new SHA.
        payload=json.dumps(dict(version=1,step=record['BeforeStep'],
                                observation=record['ObservationBefore'],commands=record['CommandsJson']),
                           ensure_ascii=False,separators=(',',':'))
        return self.prepare(dict(observation=json.loads(record['ObservationBefore']),
                                 commands=record['CommandsJson'],support_payload_json=payload,
                                 support_sha256=record['SupportSha256']))

    def prepare(self, native_state):
        observation=native_state['observation']
        self.schema.validate(observation)
        commands=native_state['commands']
        if observation['Terminal']:
            if commands or native_state['support_sha256'] is not None or native_state.get('support_payload_json') is not None:
                raise ValueError('Terminal state unexpectedly carries selectable support')
        else:
            payload=native_state['support_payload_json']
            if not isinstance(payload,str) or hashlib.sha256(payload.encode('utf-8')).hexdigest()!=native_state['support_sha256']:
                raise ValueError('Changed native support payload')
            signed=json.loads(payload)
            if (set(signed)!={'version','step','observation','commands'} or signed['version']!=1
                or signed['step']!=observation['Step'] or json.loads(signed['observation'])!=observation
                or signed['commands']!=commands or len(set(commands))!=len(commands)):
                raise ValueError('Public state or ordered commands differ from signed boundary')
            if not commands:
                raise ValueError(f'Nonterminal native state has no legal support at step {observation["Step"]}; preserve and replay rule obligations')
        state=self.tensors.encode(observation)
        candidates=[]
        for encoded in commands:
            command=json.loads(encoded)
            self.schema.validate(command,'command')
            refs=candidate_entities(observation,command)
            numeric=self.tensors.encode(command,'command')
            candidates.append(dict(numeric=numeric, node_mask=self.tensors.policy_node_mask(numeric),
                                   references=refs, texts=self.text_assets.encode_bindings(refs)))
        refs=state_entities(observation)
        return dict(state=state,state_node_mask=self.tensors.policy_node_mask(state),
                    state_references=refs,state_texts=self.text_assets.encode_bindings(refs),
                    candidates=candidates,commands=tuple(commands),
                    support_sha256=native_state['support_sha256'],
                    schema_sha256=self.schema.signature,catalog_signature=self.text_assets.catalog.signature)


def state_entities(observation):
    """Known printed-identity fields only. Ruin IDs are not monster/card IDs."""
    result=[]
    zones={
        'Inventory':('Hand','Discard','Played','RoundAside','Units','Skills','PuppetCandidates','PuppetStored'),
        'Offers':('Units','Spells','AdvancedActions','CommonSkills','LevelSkills','AdventureArtifacts',
                  'RuinArtifacts','CityArtifacts','MonasteryActions'),
        'Combat':('Enemies','Attacks','Blocks'),
    }
    for branch, fields in zones.items():
        for field in fields:
            for index, entity in enumerate((observation.get(branch) or {}).get(field,[])):
                if entity['Slot']!=index:
                    raise ValueError('Public entity slot ordering differs')
                result.append(dict(parameter=(branch,field,index),zone=(branch,field),slot=index,entity=deepcopy(entity)))
    for i, cell in enumerate(observation['World']['Cells']):
        for guard in (cell.get('Site') or {}).get('Guards',[]):
            if not guard['Revealed'] and guard['DefinitionId'] is not None:
                raise ValueError('Hidden map defender identity leaked')
            result.append(dict(parameter=('World','Cells',i,'Guard',guard['Slot']),
                               zone=('World','Cells',i,'Site','Guards'),slot=guard['Slot'],entity=deepcopy(guard)))
    scouting=observation['World'].get('LastScouting')
    if scouting is not None and scouting.get('EnemyId') is not None:
        result.append(dict(parameter=('World','LastScouting','EnemyId'),zone=('World','LastScouting'),
                           slot=None,entity=dict(Id=scouting['EnemyId'])))
    repeated=observation['Offers'].get('RepeatedEffect')
    if repeated is not None:
        result.append(dict(parameter=('Offers','RepeatedEffect','CardId'),zone=('Offers','RepeatedEffect'),
                           slot=None,entity=dict(Id=repeated['CardId'])))
    return result
