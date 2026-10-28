"""Per-user logon supervisor. No admin rights, network settings, or model polling."""
import json
import os
from pathlib import Path
import socket
import subprocess
import sys
import time
import uuid
from feishu_runtime import RUNTIME


def health_reason(status, instance, started, now, disconnected_since):
    # SDK cold imports can exceed 90s on this host. Do not repeatedly kill a
    # process before its first heartbeat; the startup wait is still bounded.
    current_heartbeat = status.get('instance') == instance and status.get('heartbeat', 0) >= started
    if not current_heartbeat:
        return None if now - started < 300 else 'startup_timeout'
    if now - status.get('heartbeat', 0) > 180:
        return 'heartbeat_stale'
    if status.get('workers_ok') is False:
        return 'worker_exited'
    if not status.get('connected') and now - disconnected_since > 300:
        return 'transport_offline'
    return None


def write_state(runtime, value):
    temporary = runtime / 'service.tmp'
    temporary.write_text(json.dumps(dict(value, pid=os.getpid(), updated=time.time())), encoding='utf-8')
    temporary.replace(runtime / 'service.json')


def stop_child(process):
    if process.poll() is None:
        # The Windows venv executable is a launcher; stop its owned child too.
        subprocess.run(['taskkill', '/PID', str(process.pid), '/T', '/F'],
            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=15,
            creationflags=subprocess.CREATE_NO_WINDOW)
        process.wait(timeout=15)


def main():
    runtime = RUNTIME
    runtime.mkdir(parents=True, exist_ok=True)
    guard = socket.socket()
    guard.setsockopt(socket.SOL_SOCKET, socket.SO_EXCLUSIVEADDRUSE, 1)
    try:
        guard.bind(('127.0.0.1', 19572))
    except OSError:
        return  # Already supervised in this user session.
    script = Path(__file__).with_name('feishu_bridge.py')
    delay = 30
    while True:
        started = time.time()
        instance = uuid.uuid4().hex
        disconnected_since = started
        reason = 'child_exited'
        process = None
        try:
            write_state(runtime, {'state':'starting', 'instance':instance})
            with (runtime / 'startup-error.log').open('w', encoding='utf-8') as errors:
                process = subprocess.Popen([sys.executable, str(script)], cwd=script.parent.parent,
                    stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=errors,
                    creationflags=subprocess.CREATE_NO_WINDOW,
                    env=dict(os.environ, MAGEKNIGHT_FEISHU_HOME=str(runtime), MAGEKNIGHT_FEISHU_INSTANCE=instance))
                while process.poll() is None:
                    try:
                        status = json.loads((runtime/'status.json').read_text(encoding='utf-8'))
                    except (OSError, ValueError):
                        status = {}
                    if status.get('connected') and status.get('instance') == instance:
                        disconnected_since = time.time()
                    failure = health_reason(status, instance, started, time.time(), disconnected_since)
                    write_state(runtime, {'child_pid':process.pid, 'instance':instance, 'started':started,
                        'state':'recovering' if failure else 'running', 'health':failure or 'monitoring'})
                    if failure:
                        reason = failure
                        stop_child(process)
                        break
                    time.sleep(15)
        except Exception as error:
            reason = type(error).__name__
            if process is not None:
                stop_child(process)
        write_state(runtime, {'state':'retrying', 'reason':reason,
            'exit_code':process.returncode if process else None, 'retry_seconds':delay})
        try:
            status = json.loads((runtime / 'status.json').read_text(encoding='utf-8'))
            status.update(connected=False, state='restarting', updated=time.time())
            temporary = runtime / 'status.service.tmp'
            temporary.write_text(json.dumps(status, ensure_ascii=False), encoding='utf-8')
            temporary.replace(runtime / 'status.json')
        except (OSError, ValueError):
            pass
        time.sleep(delay)
        delay = 30 if time.time() - started > 300 else min(delay * 2, 300)


if __name__ == '__main__':
    main()
