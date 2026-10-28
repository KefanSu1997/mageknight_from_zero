"""Persistent native solo-conquest transport, shared by collection and evaluation.

No task substitution, action truncation, automatic resets or fabricated terminals.
The policy receives public observation and immutable complete implemented support;
private accounting/binding metadata stays in the audit journal.
"""
import hashlib
import json
import math
import os
from pathlib import Path
import subprocess
import time
import uuid


class NativeRuleError(RuntimeError):
    pass


class SoloNativeEnv:
    def __init__(self, dll, repository, evidence_directory, *, preparation=False):
        self.directory = Path(evidence_directory)
        self.directory.mkdir(parents=True, exist_ok=True)
        self._stderr = (self.directory / 'native_stderr.log').open('ab')
        self.process = subprocess.Popen(['dotnet', str(Path(dll).resolve()), str(Path(repository).resolve())],
                                        stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=self._stderr,
                                        text=True, encoding='utf-8')
        self.state = None
        self.timings = []
        self._uncertain = False
        try:
            self.capabilities = self._request(op='capabilities')
            if not preparation and not self.capabilities['formal_training_ready']:
                from solo_training_admission import verify_admission
                try:
                    self.admission=verify_admission(repository,self.capabilities)
                except (OSError,ValueError,KeyError) as exc:
                    raise NativeRuleError('Formal training gate incomplete: '+str(exc)) from exc
                (self.directory/'admission_receipt.json').write_text(
                    json.dumps(self.admission,ensure_ascii=False,indent=2),encoding='utf-8')
        except BaseException:
            self.close()
            raise

    def _request(self, **request):
        if self._uncertain:
            raise RuntimeError('Transport state uncertain; preserve evidence and restore explicitly in a new process')
        started = time.perf_counter()
        try:
            encoded = json.dumps(request, ensure_ascii=False, allow_nan=False)
            self.process.stdin.write(encoded+'\n')
            self.process.stdin.flush()
            line = self.process.stdout.readline()
            if not line:
                raise RuntimeError('Native process exited; inspect native_stderr.log')
            response = json.loads(line)
        except BaseException:
            self._uncertain = True
            raise
        elapsed = time.perf_counter()-started
        self.timings.append(dict(operation=request['op'], wall_seconds=elapsed, ok=response['ok']))
        if not response['ok']:
            # Native command failures roll back; an I/O failure above is different.
            raise NativeRuleError(response['error']+': '+response['message'])
        return response['result']

    def reset(self, setup):
        self.state = self._request(op='reset', setup=setup)
        return self.state

    def step(self, index, *, log_probability=None, value=None):
        if self.state is None:
            raise NativeRuleError('Reset or restore required')
        if type(index) is not int or not 0 <= index < len(self.state['commands']):
            raise NativeRuleError('Index outside this decision support')
        if any(x is not None and not math.isfinite(x) for x in (log_probability, value)):
            raise ValueError('Behavior probability/value must be finite')
        result = self._request(op='step', index=index, support_sha256=self.state['support_sha256'],
                               log_probability=log_probability, value=value)
        self.state = result['state']
        return result

    def export(self):
        return self._request(op='export')

    def restore(self, archive):
        self.state = self._request(op='restore', archive=archive)
        return self.state

    def save(self, path):
        started = time.perf_counter()
        archive = self.export()
        payload = dict(format='solo_native_transport_v1', native=archive, request_timings=self.timings)
        data = json.dumps(payload, ensure_ascii=False, allow_nan=False, separators=(',', ':')).encode('utf-8')
        path = Path(path)
        path.parent.mkdir(parents=True, exist_ok=True)
        temporary = path.with_name(path.name+'.'+uuid.uuid4().hex+'.pending')
        with temporary.open('xb') as stream:
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
        return dict(path=str(path.resolve()), sha256=hashlib.sha256(data).hexdigest(),
                    bytes=len(data), save_wall_seconds=time.perf_counter()-started,
                    terminal=archive['terminal'], decisions=len(archive['records']))

    def close(self):
        if self.process.poll() is None:
            self.process.stdin.close()
            # Only our own adapter, after EOF. No timeout-triggered restart/kill.
            self.process.wait()
        self.process.stdout.close()
        self._stderr.close()

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.close()
