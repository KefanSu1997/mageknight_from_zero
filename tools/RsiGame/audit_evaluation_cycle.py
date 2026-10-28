"""Audit this iteration's declared scope, without substituting UI ratings."""
import argparse
import csv
import hashlib
from html.parser import HTMLParser
import json
from pathlib import Path
from urllib.parse import unquote
from persist_retrospective_review import evidence_path


def read(path):
    return json.loads(path.read_text(encoding='utf8'))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


class Links(HTMLParser):
    def __init__(self):
        super().__init__(); self.paths=[]
    def handle_starttag(self, tag, attrs):
        for key, value in attrs:
            if key in ('src','href') and value and not value.startswith(('http:','https:','#')):
                self.paths.append(unquote(value))


def audit(root):
    errors=[]; checks={}
    for mode in ['before','after_v2']:
        manifest=read(root/mode/'manifest.json')
        for capture in manifest['captures']:
            if sha(Path(capture['screenshot'])) != capture['sha256']:
                errors.append('Captured image changed: '+capture['label'])
        checks[mode+'_captures']=len(manifest['captures'])
        if len(manifest['captures'])!=17 or not manifest['passed']:
            errors.append('Incomplete seventeen-context capture: '+mode)
        intent=read(root/mode/'source_intent.json')
        if any(x['differences'] for x in intent['assertions']) or len(intent['assertions'])!=2:
            errors.append('Source or consumer differs from actual archived rules: '+mode)
        receipt=read(root/mode/'compile_receipt.json')
        if not receipt.get('success') or len(receipt['observations'])<2 or any(x['actualConsoleErrorCount']!=0 for x in receipt['observations']):
            errors.append('Missing two real zero-error Console observations: '+mode)
    regression=read(root/'anti_regression_checks.json')
    if not regression['all17_native_states_identical'] or regression['font_decreases'] or regression['common_text_rect_changes']:
        errors.append('Same-fixture regression gate failed')
    if not all(x['canvas_unchanged_1920x1080'] for x in regression['canvas_checks']):
        errors.append('Canvas changed')
    interaction=read(root/'after_v2/interaction_checks/validation.json')
    if not interaction['passed'] or not interaction['selection_preserved'] or not interaction['auto_return_verified']:
        errors.append('Selection/auto-return failed')
    repeat=interaction['repeat_draw']
    if not (repeat.get('native_state_unchanged') or repeat.get('prevented_by_disabled_control')):
        errors.append('Double source use not safely rejected/prevented')
    restored=read(root/'normal_restore.json')
    if not restored['actual_fresh_hero_selection'] or not restored['original_opening_completed'] or any(s['actualConsoleErrorCount'] for s in restored['statuses']):
        errors.append('Normal opening restoration failed')
    for rel, entry in read(root/'after_v2/source_manifest.json').items():
        if sha(Path(entry['backup']))!=entry['sha256']:
            errors.append('Saved candidate source/asset changed: '+rel)
        if rel.startswith('Assets/') and sha(Path(__file__).resolve().parents[2]/rel)!=entry['sha256']:
            errors.append('Current candidate source differs from tested snapshot: '+rel)
    reviewer=root/read(root/'active_review.json')['directory']
    frozen=read(reviewer/'run_manifest.json')
    if sha(reviewer/'input.json')!=frozen['input_sha256'] or sha(reviewer/'system_prompt.txt')!=frozen['sp_sha256'] or sha(reviewer/'protocol.json')!=frozen['protocol_sha256']:
        errors.append('Frozen review input/SP/protocol changed')
    packet=read(reviewer/'input.json')
    for item in packet['required_images']+packet['required_text']:
        if sha(Path(item['path']))!=item['sha256']:errors.append('Reviewer evidence changed: '+item['path'])
    proofs=[p for p in reviewer.glob('attempt_*/verification.json') if read(p).get('verified')]
    if len(proofs)!=1:
        errors.append('Independent32-item review not yet uniquely verified')
    else:
        proof=read(proofs[0]); result=read(proofs[0].with_name('review.json'))
        if sha(proofs[0].with_name('review.json'))!=proof['actual_final_sha256'] or proof['byte_verified_images']!=proof['required_unique_images'] or proof['model']!=['gpt-6.1-sol']:
            errors.append('Independent final/model/image proof mismatch')
        if [x['id'] for x in result['items']]!=[f'{g}.{i}' for g in range(1,9) for i in range(1,5)]:
            errors.append('Independent review not exactly32 ordered items')
        for item in result['items']:
            for path in item.get('evidence',[]):
                if not evidence_path(path,result).is_file():errors.append('Referenced item evidence missing: '+path)
        checks['review_verdict']=result['gate_verdict']
        if result.get('hard_failures'):
            errors.append('Independent review reports hard UI failures; candidate needs further iteration')
    reader=read(root/'controlled_reader/verification.json')
    checks['controlled_model_reading']=dict(verified=reader['verified'],correct=reader['correct'],denominator=reader['denominator'],scope=reader['scope'])
    if not reader['verified']:errors.append('Controlled reader final unverified')
    parser=Links();parser.feed((root/'index.html').read_text(encoding='utf8'))
    for link in parser.paths:
        if not (root/link).is_file():errors.append('Report file link missing: '+link)
    checks['static_report_links']=len(parser.paths)
    with (root/'scores.csv').open(encoding='utf-8-sig',newline='') as stream:rows=list(csv.DictReader(stream))
    if len(rows)!=32 or any(x['status']=='pending_review' for x in rows):errors.append('CSV has pending/missing decisions')
    if len(proofs)==1:
        for row in rows:
            item=next((x for x in result['items'] if x['id']==row['id']),None)
            if not item or row['score']!=('' if item['score'] is None else str(item['score'])) or row['reason']!=item['reason'] or row['status']!=item['status']:
                errors.append('CSV does not faithfully preserve independent final: '+row['id'])
    result=dict(complete=not errors,errors=errors,checks=checks,
        scope='Declared fixed tasks and observed candidate UI only; does not certify all game rules, human experience or all32 numeric measures.')
    (root/'completion_audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
    return result


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--root',type=Path,required=True)
    result=audit(parser.parse_args().root.resolve());print(json.dumps(result,ensure_ascii=False));raise SystemExit(not result['complete'])
