"""Commit a worker's raw answer to the current queue, without shell interpolation.

This is a transport gate, not an upstream schema/parser replacement. Genuine
non-JSON model output is preserved for the original parser and retry policy.
"""
import argparse
import hashlib
import json
import os
import re
import time
import uuid
from pathlib import Path

from monitor_report import process_liveness


def validate_worker_id(worker):
    if not re.fullmatch(r'/root(?:/[a-z0-9_]+)+', worker):
        raise ValueError('Worker must be the canonical child-agent task path returned by spawn_agent')


def validate_previous_round_capture(experiment):
    # The next round's directory is created before its first model request.
    # Derive this from the live output tree, not a broker's cached rounds.json.
    run = Path(experiment) / 'development/runs/mageknight-unity'
    numbers = [int(m.group(1)) for p in run.glob('round_*')
               if p.is_dir() and (m := re.fullmatch(r'round_(\d+)', p.name))]
    current = max(numbers, default=0)
    if current < 2:
        return {'current_round': current, 'required_round': None}
    previous = current - 1
    phase = run / f'round_{previous:02d}' / 'key_phases'
    manifest = phase / 'manifest.json'
    restored = phase / 'normal_launch_restored.json'
    if not manifest.is_file() or not restored.is_file():
        raise ValueError(f'Hold: round {previous} needs phase captures and normal-launch restoration')
    if json.loads(manifest.read_text(encoding='utf-8-sig')).get('passed') is not True:
        raise ValueError(f'Hold: round {previous} phase capture did not pass')
    receipt = json.loads(restored.read_text(encoding='utf-8-sig'))
    if not receipt.get('initial_version') or not str(receipt.get('role', '')).startswith('fresh_normal_launch_after_fixture'):
        raise ValueError(f'Hold: round {previous} normal-launch restoration receipt is incomplete')
    return {'current_round': current, 'required_round': previous}


def commit(experiment, worker, model, call):
    validate_worker_id(worker)
    experiment = Path(experiment).resolve()
    validate_previous_round_capture(experiment)
    status_path = experiment / 'live_status.json'
    status = json.loads(status_path.read_text(encoding='utf-8-sig'))
    request_path = Path(status['request']).resolve()
    request_path.relative_to(experiment / 'model_calls')
    if request_path.parent.name != call:
        raise ValueError('Current request does not match the assigned call')
    request = json.loads(request_path.read_text(encoding='utf-8-sig'))
    if status.get('status') != 'waiting_for_model':
        raise ValueError('Runner is not waiting for a model')
    if (request.get('runner_pid'), request.get('runner_instance')) != (status.get('pid'), status.get('runner_instance')):
        raise ValueError('Stale runner identity')
    if process_liveness(status['pid'])['state'] != 'live':
        raise ValueError('Runner process is not confirmed live')
    policy = json.loads((experiment / 'dispatch_policy.json').read_text(encoding='utf-8-sig'))
    expected = policy['editor_model' if request['kind'] == 'editor' else 'reader_model']
    if model != expected or request.get('requested_model') != model:
        raise ValueError('Worker model does not match the requested role')
    answer = request_path.parent / 'answer.txt'
    raw = answer.read_text(encoding='utf-8-sig')
    if not raw.strip() or re.fullmatch(r'\$\{[A-Za-z_]\w*\}', raw.strip()):
        raise ValueError('Empty answer or literal shell-template placeholder')
    policy_path = experiment / 'dispatch_policy.json'
    policy = json.loads(policy_path.read_text(encoding='utf-8-sig')) if policy_path.is_file() else {}
    epoch = policy.get('reader_verified_input_effective_from_call')
    if request['kind'] == 'reader' and epoch is not None and int(call.split('_')[0]) >= int(epoch):
        from verify_reader_delivery import verify
        proof = json.loads((request_path.parent / 'input_delivery_proof.json').read_text(encoding='utf8'))
        if proof.get('worker') != worker or proof.get('verified') is not True:
            raise ValueError('Reader input proof does not identify this completed worker')
        verified = verify(request_path.parent, Path(proof['transcript']))
        if verified['actual_final_sha256'] != hashlib.sha256(raw.encode('utf8')).hexdigest():
            raise ValueError('Saved answer differs from the actual worker final')
    response = request_path.parent / 'response.json'
    if response.exists():
        raise FileExistsError(response)
    if (request_path.parent / 'superseded.json').exists():
        raise ValueError('Assigned request was superseded')
    timeout = policy.get('queue_model_timeout_seconds')
    if timeout is not None:
        margin = policy.get('queue_publish_margin_seconds', 30)
        if time.time() - request_path.stat().st_mtime >= float(timeout) - float(margin):
            raise ValueError('Too late to publish safely before the original queue deadline')
    if json.loads(status_path.read_text(encoding='utf-8-sig')) != status:
        raise ValueError('Runner status changed while validating the answer')
    temporary = response.with_name('response.' + uuid.uuid4().hex + '.tmp')
    try:
        temporary.write_text(json.dumps(dict(raw=raw, executed_model=model,
            broker_agent_id='/root', worker_agent_id=worker), ensure_ascii=False), encoding='utf8')
        # Hard-link creation publishes a complete file and refuses replacement.
        os.link(temporary, response)
    finally:
        temporary.unlink(missing_ok=True)
    receipt_path = response.with_name('receipt.json')
    deadline = time.monotonic() + 5
    consumed = False
    while time.monotonic() < deadline:
        if receipt_path.is_file():
            receipt = json.loads(receipt_path.read_text(encoding='utf-8-sig'))
            consumed = (receipt.get('raw_sha256') == hashlib.sha256(raw.encode('utf8')).hexdigest()
                        and receipt.get('worker_agent_id') == worker
                        and receipt.get('executed_model') == model)
            break
        time.sleep(.1)
    # Publication alone is not acceptance. Never replace an unconsumed answer.
    return dict(request=str(request_path), response=str(response), chars=len(raw),
                published=True, queue_consumed=consumed)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--experiment', type=Path, required=True)
    parser.add_argument('--worker', required=True)
    parser.add_argument('--call', required=True)
    parser.add_argument('--model', choices=['gpt-6-luna', 'gpt-6.1-sol'], required=True)
    args = parser.parse_args()
    print(json.dumps(commit(args.experiment, args.worker, args.model, args.call)))
