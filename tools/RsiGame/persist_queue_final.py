"""Persist a completed reader's exact final after full input-delivery validation."""
import argparse
import hashlib
import json
from pathlib import Path
from commit_queue_answer import commit, validate_worker_id
from verify_reader_delivery import verify


def persist(call, transcript, worker, publish=False):
    validate_worker_id(worker)
    records = [json.loads(line) for line in transcript.read_bytes().splitlines()]
    meta = records[0].get('payload', {})
    if records[0].get('type') != 'session_meta' or meta.get('agent_path') != worker:
        raise ValueError('Actual transcript does not identify assigned canonical worker')
    request = json.loads((call/'request.json').read_text(encoding='utf-8-sig'))
    if request['kind'] != 'reader':
        raise ValueError('Reader persistence only; editor needs source-work readiness')
    finals = []
    for record in records:
        item = record.get('payload', {})
        if (item.get('type') == 'message' and item.get('role') == 'assistant'
                and item.get('phase') in ('final', 'final_answer')):
            finals.append(''.join(x.get('text', '') for x in item.get('content', [])
                                  if x.get('type') == 'output_text'))
    if not finals:
        raise ValueError('No actual final; drafts are ineligible')
    proof = verify(call, transcript)
    proof['worker'] = worker
    raw = finals[-1].encode('utf8')
    if hashlib.sha256(raw).hexdigest() != proof['actual_final_sha256']:
        raise ValueError('Delivery proof does not bind actual final')
    with (call/'answer.txt').open('xb') as stream:
        stream.write(raw)
    if (call/'answer.txt').read_bytes() != raw:
        raise ValueError('Exact final readback failed')
    with (call/'input_delivery_proof.json').open('x', encoding='utf8') as stream:
        json.dump(proof, stream, indent=2)
    result = {'persisted': True, 'actual_final_sha256': proof['actual_final_sha256'],
              'worker': worker, 'actual_final_count': len(finals), 'published': False}
    if publish:
        result.update(commit(call.parent.parent, worker, proof['actual_model'], call.name))
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--call', type=Path, required=True)
    parser.add_argument('--transcript', type=Path, required=True)
    parser.add_argument('--worker', required=True)
    parser.add_argument('--publish', action='store_true')
    args = parser.parse_args()
    print(json.dumps(persist(args.call.resolve(), args.transcript.resolve(), args.worker, args.publish)))
