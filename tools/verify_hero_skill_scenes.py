"""Verify a specific hero-skill scene run, including actual clicks, state, source text and captures."""
import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageStat

ROOT = Path(__file__).resolve().parents[1]


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    run = args.output.resolve()
    report = read(run / 'report.json')
    request = read(run / 'request.json')
    config = read(ROOT / 'AutomationConfigs/AllOriginalCards/hero_skills.json')
    suite_path = ROOT / 'Assets/Resources/CardVerification/skill_cases.json'
    suite = read(suite_path)['cases']
    digest = hashlib.sha256(suite_path.read_text(encoding='utf-8').encode()).hexdigest()
    skills = read(ROOT / 'resources/text_json/skill.json')[0]['skills']
    assert report['status'] == 'success', report['message']
    def normalized(steps):
        defaults = dict(waitAfterSeconds=-1, skipScreenshot=False, before=[], after=[])
        return [dict(defaults, **step) for step in steps]
    assert normalized(request['steps']) == normalized(config['steps']), 'Run configuration differs from current independent expectations'
    assert len(report['steps']) == len(config['steps']) + 1
    expected_by_label = {f"{case['id']}_{i:02}_{step['kind']}": (case, step) for case in suite for i, step in enumerate(case['steps'])}
    captures, assertions = 0, 0
    for i, result in enumerate(report['steps']):
        assert result['success'], result['message']
        if i:
            planned = config['steps'][i - 1]
            assert result['label'] == planned['label']
            assert result['hitObject'] == planned['buttonPath'].split('/')[-1]
            assert result['clickX'] > 0 and result['clickY'] > 0
            checks = result['assertions']
            assert len(checks) == len(planned.get('before', [])) + len(planned.get('after', []))
            assert all(c['passed'] and c['actual'] == c['expected'] for c in checks)
            assertions += len(checks)
            state = {v['key']: v['value'] for v in result['afterState']}
            assert state['suiteSha256'] == digest
            assert state['art'] == 'skill_000'
            if result['label'] in expected_by_label:
                case, action = expected_by_label[result['label']]
                assert state['ui:rule'] == skills[case['skillIndex']]['total_description']
                assert '本步骤断言通过' in state['ui:result']
                assert '失败' not in state['ui:values']
                for expected in action['expected']:
                    assert state[expected['key']] == expected['value']
        screenshot = result.get('screenshotPath', '')
        if screenshot:
            image_path = Path(screenshot)
            assert image_path.is_relative_to(run)
            with Image.open(image_path) as image:
                assert image.size == (1920, 1080)
                assert max(ImageStat.Stat(image.convert('RGB')).stddev) > 20, 'Blank screenshot'
            captures += 1
    assert captures == 1 + len(expected_by_label)
    summary = dict(status='passed', cases=len(suite), operations=len(config['steps']), assertions=assertions,
                   screenshots=captures, suiteSha256=digest, startedAt=report['startedAt'], finishedAt=report['finishedAt'],
                   scope='原表前五个技能的39个场景案例；不代表82条技能/32条部队已全部验证')
    (run / 'summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(summary, ensure_ascii=False))


if __name__ == '__main__':
    main()
