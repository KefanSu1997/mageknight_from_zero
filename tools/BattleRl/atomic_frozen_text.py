"""Pinned frozen mmBERT inputs for atomic PPO; numeric public state is retained."""
import hashlib
import json
import os
from pathlib import Path
import gymnasium as gym
import numpy as np
from atomic_battle_env import BattleEnv as AtomicEnv
from public_entity_tokens import PublicEntityTokens, original_vocabulary

WIDTH = 384
_assets = {}

def assets(path):
    path = str(Path(path).resolve())
    if path not in _assets:
        root = Path(path)
        meta = json.loads((root/'manifest.json').read_text(encoding='utf-8'))
        for name, sha in meta['assets_sha256'].items():
            if hashlib.sha256((root/name).read_bytes()).hexdigest() != sha:
                raise ValueError('Frozen text asset binding changed: '+name)
        ids = meta['entity_ids']
        table = np.load(root/'entity_vectors.npy', allow_pickle=False)
        action = np.load(root/'action_vectors.npy', allow_pickle=False)
        if table.shape != (len(ids), WIDTH) or action.shape[1] != WIDTH:
            raise ValueError('Frozen encoder dimensions changed')
        layout = PublicEntityTokens(original_vocabulary())
        if ids != layout.ids:
            raise ValueError('Public vocabulary binding changed')
        _assets[path] = meta, table, action, layout
    return _assets[path]

def extend_descriptors(actions, ordinary):
    from printed_argument_features import extend_arguments
    base, signature = extend_arguments(actions, ordinary)
    meta, _, vectors, _ = assets(os.environ['BATTLE_RL_TEXT_ASSETS'])
    keys = hashlib.sha256(json.dumps([a['Key'] for a in actions],separators=(',', ':')).encode()).hexdigest()
    if keys != meta['action_keys_sha256'] or len(actions) != len(vectors):
        raise ValueError('Primitive action ordering changed')
    if os.environ['BATTLE_RL_TEXT_ARM'] == 'zero': vectors = np.zeros_like(vectors)
    return np.concatenate((base, vectors), axis=1), dict(signature, frozen_mmbert=meta,
        text_arm=os.environ['BATTLE_RL_TEXT_ARM'])

class BattleEnv(AtomicEnv):
    def __init__(self, *args, **kwargs):
        self.text_meta, self.entity_vectors, _, self.text_layout = assets(os.environ['BATTLE_RL_TEXT_ASSETS'])
        self.text_arm = os.environ['BATTLE_RL_TEXT_ARM']
        super().__init__(*args, **kwargs)
        old = self.observation_space['state'].shape[0]
        self.observation_space.spaces['state'] = gym.spaces.Box(-np.inf, np.inf, (old+WIDTH,), np.float32)

    def _observation(self):
        obs = super()._observation()
        if self.text_arm == 'zero':
            vector = np.zeros(WIDTH, np.float32)
        else:
            # Membership comes ONLY from verified public numeric coordinates.
            # Hidden deck order, audit state, task seed and future outcomes are never read.
            features = np.asarray(self.current['features'], np.float64)
            _, entries = self.text_layout._one(features)
            indices = [self.text_layout.id_index[identity] for identity, _, _ in entries
                       if identity != '_public_global_']
            vector = self.entity_vectors[indices].mean(axis=0) if indices else np.zeros(WIDTH, np.float32)
        obs['state'] = np.concatenate((obs['state'], vector)).astype(np.float32)
        return obs
