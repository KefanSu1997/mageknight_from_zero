"""One-time localhost credential form. Secrets are encrypted with Windows DPAPI."""
import ctypes
from ctypes import wintypes
import html
import json
import os
from pathlib import Path
import secrets
from http.server import BaseHTTPRequestHandler, HTTPServer
from urllib.parse import parse_qs

from feishu_runtime import RUNTIME


class Blob(ctypes.Structure):
    _fields_ = [('size', wintypes.DWORD), ('data', ctypes.POINTER(ctypes.c_char))]


def protect(raw, decrypt=False):
    source = ctypes.create_string_buffer(raw)
    incoming = Blob(len(raw), ctypes.cast(source, ctypes.POINTER(ctypes.c_char)))
    outgoing = Blob()
    func = ctypes.windll.crypt32.CryptUnprotectData if decrypt else ctypes.windll.crypt32.CryptProtectData
    if not func(ctypes.byref(incoming), None, None, None, None, 1, ctypes.byref(outgoing)):
        raise ctypes.WinError()
    try:
        return ctypes.string_at(outgoing.data, outgoing.size)
    finally:
        ctypes.windll.kernel32.LocalFree(outgoing.data)


def main():
    RUNTIME.mkdir(parents=True, exist_ok=True)
    token = secrets.token_urlsafe(24)
    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *args):
            pass

        def do_GET(self):
            if self.path != '/' + token:
                self.send_error(404)
                return
            body = (f'<meta charset="utf-8"><h2>MageKnight 飞书本机配置</h2>'
                    f'<p>凭证仅保存到本机 Windows 加密文件，不进入项目或对话。</p>'
                    f'<form method="post" action="/{html.escape(token)}">'
                    '<label>App ID<input name="app_id" value="cli_aa22bfd78db89ce6"></label><br>'
                    '<label>App Secret<input name="secret" type="password" autocomplete="off"></label><br>'
                    '<button>加密保存到本机</button></form>').encode()
            self.send_response(200)
            self.send_header('Content-Type', 'text/html; charset=utf-8')
            self.end_headers()
            self.wfile.write(body)

        def do_POST(self):
            length = int(self.headers.get('Content-Length', '0'))
            if self.path != '/' + token or not 0 < length < 2048:
                self.send_error(403)
                return
            form = parse_qs(self.rfile.read(length).decode())
            secret = form.get('secret', [''])[0].strip()
            app_id = form.get('app_id', [''])[0].strip()
            if app_id != 'cli_aa22bfd78db89ce6' or not secret:
                self.send_error(400)
                return
            payload = json.dumps({'app_id': app_id, 'app_secret': secret}).encode()
            (RUNTIME / 'credentials.dpapi').write_bytes(protect(payload))
            self.send_response(200)
            self.send_header('Content-Type', 'text/html; charset=utf-8')
            self.end_headers()
            self.wfile.write('<meta charset="utf-8"><p>已加密保存，可以关闭此页。</p>'.encode())
            self.server.saved = True

    server = HTTPServer(('127.0.0.1', 0), Handler)
    server.saved = False
    server.timeout = 5
    (RUNTIME / 'setup-url.txt').write_text(f'http://127.0.0.1:{server.server_port}/{token}')
    while not server.saved:
        server.handle_request()
    server.server_close()


if __name__ == '__main__':
    main()
