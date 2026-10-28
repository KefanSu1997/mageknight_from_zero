"""Small client for the already installed local Unity MCP bridge."""
import argparse
import json
from pathlib import Path
import urllib.request


class UnityMcp:
    def __init__(self):
        self.headers = {'Content-Type': 'application/json', 'Accept': 'application/json, text/event-stream'}
        self.counter = 0
        self.rpc('initialize', {'protocolVersion': '2024-11-05', 'capabilities': {},
                              'clientInfo': {'name': 'structured-ui-pilot', 'version': '1'}})
        self.rpc('notifications/initialized', {}, notification=True)

    def rpc(self, method, params, notification=False):
        self.counter += 1
        data = {'jsonrpc': '2.0', 'method': method, 'params': params}
        if not notification:
            data['id'] = self.counter
        request = urllib.request.Request('http://127.0.0.1:60022/mcp',
            data=json.dumps(data).encode(), headers=self.headers)
        with urllib.request.urlopen(request, timeout=90) as response:
            session = response.headers.get('Mcp-Session-Id')
            if session:
                self.headers['Mcp-Session-Id'] = session
            raw = response.read().decode()
        if not raw:
            return {}
        if any(line.startswith('data: ') for line in raw.splitlines()):
            messages = [json.loads(line[6:]) for line in raw.splitlines() if line.startswith('data: ')]
            return next(item for item in messages if item.get('id') == self.counter)
        return json.loads(raw)

    def call(self, name, arguments):
        return self.rpc('tools/call', {'name': name, 'arguments': arguments})


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('name')
    parser.add_argument('arguments')
    parser.add_argument('--out', required=True)
    args = parser.parse_args()
    result = UnityMcp().call(args.name, json.loads(args.arguments))
    Path(args.out).write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(result, ensure_ascii=False))
