"""Audit original magic_011 paid casts, actual card zones and downstream attacks.

Passing these cases does not certify the complete card-zone lifecycle.
"""
import argparse
import hashlib
import json
from collections import Counter
from pathlib import Path
from PIL import Image, ImageStat

ROOT = Path(__file__).resolve().parents[1]


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def mapping(values):
    return {v['key']: v['value'] for v in values}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    run = parser.parse_args().output.resolve()
    suite_file = ROOT / 'Assets/Resources/CardVerification/cases.json'
    plans = {c['id']: c for c in read(suite_file)['cases'] if c['cardId'] == 'magic_011'}
    selected = read(run/'magic/request.json').get('caseIds') or []
    if selected:
        plans = {k:v for k,v in plans.items() if k in selected}
    assert plans, 'No sacrifice cases selected'
    effects, report = read(run/'magic/effects.json'), read(run/'magic/report.json')
    assert effects['suiteSha256'] == hashlib.sha256(suite_file.read_bytes()).hexdigest()
    assert report['status'] == 'success'
    cases = {c['id']: c for c in effects['cases'] if c['cardId'] == 'magic_011'}
    assert plans.keys() == cases.keys()
    card = next(c for c in read(ROOT/'resources/text_json/magic.json') if c['id'] == 'magic_011')
    source = next(c for c in read(ROOT/'AutomationOutputs/AllOriginalCards/20260910/source_inventory.json') if c['id'] == 'magic_011')
    assert hashlib.sha256((ROOT/'Assets/GameData/cards/magic_011.png').read_bytes()).hexdigest() == source['originalSha256']
    ops = assertions = shots = discard_cases = 0
    for case_id, case in cases.items():
        plan = plans[case_id]
        assert all(c['passed'] for c in case['checks']), case_id
        assert case['status'] == ('partial' if plan['limitations'] else 'passed')
        assert case['printedEffect'] == card['spell_bottom' if plan['enhanced'] else 'spell_top']['effect']
        checks = {c['key']: c for c in case['checks']}
        for key, value in mapping(plan['expected']).items():
            assert checks[key]['expected'] == value == checks[key]['actual'], (case_id, key)
        assertions += len(case['checks'])
        if 'discardIndices' in plan:
            discard_cases += 1
            before, after = mapping(case['before']), mapping(case['after'])
            if plan.get('expectedException'):
                for k in ['hand:ids','discard:ids'] + [k for k in before if k.startswith(('token:','crystal:'))]:
                    assert before[k] == after[k], (case_id, k)
            else:
                n=len(plan['discardIndices'])
                assert int(before['hand'])-int(after['hand']) == n == int(after['discard'])-int(before['discard'])
                assert after['CardsToDiscard:count'] == '0'
        steps = [s for s in report['steps'] if s['label'].startswith(case_id+'_')]
        assert steps[-1]['label'] == case_id+'_execute'
        for step in steps:
            assert step['success'] and step['hitObject'] == step['buttonPath'].split('/')[-1]
            assert all(a['passed'] for a in step['assertions'])
            state = mapping(step['afterState'])
            assert state['caseId'] == case_id and state['art'] == 'magic_011'
            assert state['ui:textFits'] == 'True', step['label']
            ops += 1
            if step['screenshotPath']:
                with Image.open(step['screenshotPath']) as image:
                    assert image.size == (1920,1080)
                    assert max(ImageStat.Stat(image.convert('RGB')).stddev) > 10
                shots += 1
    summary = dict(status='verified_cases_only',cases=len(cases),discardCases=discard_cases,
        caseResults=dict(Counter(c['status'] for c in cases.values())),operations=ops,effectAssertions=assertions,
        screenshots=shots,suiteSha256=effects['suiteSha256'],originalArtSha256=source['originalSha256'],
        remaining='Active played-card identity and full play-zone lifecycle remain pending; do not claim complete card certification.')
    (run/'sacrifice_summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(summary,ensure_ascii=False))


if __name__ == '__main__':
    main()
