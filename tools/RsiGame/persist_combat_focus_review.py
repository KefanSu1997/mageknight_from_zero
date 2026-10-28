"""Persist actual v3 FINAL bytes; verify transport/schema, never grade the UI."""
import argparse
import base64
import hashlib
import json
from pathlib import Path

from jsonschema import Draft202012Validator
from verify_reader_delivery import complete_prompt_coverage, text_parts


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def persist(packet_path, transcript, worker, out):
    packet_path, transcript, out = map(Path, (packet_path, transcript, out))
    trace_raw = transcript.read_bytes()
    records = [json.loads(line) for line in trace_raw.splitlines() if line.strip()]
    finals, outputs, images, models, agents = [], [], set(), set(), set()
    last_final = max((n for n, rec in enumerate(records)
                      if rec.get('payload', {}).get('type') == 'message'
                      and rec.get('payload', {}).get('role') == 'assistant'
                      and rec.get('payload', {}).get('phase') in ('final', 'final_answer')), default=-1)
    for rec in records[:last_final + 1] if last_final >= 0 else records:
        payload = rec.get('payload', {})
        if rec.get('type') == 'session_meta':
            agents.add(payload.get('agent_path'))
        if rec.get('type') == 'turn_context':
            models.add(payload.get('model'))
        if (payload.get('type') == 'message' and payload.get('role') == 'assistant'
                and payload.get('phase') in ('final', 'final_answer')):
            finals.append(''.join(x.get('text', '') for x in payload.get('content', [])
                                  if x.get('type') == 'output_text'))
        if payload.get('type') in ('custom_tool_call_output', 'function_call_output'):
            result = payload.get('output', '')
            outputs.append(text_parts(result))
            if isinstance(result, list):
                for block in result:
                    if isinstance(block, dict) and block.get('type') == 'input_image':
                        url = block.get('image_url', '')
                        if isinstance(url, str) and url.startswith('data:image/png;base64,'):
                            try:
                                images.add(digest(base64.b64decode(url.split(',', 1)[1], validate=True)))
                            except ValueError:
                                pass
    if not finals:
        raise ValueError('No actual FINAL; drafts must not be persisted')
    if out.exists():
        raise FileExistsError(out)
    out.mkdir(parents=True)
    raw = finals[-1].encode('utf8')
    (out / 'actual_final.txt').write_bytes(raw)
    errors, frozen = [], {}
    if not worker.startswith('/') or '//' in worker or agents != {worker}:
        errors.append('session_meta agent_path does not match canonical worker')
    if models != {'gpt-6.1-sol'}:
        errors.append('Actual model must be gpt-6.1-sol')

    def read(path, expected=None, image=False):
        p = Path(path)
        if not p.is_absolute():
            p = packet_path.parent / p
        p = p.resolve()
        try:
            data = p.read_bytes()
            frozen[p] = data
            if expected is not None and digest(data) != expected:
                errors.append('Evidence SHA mismatch: ' + str(p))
            if image:
                if not data.startswith(b'\x89PNG\r\n\x1a\n') or digest(data) not in images:
                    errors.append('Original PNG byte delivery unverified: ' + str(p))
            else:
                try:
                    complete_prompt_coverage(data.decode('utf-8-sig'), list(enumerate(outputs)))
                except ValueError:
                    errors.append('Complete text delivery unverified: ' + str(p))
            return data
        except (OSError, UnicodeError) as exc:
            errors.append('Missing/unreadable evidence: ' + str(p) + ': ' + str(exc))
            return b''

    packet_raw = read(packet_path.resolve())
    packet = {}
    schema = None
    try:
        packet = json.loads(packet_raw)
        for name in ('sp', 'rubric', 'score_schema'):
            data = read(packet[name + '_path'], packet[name + '_sha256'])
            if name == 'score_schema':
                schema = json.loads(data)
        for kind in ('required_images', 'required_text'):
            for entry in packet[kind]:
                read(entry['path'], entry['sha256'], image=kind == 'required_images')
    except (KeyError, ValueError, TypeError) as exc:
        errors.append('Invalid frozen input: ' + str(exc))
    try:
        final = json.loads(raw)
        if schema is None:
            raise ValueError('Missing schema')
        Draft202012Validator.check_schema(schema)
        errors.extend('Schema: ' + e.message for e in Draft202012Validator(schema).iter_errors(final))
        errors.extend(semantic_errors(final, packet))
    except (ValueError, TypeError, KeyError, AttributeError) as exc:
        errors.append('Invalid actual FINAL: ' + str(exc))
    for path, data in frozen.items():
        try:
            if path.read_bytes() != data:
                errors.append('Evidence changed during verification: ' + str(path))
        except OSError:
            errors.append('Evidence disappeared during verification: ' + str(path))
    if transcript.read_bytes() != trace_raw:
        errors.append('Transcript changed during verification')
    proof = dict(verified=not errors, errors=errors, worker=worker, model=sorted(models, key=str),
                 input_sha256=digest(packet_raw), transcript_sha256=digest(trace_raw),
                 actual_final_sha256=digest(raw),
                 scope='Transport, evidence delivery and schema/semantic consistency only; not model judgment accuracy or UI acceptance')
    (out / 'verification.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2), encoding='utf8')
    if not errors:
        (out / 'review.json').write_bytes(raw)
    return proof


def semantic_errors(final, packet):
    errors = []
    ids = {f'{g}.{i}' for g in range(1, 9) for i in range(1, 5)}
    if final.get('rubric_version') != packet.get('rubric_version'):
        errors.append('Rubric version mismatch')
    if final.get('overall_numeric_score') is not None:
        errors.append('Overall numeric score forbidden')
    incomplete = False
    for side in ('A', 'B'):
        rows = final.get('sides', {}).get(side, [])
        if len(rows) != 32 or {x.get('id') for x in rows} != ids:
            errors.append('Missing/duplicate32 IDs: ' + side)
        for item in rows:
            missing = item.get('missing_reasons', [])
            status = item.get('status')
            if item.get('score') is not None and (not item.get('evidence') or not item.get('reason')
                    or not item.get('unit_results')
                    or not (item.get('raw_metrics') or item.get('visual_observations'))):
                errors.append('Numeric score lacks evidence/results: ' + side + '/' + str(item.get('id')))
            incomplete |= status in ('unverified', 'partial') or bool(missing and status != 'not_applicable')
            if item.get('score') is not None and (status in ('unverified', 'not_applicable')
                    or missing or item.get('denominator') is None
                    or (status == 'partial' and item.get('id') not in ('8.1', '8.2'))):
                errors.append('Missing evidence cannot have numeric score: ' + side + '/' + str(item.get('id')))
    expected = ('input_invalid' if final.get('input_status') == 'input_invalid' else
                'failed' if final.get('hard_failures') else
                'unverified' if final.get('unverified_gates') or incomplete else None)
    if expected is not None and final.get('gate_verdict') != expected:
        errors.append('Gate verdict inconsistent; expected ' + expected)
    if final.get('gate_verdict') == 'passed_observed_scope':
        if any(item.get('score') is None and item.get('status') != 'not_applicable'
               for rows in final.get('sides', {}).values() for item in rows):
            errors.append('Passed verdict contains unscored applicable items')
        ledger = final.get('read_ledger', [])
        required = [(packet.get(name + '_path'), packet.get(name + '_sha256'))
                    for name in ('sp', 'rubric', 'score_schema')]
        required += [(e['path'], e['sha256']) for kind in ('required_images', 'required_text') for e in packet.get(kind, [])]
        if any(not any(x.get('path') == path and x.get('sha256') == sha and x.get('actually_read_or_viewed') is True
                       for x in ledger) for path, sha in required):
            errors.append('Passed verdict lacks complete read ledger')
        if final.get('blind_review', {}).get('preserved') is not True:
            errors.append('Passed verdict requires preserved blind review')
    return errors


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input', type=Path, required=True)
    parser.add_argument('--transcript', type=Path, required=True)
    parser.add_argument('--worker', required=True)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    result = persist(args.input.resolve(), args.transcript.resolve(), args.worker, args.out.resolve())
    print(json.dumps(result, ensure_ascii=False))
    raise SystemExit(0 if result['verified'] else 1)
