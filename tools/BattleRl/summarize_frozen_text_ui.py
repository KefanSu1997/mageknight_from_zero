"""Compact evidence index for real frozen-text/UI PPO experiments."""
import argparse
import csv
import json
from pathlib import Path
import numpy as np
from structured_ui_ppo import state, write_json


def summarize(root):
    root = Path(root)
    rows, episodes, mappings = [], [], []
    runs = ('train_live', 'frozen_live', 'frozen_deed_inputs')
    for run in runs:
        for path in sorted((root/run).glob('episode_*/trajectory.json')):
            data = json.loads(path.read_text(encoding='utf-8'))
            episodes.append({'run': run, 'episode': path.parent.name,
                **json.loads((path.parent/'summary.json').read_text())})
            for sample in data['steps']:
                click = sample['actual']['click']
                before, after = state(sample['actual']['before']), state(sample['actual']['observation'])
                rows.append({'run': run, 'episode': path.parent.name, 'step': sample['index'],
                    'request_id': sample['request_id'], 'button_path': click['buttonPath'],
                    'x': click['clickX'], 'y': click['clickY'], 'hit': click['hitObject'],
                    'selected_probability': sample['probabilities'][sample['action_index']],
                    'state_text': sample['texts']['state_text'],
                    'candidate_text': sample['texts']['candidate_texts'][sample['action_index']],
                    'vector_sha256': sample['texts']['candidate_vector_sha256'][sample['action_index']],
                    'phase_before': before['phase'], 'phase_after': after['phase'],
                    'reward': sample['reward'], 'screenshot': sample['actual']['screenshotPath']})
        for path in sorted((root/run).glob('update_*.json')):
            mappings.append({'run': run, **json.loads(path.read_text())})
    with (root/'action_text_click_mapping.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.DictWriter(stream, fieldnames=rows[0])
        writer.writeheader()
        writer.writerows(rows)
    frozen_checks = []
    with np.load(root/'train_live/checkpoint_002/model.npz') as train:
        for run in runs[1:]:
            with np.load(root/run/'checkpoint_001/model.npz') as frozen:
                assert set(train.files) == set(frozen.files)
                unchanged = all(np.array_equal(train[k], frozen[k]) for k in train.files)
                assert unchanged
                frozen_checks.append({'run': run, 'arrays': len(train.files), 'parameter_adam_arrays_identical': unchanged})
    write_json(root/'summary.json', {'status': 'passed', 'real_policy_clicks': len(rows), 'episodes': episodes,
        'updates_and_correspondence': mappings, 'frozen_head_verification': frozen_checks,
        'encoder_frozen_checks': {run: json.loads((root/run/'encoder_frozen_verification.json').read_text())
                                 for run in runs},
        'scope': 'real frozen reader/head update/click gate, no policy promotion or original-rule understanding claim'})
    print(json.dumps({'real_policy_clicks': len(rows), 'episodes': episodes, 'frozen_check': frozen_checks}, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--directory', required=True)
    summarize(parser.parse_args().directory)
