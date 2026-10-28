"""Build a compact index from recorded actual UI evidence; never generate events."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
from structured_ui_ppo import ROOT, state, write_json


def summarize(directory):
    root = Path(directory)
    rows, summaries, updates = [], [], []
    sources = ['Assets/Scripts/SceneAutomation/StructuredUiPilot.cs',
        'Assets/Scripts/SceneAutomation/Editor/StructuredUiPilotMenu.cs',
        'tools/BattleRl/structured_ui_ppo.py', 'tools/BattleRl/local_unity_mcp.py',
        'tools/BattleRl/verify_structured_ui_pilot.py',
        'Library/ScriptAssemblies/MageKnight.Logic.dll', 'Library/ScriptAssemblies/MageKnight.Scripts.dll']
    write_json(root/'source_manifest.json', {
        'scope': 'signatures of current actual build; captured after these runs, not prior historical admission',
        'source_sha256': {p: hashlib.sha256((ROOT/p).read_bytes()).hexdigest() for p in sources}})
    for run in ('train_verified', 'frozen_live'):
        for path in sorted((root/run).glob('episode_*/trajectory.json')):
            trajectory = json.loads(path.read_text(encoding='utf-8'))
            summary = json.loads((path.parent/'summary.json').read_text(encoding='utf-8'))
            summaries.append({'run': run, 'episode': path.parent.name, **summary})
            for sample in trajectory['steps']:
                actual = sample['actual']
                before, after = state(actual['before']), state(actual['observation'])
                click = actual['click']
                rows.append({'run': run, 'episode': path.parent.name, 'step': sample['index'],
                    'request_id': sample['request_id'], 'button': click['buttonPath'],
                    'x': click['clickX'], 'y': click['clickY'], 'hit': click['hitObject'],
                    'probability': sample['probabilities'][sample['action_index']],
                    'phase_before': before.get('phase'), 'phase_after': after.get('phase'),
                    'hand_before': before.get('hand'), 'hand_after': after.get('hand'),
                    'melee_before': before.get('melee'), 'melee_after': after.get('melee'),
                    'rejections_after': after.get('rejections'), 'reward': sample['reward'],
                    'screenshot': actual['screenshotPath']})
    for path in sorted((root/'train_verified').glob('update_*.json')):
        updates.append(json.loads(path.read_text()))
    with (root/'policy_click_mapping.csv').open('w', newline='', encoding='utf-8-sig') as stream:
        writer = csv.DictWriter(stream, fieldnames=rows[0])
        writer.writeheader()
        writer.writerows(rows)
    checks = json.loads((root/'independent_examples/verification.json').read_text(encoding='utf-8'))
    negatives = json.loads((root/'negative_verified/negative_verification.json').read_text(encoding='utf-8'))
    correspondence = [json.loads((root/run/'correspondence_verification.json').read_text())
                      for run in ('train_verified', 'frozen_live')]
    summary = {'policy_clicks': len(rows), 'parameter_count': 16641, 'backend': 'NumPy CPU',
        'episodes': summaries, 'updates': updates,
        'policy_state_comparisons': sum(e['shared_state_comparisons'] for c in correspondence for e in c['episodes']),
        'independent_script_clicks': checks['clicks'], 'independent_assertions': len(checks['checks']),
        'independent_state_comparisons': checks['shared_state_comparison']['comparisons'],
        'negative_cases': len(negatives['checks']),
        'scope': 'rollout/update/checkpoint and real UI mapping gate; not policy usability or full original acceptance'}
    write_json(root/'run_summary.json', summary)
    return summary


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--directory', required=True)
    args = parser.parse_args()
    print(json.dumps(summarize(args.directory), ensure_ascii=False, indent=2))
