"""Persist an independent review's actual final, checking inputs and trace.

Never creates, edits, repairs, averages, or substitutes model ratings.
"""
import argparse
import base64
import hashlib
import json
from pathlib import Path
from verify_reader_delivery import text_parts, complete_prompt_coverage


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def evidence_path(value, review):
    """Resolve a reviewer-declared path without rewriting its raw answer."""
    path = Path(value)
    if path.is_absolute():
        return path.resolve()
    bases = [Path(review[k]) for k in ('evidence_path_root', 'path_base') if review.get(k)]
    if len({p.resolve() for p in bases}) > 1:
        raise ValueError('Conflicting explicit evidence roots')
    base = bases[0] if bases else Path.cwd()
    if not base.is_absolute():
        raise ValueError('evidence_path_root must be absolute')
    return (base / path).resolve()


def delivered(text, outputs):
    """Full original text or complete ordered lines/fixed 12k or 24k slices.

    Delegate to the existing reader verifier's strict implementation so every
    character of every fixed-size slice, including its final tail, is required.
    """
    try:
        complete_prompt_coverage(text.replace('\r\n', '\n'), list(enumerate(outputs)))
    except ValueError:
        return False
    return True


def persist(packet_path, transcript, worker, attempt):
    packet = json.loads(packet_path.read_text(encoding='utf8'))
    dest = packet_path.parent / attempt
    if dest.exists():
        raise FileExistsError(dest)
    models, finals, outputs, rendered, view_scripts = set(), [], [], set(), []
    image_count = 0
    for line in transcript.read_bytes().splitlines():
        rec = json.loads(line)
        item = rec.get('payload', {})
        kind = item.get('type')
        if rec.get('type') == 'turn_context':
            models.add(item.get('model'))
        if kind == 'message' and item.get('role') == 'assistant' and item.get('phase') in ('final', 'final_answer'):
            finals.append(''.join(x.get('text', '') for x in item.get('content', []) if x.get('type') == 'output_text'))
        if kind in ('custom_tool_call', 'function_call'):
            script = item.get('input', item.get('arguments', ''))
            if 'view_image' in script:
                view_scripts.append(script)
        if kind in ('custom_tool_call_output', 'function_call_output'):
            result = item.get('output', '')
            outputs.append(text_parts(result).replace('\r\n', '\n'))
            if isinstance(result, list):
                for block in result:
                    if isinstance(block, dict) and block.get('type') == 'input_image':
                        image_count += 1
                        url = block.get('image_url', '')
                        if isinstance(url, str) and url.startswith('data:') and ';base64,' in url:
                            rendered.add(hashlib.sha256(base64.b64decode(url.split(',', 1)[1])).hexdigest())
    if not finals:
        raise ValueError('No actual final; do not consume drafts or synthesize scores')
    dest.mkdir()
    raw = finals[-1]
    (dest / 'actual_final.txt').write_bytes(raw.encode('utf8'))
    errors = []
    if models != {'gpt-6.1-sol'}:
        errors.append(f'Actual models differ: {models}')
    expected_text = [packet_path, Path(packet['sp_path'])] + [Path(x['path']) for x in packet['required_text']]
    for p in expected_text:
        if not delivered(p.read_text(encoding='utf-8-sig'), outputs):
            errors.append(f'Complete text delivery unverified: {p}')
    if sha(Path(packet['sp_path'])) != packet['sp_sha256']:
        errors.append('SP changed')
    unique = {}
    for entry in packet['required_images']:
        unique.setdefault(entry['sha256'], []).append(Path(entry['path']))
    scripts = '\n'.join(view_scripts)
    for expected, aliases in unique.items():
        if any(sha(p) != expected for p in aliases):
            errors.append('Evidence image changed: ' + str(aliases[0]))
        viewed = any(any(v in scripts for v in (str(p), p.as_posix(), str(p).replace('\\', '\\\\'))) for p in aliases)
        if expected not in rendered and not viewed:
            errors.append('Actual image delivery unverified: ' + str(aliases[0]))
    for entry in packet['required_text']:
        if sha(Path(entry['path'])) != entry['sha256']:
            errors.append('Text evidence changed: ' + entry['path'])
    if image_count < len(unique):
        errors.append('Actual image blocks fewer than required unique images')
    data = None
    try:
        data = json.loads(raw)
    except ValueError:
        errors.append('Actual final is not original requested JSON; preserved, not repaired')
    if isinstance(data, dict):
        expected_ids = [f'{g}.{i}' for g in range(1, 9) for i in range(1, 5)]
        items = data.get('items', [])
        if [x.get('id') for x in items] != expected_ids:
            errors.append('Missing/duplicate/out-of-order32 item IDs')
        if data.get('round') != packet['round']:
            errors.append('Round mismatch')
        if data.get('sp_sha256') != packet['sp_sha256'] or data.get('input_sha256') != sha(packet_path):
            errors.append('Reported input/SP hashes mismatch')
        for it in items:
            score = it.get('score')
            if score is not None and (type(score) is not int or not 1 <= score <= 5):
                errors.append('Invalid score type/range: ' + str(it.get('id')))
            if it.get('status') == 'unverified' and score is not None:
                errors.append('Unverified item has numeric score: ' + str(it.get('id')))
            if not it.get('reason'):
                errors.append('Missing reason: ' + str(it.get('id')))
        actual_paths = {evidence_path(p, data) for p in data.get('reviewed_images', [])}
        if any(Path(x['path']).resolve() not in actual_paths for x in packet['required_images']):
            errors.append('Final omits a required reviewed image')
    proof = dict(worker=worker, model=sorted(models), transcript=str(transcript),
        transcript_sha256=sha(transcript), input_sha256=sha(packet_path),
        actual_final_sha256=hashlib.sha256(raw.encode('utf8')).hexdigest(),
        actual_image_blocks=image_count, required_unique_images=len(unique),
        byte_verified_images=sum(x in rendered for x in unique),
        verified=not errors, errors=errors,
        scope='Input/model/readiness/schema checks only; root does not edit or certify score accuracy')
    (dest / 'verification.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2), encoding='utf8')
    if not errors:
        # Preserve exact original text, including formatting.
        (dest / 'review.json').write_bytes(raw.encode('utf8'))
    return proof


if __name__ == '__main__':
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--input', type=Path, required=True)
    ap.add_argument('--transcript', type=Path, required=True)
    ap.add_argument('--worker', required=True)
    ap.add_argument('--attempt', required=True)
    args = ap.parse_args()
    if not args.attempt.replace('_', '').isalnum():
        raise ValueError('Invalid attempt name')
    print(json.dumps(persist(args.input.resolve(), args.transcript.resolve(), args.worker, args.attempt), ensure_ascii=False))
