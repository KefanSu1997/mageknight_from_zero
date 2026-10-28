"""Use the desktop's bundled MCP client, preserving its own task and approval routing."""
import json
import os
from pathlib import Path
import queue
import subprocess
import threading
import time

THREAD = '01a0816b-a230-7462-a3e4-556ffba50c9f'
def runtime_file(environment, root, pattern):
    explicit = Path(os.environ.get(environment, '__missing__'))
    if explicit.is_file():
        return explicit
    candidates = list(root.glob(pattern))
    # Explorer logon does not inherit Codex's packaged-app filesystem view.
    # Resolve actual installed runtime paths as well as the ordinary LocalAppData.
    if not candidates and environment == 'CODEX_MCP_NODE_PATH':
        packages = Path.home() / 'AppData/Local/Packages'
        candidates = list(packages.glob('OpenAI.Codex_*/LocalCache/Local/OpenAI/Codex/runtimes/cua_node/' + pattern))
    if not candidates:
        raise RuntimeError('Codex 本机运行组件不可用，请打开桌面应用')
    return max(candidates, key=lambda path: path.stat().st_mtime)


def connection_candidates():
    # Pipes are session-scoped. Never persist a previous desktop session's address.
    names = sorted(name for name in os.listdir('\\\\.\\pipe\\')
                   if name.startswith('codex-browser-use-'))
    pipes = ['\\\\.\\pipe\\' + name for name in names]
    preferred = os.environ.get('CODEX_APP_TOOLS_PIPE_PATH')
    if preferred in pipes:
        pipes.remove(preferred)
        pipes.insert(0, preferred)
    return pipes


class Desktop:
    def __init__(self):
        node = runtime_file('CODEX_MCP_NODE_PATH', Path(os.environ['LOCALAPPDATA']) /
                            'OpenAI/Codex/runtimes/cua_node', '*/bin/node.exe')
        server = runtime_file('MAGEKNIGHT_APP_TOOLS_SERVER', Path.home() /
                              '.codex/plugins/cache/openai-bundled/codex-app-tools', '*/server.mjs')
        for pipe in connection_candidates():
            try:
                self.connect(node, server, pipe)
                snapshot = self.call('read_thread', {'threadId': THREAD, 'turnLimit': 1,
                    'includeOutputs': False, 'maxOutputCharsPerItem': 1}, timeout=10)
                if Path(snapshot['thread']['cwd']).resolve() != Path(__file__).resolve().parents[1]:
                    raise RuntimeError('桌面任务目录不匹配')
                return
            except Exception:
                if getattr(self, 'proc', None):
                    self.close()
        raise RuntimeError('Codex 桌面暂不可用；消息已保留，打开桌面后自动转交')

    def connect(self, node, server, pipe):
        environment = dict(os.environ, CODEX_APP_TOOLS_PIPE_PATH=pipe)
        self.proc = subprocess.Popen([str(node), str(server)], stdin=subprocess.PIPE,
            stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True, encoding='utf-8',
            creationflags=subprocess.CREATE_NO_WINDOW, env=environment)
        self.events = queue.Queue()
        self.sequence = 0
        threading.Thread(target=self._read, args=(self.proc, self.events), daemon=True).start()
        self.rpc('initialize', {'protocolVersion':'2024-11-05', 'capabilities':{},
            'clientInfo':{'name':'mageknight-feishu','version':'0.2.0'}}, timeout=10)
        self.send({'jsonrpc':'2.0','method':'notifications/initialized'})

    def _read(self, proc, events):
        for line in proc.stdout:
            try:
                events.put(json.loads(line))
            except ValueError:
                pass
        events.put({'error': {'message':'桌面连接已关闭'}})

    def send(self, value):
        self.proc.stdin.write(json.dumps(value, ensure_ascii=False)+'\n')
        self.proc.stdin.flush()

    def rpc(self, method, params, timeout=70):
        self.sequence += 1
        request_id = self.sequence
        self.send({'jsonrpc':'2.0', 'id':request_id,'method':method,'params':params})
        deadline = time.monotonic()+timeout
        while True:
            reply = self.events.get(timeout=max(.01, deadline-time.monotonic()))
            if reply.get('id') != request_id:
                if 'error' in reply:
                    raise RuntimeError(reply['error']['message'])
                continue
            if 'error' in reply:
                raise RuntimeError(reply['error']['message'])
            return reply.get('result')

    def call(self, name, args, timeout=70):
        result = self.rpc('tools/call', {'name':name,'arguments':args,
            '_meta':{'openai/threadId':THREAD}}, timeout)
        text = '\n'.join(x.get('text','') for x in result.get('content',[]) if x.get('type') == 'text')
        if result.get('isError'):
            raise RuntimeError(text[:500])
        try:
            return json.loads(text)
        except ValueError:
            return text

    def close(self):
        self.proc.terminate()
        try:
            self.proc.wait(timeout=5)
        except subprocess.TimeoutExpired:
            self.proc.kill()
            self.proc.wait(timeout=5)
        self.proc.stdin.close()
        self.proc.stdout.close()


if __name__ == '__main__':
    app = Desktop()
    try:
        tools = app.rpc('tools/list', {})['tools']
        print([x['name'] for x in tools if x['name'] in ('send_message_to_thread','read_thread','wait_threads')])
        result = app.call('read_thread', {'threadId':THREAD,'turnLimit':1,'includeOutputs':False,'maxOutputCharsPerItem':100})
        print(json.dumps(result, ensure_ascii=False)[:2500])
    finally:
        app.close()
