"""Verify exact unit run: original artwork/rules, independent expectations, actual pointer hits and captures."""
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
    parser.add_argument('--config',type=Path,default=ROOT/'AutomationConfigs/AllOriginalCards/units.json')
    args=parser.parse_args()
    run = args.output.resolve()
    report, request = read(run/'report.json'), read(run/'request.json')
    config = read(args.config)
    suite_path = ROOT/'Assets/Resources/CardVerification/unit_cases.json'
    suite = read(suite_path)['cases']
    if request.get('caseIds'):
        suite=[c for c in suite if c['id'] in request['caseIds']]
        assert len(suite)==len(set(request['caseIds']))==len(request['caseIds'])
    digest = hashlib.sha256(suite_path.read_text(encoding='utf-8').encode()).hexdigest()
    sources = {c['id']: c for f in ('army','army_2') for c in read(ROOT/f'resources/text_json/{f}.json')}
    cases = {c['id']: c for c in suite}
    def normalized(steps):
        return [dict(dict(waitAfterSeconds=-1,skipScreenshot=False,before=[],after=[]),**s) for s in steps]
    assert report['status']=='success', report['message']
    assert normalized(request['steps']) == normalized(config['steps'])
    assert len(report['steps']) == len(config['steps'])+1
    captures, assertions = 0, 0
    for i, result in enumerate(report['steps']):
        assert result['success'], result['message']
        if i:
            planned = config['steps'][i-1]
            assert result['label']==planned['label']
            assert result['hitObject']==planned['buttonPath'].split('/')[-1]
            assert result['clickX']>0 and result['clickY']>0
            assert len(result['assertions'])==len(planned.get('before',[]))+len(planned.get('after',[]))
            assert all(a['passed'] and a['actual']==a['expected'] for a in result['assertions'])
            assertions += len(result['assertions'])
            state = {v['key']:v['value'] for v in result['afterState']}
            assert state['suiteSha256']==digest
            assert state['ui:ruleFits']==state['ui:valuesFits']=='True', result['label']
            case = cases[state['caseId']]
            assert state['cardId']==case['cardId']
            expected_art=case['cardId']
            for completed in case['steps'][:int(state['step'])]:
                if completed['kind']=='recruit':
                    expected_art=next((v['value'] for v in completed['expected'] if v['key']=='art'),case['cardId'])
            assert state['art']==expected_art
            for ability in sources[expected_art]['abilities']:
                assert (ability if isinstance(ability,str) else ability['effect']) in state['ui:rule']
            if planned['buttonPath'].endswith('Btn_Execute'):
                assert '本步骤断言通过' in state['ui:result']
                assert '失败' not in state['ui:values']
                step = case['steps'][int(state['step'])-1]
                for check in step['expected']: assert state[check['key']]==check['value']
        screenshot = result.get('screenshotPath','')
        if screenshot:
            path = Path(screenshot); assert path.is_relative_to(run)
            with Image.open(path) as image:
                assert image.size==(1920,1080)
                assert max(ImageStat.Stat(image.convert('RGB')).stddev)>20
            captures+=1
    assert captures==1+sum(not s.get('skipScreenshot',False) for s in config['steps'])
    summary=dict(status='passed',unitDefinitions=len({c['cardId'] for c in suite}),cases=len(suite),operations=len(config['steps']),
                 assertions=assertions,screenshots=captures,suiteSha256=digest,startedAt=report['startedAt'],finishedAt=report['finishedAt'],
                 damageCases=sum(any(s['kind']=='suffer' for s in c['steps']) for c in suite),
                 scope='仅所选原部队列明步骤的来源/能力/费用/招募与承伤证据；英雄互斥、免费获取、其他未列明能力与完整冒险仍待验收')
    (run/'summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(summary,ensure_ascii=False))


if __name__=='__main__': main()
