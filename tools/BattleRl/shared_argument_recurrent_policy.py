"""Entity Transformer/MLP3/GRU with a staged shared public-argument head."""
from action_argument_features import catalog_argument_features
from entity_recurrent_policy import EntityRecurrentPolicy
from shared_argument_policy import SharedArgumentPolicy


class SharedArgumentEntityPolicy(EntityRecurrentPolicy):
    def __init__(self, observation_size, actions, layout, encoder_sizes=(256, 256, 128),
                 hidden_size=256, embedding_size=32, seed=7, entropy_coefficient=.01,
                 value_coefficient=.02, entity_width=32, entity_heads=4, argument_rank=32):
        super().__init__(observation_size, actions, layout, encoder_sizes, hidden_size,
                         embedding_size, seed, entropy_coefficient, value_coefficient,
                         entity_width, entity_heads)
        original = self.head
        self.head = SharedArgumentPolicy(hidden_size, catalog_argument_features(actions),
            argument_rank, seed, entropy_coefficient, value_coefficient)
        self.head.enable_kind_factor(original.p['action_kind_groups'])
        for key, value in original.p.items():
            if key != 'action_kind_groups':
                self.head.p[key][...] = value
