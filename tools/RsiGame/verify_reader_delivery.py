"""Verify original reader input delivery from a completed worker's own trace.

This does not grade or repair the answer, and never operates Unity.
"""
from __future__ import annotations

import argparse
import base64
import hashlib
import json
import re
from pathlib import Path


_TRUNCATED_FRAME = re.compile(r'\AWarning: truncated output \(original token count: [0-9]+\)\nTotal output lines: [0-9]+\n\n')
_EXEC_FRAME = re.compile(r'\AScript completed\nWall time [0-9]+(?:\.[0-9]+)? seconds\nOutput:\n')


def _exec_envelope(value):
    return (isinstance(value, dict) and isinstance(value.get('chunk_id'), str)
            and type(value.get('wall_time_seconds')) in (int, float)
            and type(value.get('exit_code')) is int
            and isinstance(value.get('output'), str))


def _framed_outputs(value):
    """Decode only whole JSON-line exec results after a recognized runtime header.

    Require the entire payload to consist of complete known envelopes. Never
    extract a JSON substring from arbitrary stdout or a partial/truncated frame.
    """
    match = _EXEC_FRAME.match(value) or _TRUNCATED_FRAME.match(value)
    # Runtime may return its header as a separate input_text block.
    # In that case the entire block must be complete exec-envelope JSON lines.
    payload = value[match.end():] if match else value
    rows = payload.strip().splitlines()
    if not rows:
        return []
    decoded = []
    for row in rows:
        try:
            item = json.loads(row)
        except ValueError:
            return []
        if not _exec_envelope(item):
            return []
        decoded.append(item['output'])
    return decoded


def text_parts(value):
    if isinstance(value, list):
        return '\n'.join(text_parts(v) for v in value)
    if isinstance(value, dict):
        if value.get('type') in ('input_text', 'text', 'output_text'):
            return text_parts(value.get('text', ''))
        return text_parts(value.get('output', ''))
    if isinstance(value, str):
        # functions.exec text(result) may wrap exec_command stdout in JSON.
        try:
            nested = json.loads(value)
        except ValueError:
            framed = _framed_outputs(value)
            return value + ''.join('\n' + text_parts(output) for output in framed)
        if isinstance(nested, (dict, list)):
            return value + '\n' + text_parts(nested)
        return value
    return ''


def _universal_newlines(value):
    # Python read_text(newline=None): CRCRLF becomes TWO LF, never one.
    return value.replace('\r\n', '\n').replace('\r', '\n')


def _slice_coverage(original, outputs, mode):
    joined = '\n'.join(output for _, output in outputs)
    for size in (12000, 24000):
        if len(original) <= size:
            continue
        cursor = 0
        for start in range(0, len(original), size):
            part = original[start:start + size]
            found = joined.find(part, cursor)
            if found < 0:
                break
            cursor = found + len(part)
        else:
            return [], (f'ordered_complete_{size}_character_slice_coverage' if mode == 'raw' and '\r' not in original
                        else f'ordered_complete_{size}_{mode}_character_slice_coverage')
    return None


def complete_prompt_coverage(original, outputs):
    """Require complete physical or Python-universal text, never tiny fragments.

    Raw .NET reads slice physical CR/CRLF bytes decoded as UTF8. Python readers
    slice universal-newline text. Test physical slices BEFORE conversion, then
    the explicitly defined universal representation, retaining every blank line.
    """
    matched = [n for n, output in outputs if original in output]
    if matched:
        return matched, 'exact_complete_text'
    result = _slice_coverage(original, outputs, 'raw')
    if result:
        return result
    canonical = _universal_newlines(original)
    normalized_outputs = [(n, _universal_newlines(output)) for n, output in outputs]
    matched = [n for n, output in normalized_outputs if canonical in output]
    if matched:
        return matched, 'exact_complete_universal_newline_text'
    # Keep the original ordered nonempty-line behavior for normal reader output.
    joined = '\n'.join(output for _, output in normalized_outputs)
    cursor = 0
    for line in canonical.splitlines():
        if not line:
            continue
        found = joined.find(line, cursor)
        if found < 0:
            break
        cursor = found + len(line)
    else:
        return [], 'ordered_complete_nonempty_line_coverage'
    result = _slice_coverage(canonical, normalized_outputs, 'universal_newline')
    if result:
        return result
    # Readers may split a minified JSON line at arbitrary character boundaries.
    # Require every character in order, using substantial exact spans; never
    # bridge a missing span just because a later section was delivered.
    cursor = 0
    for _, output in normalized_outputs:
        position = 0
        while cursor < len(canonical):
            anchor = canonical[cursor:cursor + 256]
            found = output.find(anchor, position)
            if found < 0:
                break
            count = len(anchor)
            while (cursor + count < len(canonical)
                   and found + count < len(output)
                   and canonical[cursor + count] == output[found + count]):
                count += 1
            cursor += count
            position = found + count
        if cursor == len(canonical):
            return [], 'ordered_complete_variable_character_slice_coverage'
    # Legacy Windows text-mode readers can slice the CRLF representation before
    # stdout translation. Preserve those exact substantial boundaries and tail.
    windows = canonical.replace('\n', '\r\n')
    for size in (12000, 24000):
        if len(windows) <= size:
            continue
        cursor = 0
        for start in range(0, len(windows), size):
            part = _universal_newlines(windows[start:start + size])
            found = joined.find(part, cursor)
            if found < 0:
                break
            cursor = found + len(part)
        else:
            return [], f'ordered_complete_{size}_character_slice_coverage'
    raise ValueError('Complete original messages.txt absent from actual tool output')


