"""Recover a real late Editor report without replaying completed reader roles.

The original timeout remains a failed attempt. Parse the actual completed final
with the pinned parser, then execute the pinned build/verification suffix again.
"""
from __future__ import annotations

import copy
import hashlib
import inspect
import json
from pathlib import Path
import time

from resume_build_suffix import original_suffix


def install(loop, args):
    number, call = args.resume_late_editor, args.late_editor_call
    if number is None:
        return
    recover, suffix = original_suffix(loop)

    def resume(g):
        if not g.rounds or g.rounds[-1].get('round') != number:
            raise RuntimeError('Late report recovery must target the last saved round')
        previous = g.rounds[-1]
        if (previous.get('selection', {}).get('primary_target') is not None
                or previous.get('repair_report', {}).get('ok')
                or 'TimeoutError' not in str(previous.get('repair', {}).get('error'))):
            raise RuntimeError('Expected original no-primary Editor queue timeout')
        folder = args.out / 'model_calls' / call
        folder.resolve().relative_to((args.out / 'model_calls').resolve())
        request = json.loads((folder / 'request.json').read_text(encoding='utf8'))
        response = json.loads((folder / 'response.json').read_text(encoding='utf8'))
        if (request.get('kind') != 'editor' or response.get('executed_model') != 'gpt-6.1-sol'
                or request['metadata']['original_repair_options']['tag'] != f'r{number:02d}'):
            raise RuntimeError('Late final belongs to another role or round')
        raw = response['raw']
        trace = Path(json.loads((folder / 'root_worker_trace.json').read_text())['actual_trace'])
        finals, models = [], []
        for line in trace.open(encoding='utf8'):
            item = json.loads(line)
            payload = item.get('payload', {})
            if item.get('type') == 'turn_context' and payload.get('model'):
                models.append(payload['model'])
            if (payload.get('type') == 'message' and payload.get('role') == 'assistant'
                    and payload.get('phase') in ('final', 'final_answer')):
                finals.append(''.join(v.get('text', '') for v in payload.get('content', [])
                                      if v.get('type') == 'output_text'))
        if not finals or finals[-1] != raw or set(models) != {'gpt-6.1-sol'}:
            raise RuntimeError('Late raw final or actual Editor model is not verified')
        parsed = loop.parse_report(raw)
        if not parsed.get('ok'):
            raise RuntimeError('Actual late final did not pass the original report parser')
        paths = parsed['report'].get('art', {}).get('files', [])
        if not paths:
            raise RuntimeError('No actual reported candidate files to bind')
        bound = {}
        for relative in paths:
            source = (args.project / relative).resolve()
            source.relative_to(args.project.resolve())
            bound[relative] = hashlib.sha256(source.read_bytes()).hexdigest()
        controller = 'Assets/Scripts/Battle/PveBattleController.cs'
        if bound.get(controller, '').upper() not in parsed['report'].get('note', ''):
            raise RuntimeError('Current controller does not match the actual final report')
        out = args.out / 'late_editor_recovery' / f'round_{number:02d}' / str(time.time_ns())
        out.mkdir(parents=True, exist_ok=False)
        (out / 'original_round.json').write_text(json.dumps(previous, ensure_ascii=False, indent=2), encoding='utf8')
        (out / 'actual_final.txt').write_text(raw, encoding='utf8')
        (out / 'original_suffix.py.txt').write_text(suffix, encoding='utf8')
        from run_unity_experiment import snapshot
        snapshot(args.project, out / 'source_before.json', args.out / 'source_blobs', args.hash_cache)
        current = copy.deepcopy(previous)
        current['repair_report'] = parsed
        current['late_editor_recovery'] = dict(
            original_failure=str(out / 'original_round.json'), call=call,
            actual_final_sha256=hashlib.sha256(raw.encode()).hexdigest(),
            actual_worker=response['worker_agent_id'], actual_model=models[-1],
            original_failed_repair_preserved=True, original_report_parser=True,
            original_build_suffix=str(out / 'original_suffix.py.txt'),
            reported_candidate_files=bound, finished=False,
            scope='Operator recovery after a queue timeout, not a timely original Editor success. '
                  'No reader, goal or Editor answer regenerated; original failure retained.')
        g.rounds[-1] = current
        try:
            recover(g, number, current)
            if not current.get('build_ok') or current.get('checkpoint_error'):
                raise RuntimeError('Original suffix build/verification did not finish')
            for relative, expected in bound.items():
                if hashlib.sha256((args.project / relative).read_bytes()).hexdigest() != expected:
                    raise RuntimeError(f'Reported candidate changed during recovery: {relative}')
            snapshot(args.project, out / 'source_after.json', args.out / 'source_blobs', args.hash_cache)
            current['late_editor_recovery']['finished'] = True
            g.save()
        except BaseException:
            (out / 'incomplete_recovery.json').write_text(json.dumps(current, ensure_ascii=False, indent=2), encoding='utf8')
            g.rounds[-1] = previous
            raise

    source = inspect.getsource(loop.main)
    marker = '    g.total = a.rounds\n'
    if source.count(marker) != 1:
        raise RuntimeError('Pinned main recovery seam is ambiguous')
    loop.__dict__['_unity_resume_late_editor'] = resume
    namespace = {}
    exec(compile(source.replace(marker, marker + '    _unity_resume_late_editor(g)\n'),
                 str(loop.__file__) + ':explicit-late-editor-recovery', 'exec'), loop.__dict__, namespace)
    loop.main = namespace['main']
