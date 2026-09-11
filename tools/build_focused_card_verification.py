"""Select affected original cards/cases without deleting the complete regression suite.

Example: python tools/build_focused_card_verification.py --cards magic_011
The Unity menu Run Focused Cases consumes the generated request. A focused run
certifies only its explicit caseIds, never the omitted regression cases.
"""
import argparse
import copy
import hashlib
import json
from pathlib import Path
from build_all_card_verification import write_json

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    choice = parser.add_mutually_exclusive_group(required=True)
    choice.add_argument('--cards', nargs='+')
    choice.add_argument('--cases', nargs='+')
    choice.add_argument('--failed-from', type=Path, help='Keep only failed cases from a complete, current scene run')
    args = parser.parse_args()
    suite = json.loads((ROOT/'Assets/Resources/CardVerification/cases.json').read_text(encoding='utf-8-sig'))['cases']
    if args.failed_from:
        effects = json.loads(args.failed_from.read_text(encoding='utf-8-sig'))
        folder = args.failed_from.parent
        report = json.loads((folder/'report.json').read_text(encoding='utf-8-sig'))
        prior = json.loads((folder/'request.json').read_text(encoding='utf-8-sig'))
        expected = prior.get('caseIds') or [c['id'] for c in suite if c['batch'] == effects['batch']]
        assert report['status'] == 'success', 'Do not prune an incomplete or failed UI run'
        assert {c['id'] for c in effects['cases']} == set(expected), 'Do not prune missing case results'
        assert len(effects['cases']) == len(expected) == len(set(expected)), 'Do not prune duplicate case results'
        for case in effects['cases']:
            failed = any(not c['passed'] for c in case['checks']) or (case.get('exception') or '') != (case.get('expectedException') or '')
            assert (case['status'] == 'failed') == failed, 'Do not prune incorrectly classified results'
        assert effects['suiteSha256'] == hashlib.sha256((ROOT/'Assets/Resources/CardVerification/cases.json').read_bytes()).hexdigest(), 'Stale suite: rerun affected cases before pruning'
        args.cases = [c['id'] for c in effects['cases'] if c['status'] == 'failed']
        if not args.cases:
            prior['caseIds'] = []; prior['steps'] = []
            write_json(ROOT/'AutomationConfigs/AllOriginalCards/focused.json', prior)
            print(json.dumps({'remainingCases':0,'passedChecksRemovedFromDailyQueue':len(expected),'regressionLibraryPreserved':True}))
            return
    requested = set(args.cards or args.cases)
    key = 'cardId' if args.cards else 'id'
    cases = [c for c in suite if c[key] in requested]
    assert {c[key] for c in cases} == requested, 'Unknown card/case IDs; refusing an incomplete selection'
    batches = {c['batch'] for c in cases}
    assert len(batches) == 1, 'Select one batch per focused scene run'
    batch = next(iter(batches))
    full = json.loads((ROOT/f'AutomationConfigs/AllOriginalCards/{batch}.json').read_text(encoding='utf-8-sig'))
    request = copy.deepcopy(full)
    request['caseIds'] = [c['id'] for c in cases]
    request['steps'] = []
    for index, case in enumerate(cases):
        steps = [copy.deepcopy(s) for s in full['steps'] if s['label'].startswith(case['id']+'_')]
        if index == 0:
            steps = [s for s in steps if s['label'] != case['id']+'_prepare']
        for step in steps:
            for assertion in step.get('after', []):
                if assertion['key'] == 'completed':
                    assertion['expected'] = str(index+1)
        request['steps'].extend(steps)
    assert len([s for s in request['steps'] if s['label'].endswith('_execute')]) == len(cases)
    write_json(ROOT/'AutomationConfigs/AllOriginalCards/focused.json', request)
    print(json.dumps({'batch':batch,'selectedCases':len(cases),'fullBatchCases':sum(c['batch']==batch for c in suite),
                      'operations':len(request['steps']),'caseIds':request['caseIds']},ensure_ascii=False))


if __name__ == '__main__':
    main()