def verify(call: Path, transcript: Path):
    request = json.loads((call / 'request.json').read_text(encoding='utf-8-sig'))
    original = (call / 'messages.txt').read_bytes().decode('utf-8-sig')
    raw = transcript.read_bytes()
    if not original.strip():
        raise ValueError('Original reader prompt is empty')
    outputs, image_count, view_scripts, finals, models = [], 0, [], [], []
    rendered_image_hashes = set()
    for number, line in enumerate(raw.splitlines(), 1):
        record = json.loads(line)
        item = record.get('payload', {})
        if record.get('type') == 'turn_context' and item.get('model'):
            models.append(item['model'])
        kind = item.get('type')
        if (kind == 'message' and item.get('role') == 'assistant'
                and item.get('phase') in ('final', 'final_answer')):
            finals.append(''.join(v.get('text', '') for v in item.get('content', [])
                                  if v.get('type') == 'output_text'))
        if kind in ('custom_tool_call', 'function_call'):
            script = item.get('input', item.get('arguments', ''))
            if 'view_image' in script:
                view_scripts.append(script)
        if kind in ('custom_tool_call_output', 'function_call_output'):
            result = item.get('output', '')
            outputs.append((number, text_parts(result)))
            if isinstance(result, list):
                for value in result:
                    if not isinstance(value, dict) or value.get('type') != 'input_image':
                        continue
                    image_count += 1
                    url = value.get('image_url', '')
                    if isinstance(url, str) and url.startswith('data:') and ';base64,' in url:
                        rendered_image_hashes.add(hashlib.sha256(
                            base64.b64decode(url.split(',', 1)[1], validate=True)).hexdigest())
    matched, coverage = complete_prompt_coverage(original, outputs)
    unique = {}
    for im in request.get('images', []):
        unique.setdefault(im['sha256'], []).append(Path(im['path']))
    scripts = '\n'.join(view_scripts)
    for expected, aliases in unique.items():
        viewed = False
        for path in aliases:
            if hashlib.sha256(path.read_bytes()).hexdigest() != expected:
                raise ValueError(f'Request image changed: {path}')
            variants = (str(path), str(path).replace('\\', '\\\\'), path.as_posix())
            viewed = viewed or any(v in scripts for v in variants)
        if expected not in rendered_image_hashes and not viewed:
            raise ValueError(f'No image path for unique SHA appeared in view_image calls: {expected}')
    if image_count < len(unique):
        raise ValueError('Not all unique images were returned to the worker as image content')
    if not finals:
        raise ValueError('Actual completed final answer absent from worker trace')
    if not models or set(models) != {request['requested_model']}:
        raise ValueError('Actual worker trace does not confirm the requested reader model')
    return dict(scope='Original reader input delivery only; answer accuracy not graded',
                original_prompt_output_lines=matched, prompt_coverage=coverage,
                original_prompt_sha256=hashlib.sha256((call / 'messages.txt').read_bytes()).hexdigest(),
                original_prompt_characters=len(original),
                original_prompt_canonical_characters=len(_universal_newlines(original)),
                original_prompt_canonical_sha256=hashlib.sha256(_universal_newlines(original).encode('utf8')).hexdigest(),
                original_prompt_canonical_encoding='UTF8 after Python universal-newline conversion; CRCRLF preserves two LF',
                unique_images=len(unique),
                actual_image_blocks=image_count, transcript=str(transcript),
                unique_images_byte_verified=sum(sha in rendered_image_hashes for sha in unique),
                transcript_sha256=hashlib.sha256(raw).hexdigest(),
                actual_model=models[-1], actual_final_characters=len(finals[-1]),
                actual_final_sha256=hashlib.sha256(finals[-1].encode('utf8')).hexdigest(),
                verified=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--call', type=Path, required=True)
    parser.add_argument('--transcript', type=Path, required=True)
    parser.add_argument('--worker', required=True)
    parser.add_argument('--out', type=Path)
    args = parser.parse_args()
    result = verify(args.call.resolve(), args.transcript.resolve())
    result['worker'] = args.worker
    destination = args.out or args.call / 'input_delivery_proof.json'
    with destination.open('x', encoding='utf8') as stream:
        json.dump(result, stream, ensure_ascii=False, indent=2)
    print(json.dumps(dict(verified=True, unique_images=result['unique_images'], output=str(destination))))


if __name__ == '__main__':
    main()
