"""Preserve action/click evidence with checksums and source snapshots, without overwrites."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import zipfile
from structured_ui_ppo import ROOT, SCHEMA, category, CATEGORIES


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def archive(source, destination):
    source, destination = Path(source).resolve(), Path(destination).resolve()
    if destination.exists() or destination.with_suffix('.zip').exists():
        raise ValueError('Use a fresh archive destination; existing evidence is immutable')
    rows = []
    for run in ('train_verified', 'frozen_live'):
        for trajectory in sorted((source/run).glob('episode_*/trajectory.json')):
            data = json.loads(trajectory.read_text(encoding='utf-8'))
            for sample in data['steps']:
                click = sample['actual']['click']
                rows.append({'role': sample['role'], 'run': run, 'episode': trajectory.parent.name,
                    'step': sample['index'], 'request_id': sample['request_id'],
                    'action': {'candidate_index': sample['action_index'],
                               'category': CATEGORIES[category(sample['selected']['name'])],
                               'control': sample['selected'],
                               'probability': sample['probabilities'][sample['action_index']]},
                    'observation_version': sample['before_version'],
                    'pointer': {key: click[key] for key in ('buttonPath', 'clickX', 'clickY', 'hitObject', 'success')},
                    'evidence': trajectory.relative_to(source).as_posix(),
                    'screenshot': Path(sample['actual']['screenshotPath']).relative_to(source).as_posix()
                                  if sample['actual']['screenshotPath'] else None})
    examples = source/'independent_examples/examples.json'
    for index, sample in enumerate(json.loads(examples.read_text(encoding='utf-8'))['steps']):
        click = sample['actual']['click']
        rows.append({'role': sample['role'], 'run': 'independent_examples', 'step': index,
            'request_id': sample['actual']['id'], 'action': {'control_name': sample['name']},
            'observation_version': sample['actual']['before']['version'],
            'pointer': {key: click[key] for key in ('buttonPath', 'clickX', 'clickY', 'hitObject', 'success')},
            'evidence': examples.relative_to(source).as_posix(),
            'screenshot': Path(sample['actual']['screenshotPath']).relative_to(source).as_posix()
                          if sample['actual']['screenshotPath'] else None})
    assert len(rows) == 99 and sum(row['role'] != 'scripted_validation_not_policy' for row in rows) == 81
    destination.mkdir(parents=True)
    evidence = destination/'evidence'
    evidence.mkdir()
    for name in ('train_verified', 'frozen_live', 'independent_examples', 'negative_verified'):
        shutil.copytree(source/name, evidence/name)
    for name in ('policy_click_mapping.csv', 'run_summary.json', 'source_manifest.json',
                 'frozen_parameter_verification.json', 'console_final_1.json', 'console_final_2.json'):
        shutil.copy2(source/name, evidence/name)
    snapshot_checks = []
    old_manifest = json.loads((source/'source_manifest.json').read_text())
    for relative, expected in old_manifest['source_sha256'].items():
        path = ROOT/relative
        matches = path.exists() and sha(path) == expected
        snapshot_checks.append({'path': relative, 'expected_sha256': expected,
                                'current_matches_recorded': matches})
        if matches:
            target = destination/'source_snapshot'/relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, target)
    with (destination/'action_click_index.jsonl').open('w', encoding='utf-8') as stream:
        for row in rows:
            stream.write(json.dumps(row, ensure_ascii=False)+'\n')
    manifest = {'schema': 'structured-ui-action-click-evidence@1', 'feature_contract': SCHEMA,
        'source_directory': str(source), 'policy_clicks': 81, 'scripted_validation_clicks': 18,
        'negative_cases': 3, 'source_snapshot_checks': snapshot_checks,
        'binding': 'candidate index is local to an observation; resolve actual control path and raycast at execution',
        'scope': 'recorded pointer correspondence, not complete Unity RNG/state replay or trained-policy victory',
        'files': {p.relative_to(destination).as_posix(): {'sha256': sha(p), 'bytes': p.stat().st_size}
                  for p in sorted(destination.rglob('*')) if p.is_file()}}
    (destination/'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    with zipfile.ZipFile(destination.with_suffix('.zip'), 'x', zipfile.ZIP_DEFLATED) as bundle:
        for path in sorted(destination.rglob('*')):
            if path.is_file():
                bundle.write(path, path.relative_to(destination).as_posix())
    # Re-read every saved member, so successful archive creation also confirms integrity.
    with zipfile.ZipFile(destination.with_suffix('.zip')) as bundle:
        assert bundle.testzip() is None
        for relative, info in manifest['files'].items():
            assert hashlib.sha256(bundle.read(relative)).hexdigest() == info['sha256']
    print(json.dumps({'archive': str(destination), 'zip': str(destination.with_suffix('.zip')),
        'zip_sha256': sha(destination.with_suffix('.zip')), 'files_verified': len(manifest['files']),
        'policy_clicks': 81, 'scripted_clicks': 18, 'source_snapshot_checks': snapshot_checks}, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', required=True)
    parser.add_argument('--destination', required=True)
    args = parser.parse_args()
    archive(args.source, args.destination)
