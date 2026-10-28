"""Explicit recovery of a saved no-primary round after its engine build raised.

Use the pinned original build/verification/checkpoint suffix verbatim. Never
reconstruct model replies, mark a build passed, or skip the original checkpoint.
"""
from __future__ import annotations

import ast
import copy
import hashlib
import inspect
import json
from pathlib import Path
import time


def original_suffix(loop):
    source = Path(loop.__file__).read_text(encoding='utf8')
    node = next(n for n in ast.parse(source).body
                if isinstance(n, ast.FunctionDef) and n.name == 'one_round')
    body = ast.get_source_segment(source, node)
    marker = '    b = build_tree(g.work)\n'
    if body.count(marker) != 1:
        raise RuntimeError('Pinned original build suffix is ambiguous')
    suffix = body[body.index(marker):]
    header = '''def _unity_original_build_suffix(g, r, rec):
    rdir = g.dir / f'round_{r:02d}'
    direction = rec['direction']
    script = (rec.get('frozen') or {}).get('path')
    sel = rec['selection']
    gr = rec['issues']
    art = rec.get('art')
'''
    compiled = {}
    exec(compile(header + suffix + '\n', str(loop.__file__) + ':recovered-original-build-suffix', 'exec'),
         loop.__dict__, compiled)
    return compiled['_unity_original_build_suffix'], suffix


def install(loop, args):
    number = args.resume_build_suffix
    if number is None:
        return
    recover, suffix = original_suffix(loop)

    def resume(g):
        if not g.rounds or g.rounds[-1].get('round') != number:
            raise RuntimeError('Recovery must target the last saved round')
        previous = g.rounds[-1]
        if (not previous.get('crash') or 'Editor capture bridge did not respond' not in previous['crash']
                or 'build_ok' in previous or not previous.get('repair_report', {}).get('ok')
                or previous.get('selection', {}).get('primary_target') is not None):
            raise RuntimeError('Recovery supports only a saved no-primary round with an engine bridge build exception')
        out = args.out / 'build_suffix_recovery' / f'round_{number:02d}' / str(time.time_ns())
        out.mkdir(parents=True, exist_ok=False)
        (out / 'original_round.json').write_text(json.dumps(previous, ensure_ascii=False, indent=2), encoding='utf8')
        (out / 'original_suffix.py.txt').write_text(suffix, encoding='utf8')
        from run_unity_experiment import snapshot
        before_source = out / 'source_before.json'
        snapshot(args.project, before_source, args.out / 'source_blobs', args.hash_cache)
        current = copy.deepcopy(previous)
        current.pop('crash')
        current['build_suffix_recovery'] = {
            'failed_record': str(out / 'original_round.json'),
            'verbatim_suffix': str(out / 'original_suffix.py.txt'),
            'suffix_sha256': hashlib.sha256(suffix.encode()).hexdigest(),
            'same_model_work_reused': True,
            'reason': 'Resume at original failed engine build; completed plan/probe/readers/editor are preserved.',
            'finished': False,
        }
        g.rounds[-1] = current
        try:
            recover(g, number, current)
            if not current.get('build_ok') or current.get('checkpoint_error'):
                raise RuntimeError('Recovered original build/checkpoint did not pass; do not advance')
            after_source = out / 'source_after.json'
            snapshot(args.project, after_source, args.out / 'source_blobs', args.hash_cache)
            before_files = json.loads(before_source.read_text(encoding='utf8'))['files']
            after_files = json.loads(after_source.read_text(encoding='utf8'))['files']
            changed_assets = [p for p in before_files.keys() | after_files.keys()
                              if p.startswith('Assets/') and before_files.get(p) != after_files.get(p)]
            current['build_suffix_recovery']['source_before'] = str(before_source)
            current['build_suffix_recovery']['source_after'] = str(after_source)
            current['build_suffix_recovery']['assets_changed_during_recovery'] = changed_assets
            if changed_assets:
                raise RuntimeError('Assets changed during recovered build/checkpoint; preserve attempt without certifying it')
            current['build_suffix_recovery']['finished'] = True
            g.save()
        except BaseException:
            (out / 'incomplete_recovery.json').write_text(json.dumps(current, ensure_ascii=False, indent=2), encoding='utf8')
            g.rounds[-1] = previous
            raise

    source = inspect.getsource(loop.main)
    marker = '    g.total = a.rounds\n'
    if source.count(marker) != 1:
        raise RuntimeError('Pinned main recovery seam is ambiguous')
    loop.__dict__['_unity_resume_build_suffix'] = resume
    compiled = {}
    exec(compile(source.replace(marker, marker + '    _unity_resume_build_suffix(g)\n'),
                 str(loop.__file__) + ':explicit-build-suffix-recovery', 'exec'), loop.__dict__, compiled)
    loop.main = compiled['main']
