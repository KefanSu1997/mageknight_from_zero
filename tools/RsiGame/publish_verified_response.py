"""Publish an actual worker FINAL only to its live, current Windows queue request."""
import argparse
import ctypes
import hashlib
import json
import os
from pathlib import Path

from verify_reader_delivery import verify


def windows_process_alive(pid):
    """Read-only process query. os.kill(pid, 0) is unsafe on Windows."""
    if os.name != 'nt':
        raise RuntimeError('This publisher requires the Windows process query API')
    from ctypes import wintypes
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    kernel.OpenProcess.restype = wintypes.HANDLE
    kernel.GetExitCodeProcess.argtypes = [wintypes.HANDLE, ctypes.POINTER(wintypes.DWORD)]
    kernel.GetExitCodeProcess.restype = wintypes.BOOL
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    handle = kernel.OpenProcess(0x1000, False, pid)  # QUERY_LIMITED_INFORMATION only
    if not handle:
        return False
    try:
        code = wintypes.DWORD()
        if not kernel.GetExitCodeProcess(handle, ctypes.byref(code)):
            raise ctypes.WinError(ctypes.get_last_error())
        return code.value == 259  # STILL_ACTIVE
    finally:
        kernel.CloseHandle(handle)


def publish(call, transcript, worker, broker):
    call, transcript = Path(call).resolve(), Path(transcript).resolve()
    request = json.loads((call / 'request.json').read_bytes())
    if request['kind'] != 'reader':
        raise ValueError('Editors require separate source-change preservation and operator handling')
    proof = verify(call, transcript)
    records = [json.loads(line) for line in transcript.read_bytes().splitlines() if line.strip()]
    identities = [r['payload'].get('agent_path') for r in records if r.get('type') == 'session_meta']
    if identities != [worker]:
        raise ValueError('Actual worker identity mismatch')
    finals = [r['payload'] for r in records if r.get('payload', {}).get('type') == 'message'
              and r['payload'].get('role') == 'assistant'
              and r['payload'].get('phase') in ('final', 'final_answer')]
    raw = ''.join(x.get('text', '') for x in finals[-1].get('content', []) if x.get('type') == 'output_text')
    if hashlib.sha256(raw.encode()).hexdigest() != proof['actual_final_sha256']:
        raise ValueError('Actual FINAL mismatch')
    proof['worker'] = worker
    # Preserve verified work even when its runner has already exited.
    for name, value in [('actual_final.txt', raw.encode()),
                        ('input_delivery_proof.json', json.dumps(proof, ensure_ascii=False, indent=2).encode())]:
        path = call / name
        if path.exists():
            if name == 'actual_final.txt' and path.read_bytes() != value:
                raise ValueError('Refusing to replace a different FINAL')
        else:
            path.write_bytes(value)
    state = json.loads((call.parents[1] / 'live_status.json').read_bytes())
    if (state.get('status') != 'waiting_for_model'
            or state.get('pid') != request['runner_pid']
            or state.get('runner_instance') != request['runner_instance']
            or Path(state.get('request', '')).resolve() != call / 'request.json'
            or not windows_process_alive(request['runner_pid'])):
        raise RuntimeError('Runner is absent or this request is no longer current; FINAL retained, not published')
    response = dict(raw=raw, executed_model=proof['actual_model'], worker_agent_id=worker, broker_agent_id=broker)
    with (call / 'response.json').open('x', encoding='utf8') as stream:
        json.dump(response, stream, ensure_ascii=False)
    return dict(published=True, call=str(call), worker=worker, model=proof['actual_model'])


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--call', required=True)
    parser.add_argument('--transcript', required=True)
    parser.add_argument('--worker', required=True)
    parser.add_argument('--broker', required=True)
    args = parser.parse_args()
    print(json.dumps(publish(args.call, args.transcript, args.worker, args.broker)))
