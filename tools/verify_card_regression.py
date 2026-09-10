"""Verify an exact adventure run and exact EditMode run without reusing old summaries."""
import argparse
import json
import xml.etree.ElementTree as ET
from pathlib import Path

import summarize_adventure_acceptance as adventure


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--scenes', type=Path, required=True)
    parser.add_argument('--tests', type=Path, required=True)
    args = parser.parse_args()
    adventure.BASE = args.scenes.resolve()
    summary = {'scenes': {}, 'operations': 0, 'assertions': 0, 'screenshots': 0}
    for name in adventure.NAMES:
        report, count = adventure.validate(name)
        row = {'operations': len(report['steps']) - 1, 'assertions': count,
               'screenshots': len(report['steps']), 'status': report['status'], 'startedAt': report['startedAt']}
        summary['scenes'][name] = row
        for key in ['operations', 'assertions', 'screenshots']:
            summary[key] += row[key]
    tests = json.loads((args.tests / 'editmode-summary.json').read_text(encoding='utf-8-sig'))
    xml = ET.parse(args.tests / 'editmode-results.xml').getroot()
    assert xml.attrib['result'] == 'Passed'
    assert tests['passed'] == int(xml.attrib['passed'])
    assert tests['failed'] == int(xml.attrib['failed']) == 0
    assert tests['skipped'] == int(xml.attrib['skipped']) == 0
    summary['editMode'] = tests
    summary['testStartedAt'] = xml.attrib['start-time']
    target = args.scenes / 'regression_summary.json'
    target.write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(summary, ensure_ascii=False))


if __name__ == '__main__':
    main()
