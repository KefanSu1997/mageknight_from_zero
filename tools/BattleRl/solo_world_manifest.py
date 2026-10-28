"""Materialize immutable family splits from native canonical exports.

This does not generate missing world content or infer a world from RNG seeds.
The private manifest must never be included in policy observations.
"""
import hashlib
import json
from pathlib import Path
from solo_conquest_contract import family_digest, split_worlds

HEROES = ('Arythea', 'Tovak', 'Goldyx', 'Norowas', 'Wolfhawk', 'Krang', 'Braevalar')


def encoded(value):
    return json.dumps(value, sort_keys=True, ensure_ascii=False,
                      separators=(',', ':'), allow_nan=False).encode('utf-8')


def assemble(exports):
    """Each export supplies its canonical world and seven actual native setups."""
    by_family = {}
    for row in exports:
        key = family_digest(row['world'])
        if key in by_family:
            raise ValueError('Duplicate canonical world, even if its seeds differ')
        tasks = row['tasks']
        if len(tasks) != 7 or {t['setup']['Hero'] for t in tasks} != set(HEROES):
            raise ValueError('Each family requires all seven heroes exactly once')
        for task in tasks:
            setup = task['setup']
            if setup.get('DualColorRule') != 1:
                raise ValueError('Formal dummy dual-color rule must be MaximumMatchingCrystals (1)')
            if setup['Version'] != 1 or setup['DummyHero'] not in HEROES or setup['DummyHero'] == setup['Hero']:
                raise ValueError('Invalid native hero/dummy setup')
            if type(task['actor_seed']) is not int or task['actor_seed'] < 0:
                raise ValueError('Explicit independent task actor seed required')
            if not setup.get('Seeds'):
                raise ValueError('Missing native recovery seeds')
        by_family[key] = row
    splits = split_worlds([r['world'] for r in exports])
    result = {}
    for name, families in splits.items():
        tasks = []
        for family in families:
            key = family['family_sha256']
            source = {t['setup']['Hero']: t for t in by_family[key]['tasks']}
            for hero in HEROES:
                task = source[hero]
                tasks.append(dict(id=key+'/'+hero, family_sha256=key,
                                  setup=task['setup'], actor_seed=task['actor_seed']))
        result[name] = dict(version=1, split=name, test_locked=name=='test',
                            families=families, tasks=tasks)
    return result


def write_manifests(exports, output):
    manifests = assemble(exports)
    output = Path(output)
    # A failed or previous generation cannot be silently overwritten.
    output.mkdir(parents=True, exist_ok=False)
    index = dict(version=1, files={})
    for split, manifest in manifests.items():
        payload = encoded(manifest)
        filename = split+'.json'
        (output/filename).write_bytes(payload)
        index['files'][split] = dict(file=filename, sha256=hashlib.sha256(payload).hexdigest(),
                                    worlds=len(manifest['families']), episodes=len(manifest['tasks']))
    (output/'index.json').write_bytes(encoded(index))
    return index


def read_tasks(directory, split, *, final_test=False):
    if split not in ('train', 'validation', 'test'):
        raise ValueError('Unknown split')
    if split == 'test' and not final_test:
        raise ValueError('Test tasks are locked until final model selection')
    directory = Path(directory)
    index = json.loads((directory/'index.json').read_text(encoding='utf-8'))
    entry = index['files'][split]
    if entry['file'] != split+'.json':
        raise ValueError('Unexpected manifest filename')
    payload = (directory/entry['file']).read_bytes()
    if hashlib.sha256(payload).hexdigest() != entry['sha256']:
        raise ValueError('World manifest changed')
    manifest = json.loads(payload)
    if manifest['split'] != split or len(manifest['tasks']) != entry['episodes']:
        raise ValueError('Manifest split/count mismatch')
    if any(t['setup'].get('DualColorRule') != 1 for t in manifest['tasks']):
        raise ValueError('Manifest dummy dual-color rule differs from experiment decision')
    return manifest['tasks']
