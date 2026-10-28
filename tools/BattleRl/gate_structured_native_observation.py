"""Actual native training-task reset: green observations plus contextual support."""
import os
import sys
import json
import hashlib
import argparse
from pathlib import Path
import numpy as np
sys.path[:0] = ['optimization_stage_20261014', 'tools/BattleRl']
import train_sb3_atomic_entity_text as base
from atomic_entity_text import BattleEnv as GreenEnv
from structured_joint_policy import BattleEnv
from train_ppo import Rules
from action_argument_features import catalog_argument_features

parser = argparse.ArgumentParser()
parser.add_argument('--out', default='optimization_stage_20261014/structured_native_observation_gate')
args = parser.parse_args()
out = Path(args.out)
out.mkdir(exist_ok=False)
os.environ['PATH'] = '/root/autodl-tmp/dotnet:' + os.environ.get('PATH', '')
os.environ['BATTLE_RL_TEXT_ASSETS'] = str(Path('MageKnight_RL_Runs/20261013_mmbert_pair/text_assets').resolve())
os.environ['BATTLE_RL_TEXT_ARM'] = 'text'
os.environ['BATTLE_RL_ATOMIC_REGISTRY'] = str((out/'atomic_supports.jsonl').resolve())
os.environ['BATTLE_RL_CONTEXT_STORE'] = str((out/'contexts').resolve())
base.configure_rules('MageKnight_RL_Runs/atomic_adapter_v2_frozen/BattleRl.dll')
rules = Rules()
try:
    history = catalog_argument_features(rules.actions)
finally:
    rules.close()
pool = json.loads(Path('MageKnight_RL_Runs/atomic_dataset/train.json').read_text())
rows = (pool['tasks'] + pool.get('unknown_tasks', []))[:8]
env = BattleEnv(rows, out, 0, history, reward_config={'version':base.SingleBattleReward.version, 'scale':.01})
cases = []
try:
    for _ in range(8):
        obs, info = env.reset()
        green = GreenEnv._observation(env)
        assert set(obs) == set(green) | {'context_id'}
        for key, value in green.items():
            np.testing.assert_array_equal(obs[key], value, err_msg=key)
        assert int(obs['context_id'][0]) > 0 and int(obs['support_id'][0]) > 0
        doc = env.context_store.read(int(obs['context_id'][0]))
        assert doc['operations'] == env.choices
        assert doc['sequences'] == env.registry.get(env.support_id)
        assert len(doc['options']) == len(env.choices)
        np.testing.assert_array_equal(obs['state'], green['state'])
        cases.append(dict(task_id=info.get('task_id'), candidates=len(env.choices),
                          public_entities=int(obs['entity_present'].sum()),
                          state_width=int(obs['state'].shape[0])))
finally:
    env.close()
report = dict(passed=True, cases=cases,
              registry_sha256=hashlib.sha256((out/'atomic_supports.jsonl').read_bytes()).hexdigest(),
              scope='8 actual native train resets. All original green observation fields exactly retained and complete contextual support bound. No policy optimizer update, learned checkpoint migration, frozen evaluation or speed claim.')
(out/'gate.json').write_text(json.dumps(report, indent=2))
print(json.dumps(report), flush=True)
