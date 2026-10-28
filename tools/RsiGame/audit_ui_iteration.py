"""Audit saved iteration evidence and independent gate; does not certify the whole goal."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
from persist_retrospective_review import evidence_path


def read(p):return json.loads(p.read_text(encoding='utf8'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()


def audit(root):
    errors=[];checks={};meta=read(root/'cycle.json');candidate=meta['candidate']
    for mode in ['before',candidate]:
        manifest=read(root/mode/'manifest.json');checks[mode+'_contexts']=len(manifest['captures'])
        if not manifest.get('passed') or len(manifest['captures'])!=17:errors.append('Incomplete context collection: '+mode)
        for row in manifest['captures']:
            if sha(Path(row['screenshot']))!=row['sha256']:errors.append('Screenshot changed: '+mode+'/'+row['label'])
        if any(x['differences'] for x in read(root/mode/'source_intent.json')['assertions']):errors.append('Formal source/consumer mismatch: '+mode)
        receipt=read(root/mode/'compile_receipt.json')
        if not receipt.get('success') or len(receipt.get('observations',[]))<2 or any(x.get('actualConsoleErrorCount')!=0 for x in receipt['observations']):errors.append('Two real zero-error compile observations missing: '+mode)
    regression=read(root/'anti_regression_checks.json')
    if not regression['all17_native_states_identical'] or not regression['canvas_unchanged'] or regression['font_decreases']:errors.append('Native/canvas/font regression')
    info=read(root/candidate/'required_information.json')['limited_required_role_checks'];checks['limited_role_checks']=info
    if info['numerator']!=info['denominator']:errors.append('Incomplete declared text roles')
    interaction=read(root/candidate/'interaction_checks/validation.json')
    if not interaction.get('passed') or not interaction['selection_preserved'] or not interaction['auto_return_verified']:errors.append('Source selection/return failed')
    recall=read(root/candidate/'recall_checks/validation.json')
    if not recall['passed'] or not recall['new_session_clears_history'] or not recall['result_retained_after_phase_change']:errors.append('Recall sequence failed')
    scroll=read(root/candidate/'scroll_checks/end_validation.json')
    if not scroll['bottom_reached'] or not scroll['native_state_unchanged'] or scroll['font_size']!=22:errors.append('Scroll evidence failed')
    for rel,row in read(root/candidate/'source_manifest.json').items():
        if sha(Path(row['backup']))!=row['sha256']:errors.append('Archived source changed: '+rel)
        if sha(Path(__file__).resolve().parents[2]/rel)!=row['sha256']:errors.append('Current version differs: '+rel)
    restored=read(root/'normal_restore.json')
    if not restored['actual_fresh_hero_selection'] or not restored['original_opening_completed'] or any(x['actualConsoleErrorCount'] for x in restored['statuses']):errors.append('Normal opening not restored')
    reader=read(root/'controlled_reader/verification.json');checks['controlled_reader']=dict(verified=reader['verified'],correct=reader['correct'],denominator=reader['denominator'])
    if not reader['verified']:errors.append('Reader proof invalid')
    review=root/read(root/'active_review.json')['directory'];frozen=read(review/'run_manifest.json');packet=read(review/'input.json')
    for name,key in [('input.json','input_sha256'),('protocol.json','protocol_sha256'),('system_prompt.txt','sp_sha256')]:
        if sha(review/name)!=frozen[key]:errors.append('Frozen review changed: '+name)
    for row in packet['required_images']+packet['required_text']:
        if sha(Path(row['path']))!=row['sha256']:errors.append('Evidence changed: '+row['path'])
    proofs=[p for p in review.glob('attempt_*/verification.json') if read(p).get('verified')]
    if len(proofs)!=1:errors.append('Independent final not uniquely verified')
    else:
        proof=read(proofs[0]);final=read(proofs[0].with_name('review.json'));checks['gate_verdict']=final['gate_verdict']
        if sha(proofs[0].with_name('review.json'))!=proof['actual_final_sha256'] or proof['model']!=['gpt-6.1-sol'] or proof['byte_verified_images']!=proof['required_unique_images']:errors.append('Independent proof mismatch')
        if final.get('hard_failures') or 'fail' in final['gate_verdict'].lower():errors.append('Independent reviewer identifies hard failure')
        for item in final['items']:
            for p in item.get('evidence',[]):
                if not evidence_path(p,final).is_file():errors.append('Missing review citation: '+p)
        with (root/'scores.csv').open(encoding='utf-8-sig',newline='') as stream:rows=list(csv.DictReader(stream))
        if len(rows)!=32:errors.append('CSV item count mismatch')
        for item,row in zip(final['items'],rows):
            if item['id']!=row['id'] or item['reason']!=row['reason'] or row['score']!=('' if item['score'] is None else str(item['score'])):errors.append('CSV changed actual rating: '+item['id'])
    result=dict(iteration_evidence_complete=not errors,errors=errors,checks=checks,
                scope='This fixed iteration only; not all30 rounds, human tests, all game rules or whole-goal completion.')
    (root/'completion_audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
    return result


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--root',type=Path,required=True)
    result=audit(parser.parse_args().root.resolve());print(json.dumps(result,ensure_ascii=False));raise SystemExit(0 if result['iteration_evidence_complete'] else 1)
