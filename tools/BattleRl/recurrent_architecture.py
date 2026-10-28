"""Explicit saved architecture selection; preserve the ordinary actor default."""
from pathlib import Path
from recurrent_policy import RecurrentPolicy
from task_feasibility import digest, file_hash


def build_policy(observation_size, actions, feature_schema, entity_transformer=False, shared_argument_head=False, printed_information=False):
    if printed_information and not shared_argument_head:
        raise ValueError('Printed public information requires the shared argument head')
    if shared_argument_head and not entity_transformer:
        raise ValueError('Shared public argument head currently requires the verified entity actor')
    kinds = len({action['Kind'] for action in actions})
    if not entity_transformer:
        return RecurrentPolicy(observation_size, actions), dict(
            architecture=f'MLP256-256-128_GRU256_factored{kinds}_original_catalog')
    from public_entity_tokens import PublicEntityTokens, original_vocabulary
    from entity_recurrent_policy import EntityRecurrentPolicy
    layout = PublicEntityTokens(original_vocabulary(), feature_schema, observation_size)
    metadata = dict(entity_transformer=True, entity_width=32, entity_heads=4,
        entity_vocabulary_sha256=digest(layout.vocabulary),
        architecture=f'EntityTransformer32_heads4_MLP256-256-128_GRU256_factored{kinds}_original_catalog')
    for key, source in (('entity_transformer_sha256', 'entity_transformer.py'),
                        ('entity_recurrent_policy_sha256', 'entity_recurrent_policy.py'),
                        ('public_entity_tokens_sha256', 'public_entity_tokens.py'),
                        ('recurrent_architecture_sha256', 'recurrent_architecture.py')):
        metadata[key] = file_hash(Path(__file__).with_name(source))
    if shared_argument_head:
        import hashlib
        from shared_argument_recurrent_policy import SharedArgumentEntityPolicy
        policy = SharedArgumentEntityPolicy(observation_size, actions, layout)
        metadata.update(shared_argument_head=True, argument_head_rank=32,
            argument_feature_width=96, argument_descriptor_sha256=hashlib.sha256(policy.head.argument_features.tobytes()).hexdigest(),
            architecture='SharedArgument32_' + metadata['architecture'])
        for key, source in (('action_argument_features_sha256','action_argument_features.py'),
                            ('shared_argument_policy_sha256','shared_argument_policy.py'),
                            ('shared_argument_recurrent_policy_sha256','shared_argument_recurrent_policy.py')):
            metadata[key] = file_hash(Path(__file__).with_name(source))
        if printed_information:
            import numpy as np
            from printed_argument_features import extend_arguments
            features, signature = extend_arguments(actions, policy.head.argument_features, layout)
            policy.head.argument_features = features
            policy.head.p['argument_project'] = np.zeros((features.shape[1], 32))
            metadata.update(printed_information=True, argument_feature_width=features.shape[1],
                argument_descriptor_sha256=hashlib.sha256(features.tobytes()).hexdigest(),
                printed_sources_sha256=digest(signature),
                printed_entity_text_sha256=file_hash(Path(__file__).with_name('printed_entity_text.py')),
                printed_argument_features_sha256=file_hash(Path(__file__).with_name('printed_argument_features.py')),
                architecture='PrintedPublic_' + metadata['architecture'])
        return policy, metadata
    return EntityRecurrentPolicy(observation_size, actions, layout), metadata
