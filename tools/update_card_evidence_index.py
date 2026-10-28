"""Incremental historical scene evidence; never promotes partial to full acceptance."""
import hashlib
import json
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TARGET = ROOT / 'vibe_coding/codex/card_scene_evidence_index.json'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def main():
    baseline = read(ROOT / 'vibe_coding/codex/card_verification_history_index_2026-09-15.json')
    state = read(TARGET) if TARGET.exists() else {'runs': {}, 'cases': {}}
    candidates = {ROOT / 'AutomationOutputs/AllOriginalCards' / r['run'] / r['batch'] for r in baseline['runs']}
    for run in (ROOT / 'AutomationOutputs/AllOriginalCards').iterdir():
        if run.is_dir() and run.name >= '20260915_':
            candidates.update(run / batch for batch in ('basic_card', 'advanced_card', 'magic', 'items'))
    for folder in sorted(candidates):
        key = folder.relative_to(ROOT).as_posix()
        if key in state['runs'] or not (folder / 'effects.json').is_file() or not (folder / 'report.json').is_file():
            continue
        effects = read(folder / 'effects.json')
        ui = read(folder / 'report.json')
        if ui['status'] != 'success' or any(not s['success'] or any(not a['passed'] for a in s.get('assertions', [])) for s in ui['steps']):
            continue
        ids = [c['id'] for c in effects['cases']]
        assert len(ids) == len(set(ids)), key
        request = read(folder / 'request.json')
        if request.get('caseIds'):
            assert set(ids) == set(request['caseIds']), key
        state['runs'][key] = {'finishedAt': effects['finishedAt'], 'caseCount': len(ids),
            'sha256': hashlib.sha256((folder / 'effects.json').read_bytes()).hexdigest()}
        for case in effects['cases']:
            failed = any(not c['passed'] for c in case['checks']) or (case.get('exception') or '') != (case.get('expectedException') or '')
            assert (case['status'] == 'failed') == failed, case['id']
            previous = state['cases'].get(case['id'])
            if previous and previous['finishedAt'] >= effects['finishedAt']:
                continue
            state['cases'][case['id']] = {'cardId': case['cardId'], 'status': case['status'],
                'run': key, 'finishedAt': effects['finishedAt']}
    suite = read(ROOT / 'Assets/Resources/CardVerification/cases.json')['cases']
    playable = {c['id'] for c in baseline['cards'] if c['category'] == 'playable_face'}
    state['scope'] = 'Historical results by latest completed scene run, not current-code recertification or full-card acceptance.'
    state['plannedCases'] = len(suite)
    state['historicalResults'] = dict(Counter(c['status'] for c in state['cases'].values()))
    state['missingCases'] = [c['id'] for c in suite if c['id'] not in state['cases']]
    state['validCardFailures'] = [key for key,c in state['cases'].items() if c['cardId'] in playable and c['status'] == 'failed']
    state['nonplayableFailures'] = [key for key,c in state['cases'].items() if c['cardId'] not in playable and c['status'] == 'failed']
    TARGET.write_text(json.dumps(state, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps({k:state[k] for k in ('plannedCases','historicalResults','missingCases','validCardFailures','nonplayableFailures')}, ensure_ascii=False))


if __name__ == '__main__':
    main()
