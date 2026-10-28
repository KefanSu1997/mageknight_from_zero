"""Inspect actual delivery so an unfinished review is never reported as complete."""
import argparse
import base64
import hashlib
import json
from pathlib import Path
from verify_reader_delivery import complete_prompt_coverage, text_parts


def inspect(packet_path, transcript_path):
    packet_path, transcript_path = Path(packet_path).resolve(), Path(transcript_path).resolve()
    packet = json.loads(packet_path.read_text(encoding='utf-8-sig'))
    outputs, images, finals, models = [], set(), 0, set()
    last = None
    for line in transcript_path.read_bytes().splitlines():
        row = json.loads(line)
        last = row.get('timestamp')
        payload = row.get('payload', {})
        if row.get('type') == 'turn_context':
            models.add(payload.get('model'))
        if payload.get('type') == 'message' and payload.get('phase') in ('final', 'final_answer'):
            finals += 1
        if payload.get('type') in ('function_call_output', 'custom_tool_call_output'):
            value = payload.get('output', '')
            outputs.append(text_parts(value))
            if isinstance(value, list):
                for block in value:
                    url = block.get('image_url', '') if isinstance(block, dict) else ''
                    if isinstance(url, str) and url.startswith('data:image/png;base64,'):
                        images.add(hashlib.sha256(base64.b64decode(url.split(',', 1)[1])).hexdigest())
    required = packet['required_images']
    text_entries = [dict(path=packet[name + '_path'], sha256=packet[name + '_sha256'])
                    for name in ('sp', 'rubric', 'score_schema')] + packet['required_text']
    incomplete_text = []
    for entry in [dict(path=str(packet_path))] + text_entries:
        path = Path(entry['path'])
        if not path.is_absolute():
            path = packet_path.parent / path
        raw = path.read_bytes()
        if entry.get('sha256') and hashlib.sha256(raw).hexdigest() != entry['sha256']:
            raise ValueError('Frozen evidence changed: ' + str(path))
        try:
            complete_prompt_coverage(raw.decode('utf-8-sig'), list(enumerate(outputs)))
        except ValueError:
            incomplete_text.append(str(path))
    return dict(last_record_utc=last, executed_models=sorted(models, key=str), actual_finals=finals,
                required_original_pngs=len(required), delivered_original_pngs=sum(e['sha256'] in images for e in required),
                incomplete_text=incomplete_text,
                scope='Delivery progress only; actual reading quality, scoring and UI acceptance are not asserted')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input', required=True)
    parser.add_argument('--transcript', required=True)
    args = parser.parse_args()
    print(json.dumps(inspect(args.input, args.transcript), ensure_ascii=False, indent=2))
