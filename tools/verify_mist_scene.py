"""Verify magic_014 source, real pointer actions, independent states and readable captures.

This verifies the represented cases; it never promotes a partial card to complete.
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
    args = parser.parse_args()
    folder = args.output.resolve() / 'magic'
    suite_file = ROOT / 'Assets/Resources/CardVerification/cases.json'
    planned = {c['id']: c for c in read(suite_file)['cases'] if c['cardId'] == 'magic_014'}
    selected = read(folder/'request.json').get('caseIds') or []
    if selected:
        planned = {k:v for k,v in planned.items() if k in selected}
    assert planned, 'No mist cases selected'
    effects, report = read(folder / 'effects.json'), read(folder / 'report.json')
    assert report['status'] == 'success'
    assert effects['suiteSha256'] == hashlib.sha256(suite_file.read_bytes()).hexdigest()
    cases = {c['id']: c for c in effects['cases'] if c['cardId'] == 'magic_014'}
    assert cases.keys() == planned.keys()
    card = next(c for c in read(ROOT / 'resources/text_json/magic.json') if c['id'] == 'magic_014')
    source = next(c for c in read(ROOT / 'AutomationOutputs/AllOriginalCards/20260910/source_inventory.json') if c['id'] == 'magic_014')
    art = ROOT / 'Assets/GameData/cards/magic_014.png'
    assert hashlib.sha256(art.read_bytes()).hexdigest() == source['originalSha256']
    operations = assertions = screenshots = 0
    for case_id, case in cases.items():
        plan = planned[case_id]
        assert all(c['passed'] for c in case['checks']), case_id
        assert case['status'] == ('partial' if plan['limitations'] else 'passed')
        assert case['printedEffect'] == card['spell_bottom' if plan['enhanced'] else 'spell_top']['effect']
        checks = {c['key']: c for c in case['checks']}
        for key, value in mapping(plan['expected']).items():
            assert checks[key]['expected'] == value == checks[key]['actual'], (case_id, key)
        assertions += len(case['checks'])
        steps = [s for s in report['steps'] if s['label'].startswith(case_id + '_')]
        assert steps and steps[-1]['label'] == case_id + '_execute'
        for step in steps:
            assert step['success'] and step['hitObject'] == step['buttonPath'].split('/')[-1]
            assert all(a['passed'] for a in step['assertions'])
            state = mapping(step['afterState'])
            assert state['caseId'] == case_id and state['art'] == 'magic_014'
            assert state['ui:textFits'] == 'True', step['label']
            operations += 1
            if step['screenshotPath']:
                with Image.open(step['screenshotPath']) as image:
                    assert image.size == (1920, 1080)
                    assert max(ImageStat.Stat(image.convert('RGB')).stddev) > 10
                screenshots += 1
        if plan.get('operations'):
            assert int(mapping(steps[-1]['afterState'])['operationCount']) == len(plan['operations'])
        if plan.get('unitIds'):
            assert plan['unitIds'] == ['army_2_006', 'army_2_001']
            state = mapping(steps[-1]['afterState'])
            assert '原版部队' in state['ui:fixture']
            for i in range(len(plan['operations'])):
                assert f'步骤{i + 1}：' in state['ui:comparison']
    result = dict(status='verified_cases_only', caseCount=len(cases),
                  caseResults=dict(Counter(c['status'] for c in cases.values())),
                  pointerOperations=operations, effectAssertions=assertions,
                  screenshots=screenshots, originalArtSha256=source['originalSha256'],
                  suiteSha256=effects['suiteSha256'],
                  remaining='Full card-zone lifecycle, unsafe-hex forced retreat and other card combinations remain unverified.')
    (args.output / 'mist_summary.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
