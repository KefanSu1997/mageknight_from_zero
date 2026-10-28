"""Audit the requested twelve independent reviews, not the game's UI quality."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
from persist_retrospective_review import evidence_path


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def audit(root):
    errors, rounds = [], []
    manifest = json.loads((root / 'run_manifest.json').read_text(encoding='utf8'))
    if sha(Path(manifest['sp_path'])) != manifest['sp_sha256']:
        errors.append('Frozen SP differs from manifest')
    if sha(root / 'protocol.json') != manifest['protocol_sha256']:
        errors.append('Frozen protocol differs from manifest')
    ids = [f'{g}.{i}' for g in range(1, 9) for i in range(1, 5)]
    for entry in manifest['inputs']:
        n = entry['round']
        p = Path(entry['path'])
        issues = []
        if sha(p) != entry['sha256']:
            issues.append('Input packet changed')
        packet = json.loads(p.read_text(encoding='utf8'))
        for ev in packet['required_images'] + packet['required_text']:
            q = Path(ev['path'])
            if not q.is_file() or sha(q) != ev['sha256']:
                issues.append('Required evidence missing or changed: ' + str(q))
        accepted = []
        for proof_path in p.parent.glob('*/verification.json'):
            proof = json.loads(proof_path.read_text(encoding='utf8'))
            if proof.get('verified'):
                accepted.append((proof_path, proof))
        if len(accepted) != 1:
            issues.append(f'Expected one verified final, got {len(accepted)}')
        else:
            proof_path, proof = accepted[0]
            final_path = proof_path.with_name('review.json')
            raw_path = proof_path.with_name('actual_final.txt')
            if not final_path.is_file() or sha(final_path) != proof['actual_final_sha256']:
                issues.append('Review file differs from actual final')
            if not raw_path.is_file() or sha(raw_path) != proof['actual_final_sha256']:
                issues.append('Raw final file differs from actual final')
            if proof.get('model') != ['gpt-6.1-sol']:
                issues.append('Incorrect actual reviewer model')
            if proof.get('input_sha256') != entry['sha256']:
                issues.append('Proof not bound to frozen packet')
            data = json.loads(final_path.read_text(encoding='utf8'))
            if data.get('input_status') != 'valid' or data.get('round') != n:
                issues.append('Invalid input/round declaration')
            if [i.get('id') for i in data.get('items', [])] != ids:
                issues.append('Not exactly32 ordered items')
            for it in data.get('items', []):
                if not it.get('reason') or not isinstance(it.get('evidence'), list):
                    issues.append('Missing reason/evidence declaration: ' + str(it.get('id')))
                if it.get('status') == 'unverified' and it.get('score') is not None:
                    issues.append('Unknown item substituted with a score')
                for ev in it.get('evidence', []):
                    if not evidence_path(ev, data).is_file():
                        issues.append('Referenced evidence file missing: ' + ev)
            if data.get('hard_failures') and data.get('gate_verdict') != 'failed':
                issues.append('Hard failure hidden by verdict')
            if not Path(proof['transcript']).is_file():
                issues.append('Actual reviewer trace missing')
            else:
                finals, models = [], set()
                for line in Path(proof['transcript']).open(encoding='utf8'):
                    record = json.loads(line)
                    item = record.get('payload', {})
                    if record.get('type') == 'turn_context':
                        models.add(item.get('model'))
                    if item.get('type') == 'message' and item.get('role') == 'assistant' and item.get('phase') in ('final', 'final_answer'):
                        raw = ''.join(b.get('text', '') for b in item.get('content', []) if b.get('type') == 'output_text')
                        finals.append(hashlib.sha256(raw.encode('utf8')).hexdigest())
                if proof['actual_final_sha256'] not in finals or models != {'gpt-6.1-sol'}:
                    issues.append('Actual trace does not prove this final/model')
            if n == 9:
                required = packet['required_images']
                if not any(x['role'] == 'corrected' for x in required):
                    issues.append('R9 corrective images not supplied')
                if not any('post_gate_fix' not in x['path'] and x['role'] == 'candidate' for x in required):
                    issues.append('R9 initial failure images not supplied')
        rounds.append(dict(round=n, verified=not issues, errors=issues))
        errors.extend(f'R{n}: {s}' for s in issues)
    csv_path = root / 'scores.csv'
    if not csv_path.is_file():
        errors.append('Complete CSV missing')
    else:
        with csv_path.open(encoding='utf-8-sig', newline='') as f:
            rows = list(csv.DictReader(f))
        if len(rows) != 384 or any(row['status'] == 'pending_review' for row in rows):
            errors.append('CSV not384 completed item decisions')
    progress = json.loads((root / 'verified_progress.json').read_text(encoding='utf8'))
    if progress.get('verified_reviews') != 12 or progress.get('reviewed_items') != 384:
        errors.append('Final report has fewer than12x32 decisions')
    for name in ['system_prompt.txt', 'protocol.json', 'README.md', 'index.html', 'results.json', 'scores.csv']:
        if not (root / name).is_file():
            errors.append('Required deliverable missing: ' + name)
    result = dict(objective='完成12轮的独立评分', complete=not errors,
        scope='Completeness and authenticity of independent reviews; failed UI gates are legitimate outcomes, not incomplete reviews.',
        expected_rounds=12, expected_items=384, rounds=rounds, errors=errors,
        original_scores_untouched=True, no_main_agent_score_substitution=True)
    (root / 'completion_audit.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf8')
    return result


if __name__ == '__main__':
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--root', type=Path, required=True)
    args = ap.parse_args()
    result = audit(args.root.resolve())
    print(json.dumps(dict(complete=result['complete'], verified_rounds=sum(x['verified'] for x in result['rounds']),
                         errors=result['errors'][:15]), ensure_ascii=False))
