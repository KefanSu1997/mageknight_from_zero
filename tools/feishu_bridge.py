"""Feishu private messages -> the existing local Codex task. No Unity dependency."""
import json
import logging
from logging.handlers import RotatingFileHandler
import os
import re
from pathlib import Path
import secrets
import socket
import sqlite3
import threading
import time
import uuid
from types import SimpleNamespace

from feishu_setup import RUNTIME, protect
from feishu_media import image_keys, post_content, download_images, attachment_text

PROJECT = Path(__file__).resolve().parents[1]
THREAD = '01a0816b-a230-7462-a3e4-556ffba50c9f'
LOG = logging.getLogger('feishu_bridge')


class DesktopTaskStopped(RuntimeError):
    pass


class TransportLog(logging.Handler):
    """Keep actionable SDK errors even under pythonw, without endpoint tokens."""
    def __init__(self, secret):
        super().__init__(logging.ERROR)
        self.secret = secret

    def emit(self, record):
        text = record.getMessage().replace(self.secret, '[redacted]')
        text = re.sub(r'https?://\S+|wss?://\S+', '[endpoint]', text)
        LOG.error('transport: %s', text[:700])


def incoming(data):
    """Accept private text and image references; attachment content is not an instruction."""
    event = data.event
    msg, sender = event.message, event.sender
    if msg.chat_type != 'p2p' or sender.sender_type != 'user' or msg.message_type not in ('text', 'post', 'image'):
        return None
    content = json.loads(msg.content)
    if msg.message_type == 'post':
        content = post_content(content)
        lines = [content.get('title', '')]
        for row in content.get('content', []):
            lines.append(''.join(item.get('text', '') for item in row
                if item.get('tag') in ('text', 'a', 'code_block', 'md')))
        text = '\n'.join(lines).strip()
    else:
        text = content.get('text', '').strip()
    if image_keys(msg.message_type, msg.content) and not text:
        text = '用户发送了图片附件。'
    identity = sender.sender_id.open_id
    if not identity or not msg.message_id or not text or len(text) > 12000:
        return None
    return msg.message_id, identity, msg.chat_id, text


class Bridge:
    def __init__(self):
        LOG.info('startup: loading Feishu SDK')
        import lark_oapi as lark
        self.lark = lark
        LOG.info('startup: reading local credentials')
        self.config = json.loads(protect((RUNTIME / 'credentials.dpapi').read_bytes(), True))
        from lark_oapi.core.log import logger as sdk_logger
        sdk_logger.addHandler(TransportLog(self.config['app_secret']))
        self.client = lark.Client.builder().app_id(self.config['app_id']).app_secret(
            self.config['app_secret']).timeout(15).log_level(lark.LogLevel.ERROR).build()
        LOG.info('startup: opening durable queues')
        self.lock = threading.RLock()
        self.db = sqlite3.connect(RUNTIME / 'messages.sqlite', check_same_thread=False)
        self.db.execute('CREATE TABLE IF NOT EXISTS messages (id TEXT PRIMARY KEY, sender TEXT, chat TEXT, text TEXT, state TEXT, created REAL)')
        self.db.execute('CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY, value TEXT)')
        self.db.execute('CREATE TABLE IF NOT EXISTS outbox (id TEXT PRIMARY KEY, chat TEXT, text TEXT, state TEXT, attempts INTEGER, next_try REAL)')
        self.db.execute('CREATE TABLE IF NOT EXISTS media (id TEXT PRIMARY KEY, image_keys TEXT, attempts INTEGER, next_try REAL)')
        self.db.commit()
        self.pair_code = secrets.token_hex(4)
        self.pair_until = time.time() + 1800
        pair_file = RUNTIME / 'pairing.txt'
        if pair_file.exists() and pair_file.stat().st_mtime + 1800 > time.time():
            self.pair_code = pair_file.read_text(encoding='utf-8').strip().split()[-1]
            self.pair_until = pair_file.stat().st_mtime + 1800
        else:
            pair_file.write_text('配对 ' + self.pair_code, encoding='utf-8')
        self.ws = None
        self.current = None
        self.stop_request = threading.Event()
        self.wakeup = threading.Event()
        # Ambiguous in-flight actions must not silently disappear or execute twice.
        self.db.execute("UPDATE messages SET state='interrupted_notice' WHERE state IN ('running','dispatching')")
        self.db.commit()
        try:
            previous = json.loads((RUNTIME / 'status.json').read_text(encoding='utf-8'))
        except (OSError, ValueError):
            previous = {}
        self.history_since = max(time.time() - 7 * 86400,
            previous.get('last_live', previous.get('last_incoming', time.time())) - 120)
        self.save_status('starting', connected=False, error=None)
        LOG.info('startup: queues ready')

    def setting(self, key):
        row = self.db.execute('SELECT value FROM settings WHERE key=?', (key,)).fetchone()
        return row[0] if row else None

    def save_status(self, state=None, **extra):
        path = RUNTIME / 'status.json'
        try:
            value = json.loads(path.read_text(encoding='utf-8'))
        except (OSError, ValueError):
            value = {}
        if state:
            value['state'] = state
        value.update(extra, updated=time.time(), pid=os.getpid(), thread=THREAD,
                     instance=os.environ.get('MAGEKNIGHT_FEISHU_INSTANCE'))
        tmp = path.with_suffix('.tmp')
        tmp.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')
        tmp.replace(path)

    def reply(self, chat, text, key):
        # Persist before delivery. A transient API failure must not discard the
        # user's command or a completed model result. Reuse the same Feishu UUID.
        with self.lock:
            for i, offset in enumerate(range(0, len(text), 2500)):
                mid = str(uuid.uuid5(uuid.NAMESPACE_URL, key + ':' + str(i)))
                self.db.execute('INSERT OR IGNORE INTO outbox VALUES (?,?,?,?,?,?)',
                    (mid, chat, text[offset:offset+2500], 'pending', 0, 0))
            self.db.commit()
        return self.flush_outbox()

    def flush_outbox(self):
        from lark_oapi.api.im.v1 import CreateMessageRequest, CreateMessageRequestBody
        with self.lock:
            rows = self.db.execute("SELECT id,chat,text,attempts FROM outbox WHERE state='pending' AND next_try<=? ORDER BY rowid LIMIT 5", (time.time(),)).fetchall()
            # Serialize send attempts across worker/control/outbox threads.
            for mid, chat, text, attempts in rows:
                self.db.execute('UPDATE outbox SET next_try=? WHERE id=?', (time.time()+60, mid))
            self.db.commit()
        for mid, chat, text, attempts in rows:
            req = CreateMessageRequest.builder().receive_id_type('chat_id').request_body(
                CreateMessageRequestBody.builder().receive_id(chat).msg_type('text').content(
                    json.dumps({'text': text}, ensure_ascii=False)).uuid(mid).build()).build()
            try:
                response = self.client.im.v1.message.create(req)
                if not response.success():
                    raise RuntimeError('Feishu code=' + str(response.code))
            except Exception as error:
                with self.lock:
                    self.db.execute('UPDATE outbox SET attempts=?,next_try=? WHERE id=?',
                        (attempts+1, time.time()+min(30*2**min(attempts,4),300), mid))
                    self.db.commit()
                    self.save_status(outbox_error=type(error).__name__)
                LOG.error('outbox retry pending: %s', type(error).__name__)
                continue
            with self.lock:
                self.db.execute("UPDATE outbox SET state='sent',text='' WHERE id=?", (mid,))
                self.db.commit()
                self.save_status(last_reply=time.time(), outbox_error=None)
        with self.lock:
            pending = self.db.execute("SELECT count(*) FROM outbox WHERE state='pending'").fetchone()[0]
            self.save_status(outbox_pending=pending)
        return pending == 0

    def deliver_outbox(self):
        while True:
            try:
                self.flush_outbox()
            except Exception as error:
                LOG.error('outbox worker: %s', type(error).__name__)
            time.sleep(15)

    def receive(self, data):
        try:
            item = incoming(data)
            if not item:
                return
            mid, sender, chat, text = item
            with self.lock:
                self.save_status(last_incoming=time.time(), incoming_type=data.event.message.message_type)
                paired = self.setting('sender')
                if not paired:
                    pair_text = ' '.join(text.strip('`').split())
                    matches = secrets.compare_digest(pair_text.encode(), ('配对 ' + self.pair_code).encode())
                    if not matches or time.time() > self.pair_until:
                        if pair_text.startswith('配对'):
                            reason = '配对码已过期，请回到 Codex 获取有效配对码。' if matches else '配对码不匹配，请复制 Codex 中给出的完整配对消息。'
                            self.db.execute('INSERT OR IGNORE INTO messages VALUES (?,?,?,?,?,?)',
                                (mid, sender, chat, reason, 'pair_notice', time.time()))
                            self.db.commit()
                            self.wakeup.set()
                        return
                    self.db.execute('INSERT INTO settings VALUES (?,?)', ('sender', sender))
                    self.db.execute('INSERT INTO settings VALUES (?,?)', ('chat', chat))
                    self.db.commit()
                    state = 'paired'
                elif sender != paired or chat != self.setting('chat'):
                    return
                else:
                    state = 'control' if text.lower().rstrip('?？') in ('状态', 'ping', '连接测试', '停止', '取消排队', 'hello', '你好') else 'queued'
                attachments = image_keys(data.event.message.message_type, data.event.message.content)
                if attachments:
                    state = 'media_pending'
                cursor = self.db.execute('INSERT OR IGNORE INTO messages VALUES (?,?,?,?,?,?)',
                    (mid, sender, chat, text, state, time.time()))
                if cursor.rowcount == 0:
                    self.db.commit()
                    return
                if attachments:
                    self.db.execute('INSERT OR IGNORE INTO media VALUES (?,?,0,0)', (mid, json.dumps(attachments)))
                self.db.commit()
                self.save_status(last_received=time.time(), paired=True)
                self.wakeup.set()
        except Exception as error:
            LOG.error('receive failed: %s', type(error).__name__)

    def prepare_media(self):
        with self.lock:
            row = self.db.execute("SELECT m.id,m.chat,m.text,a.image_keys,a.attempts FROM messages m JOIN media a ON m.id=a.id WHERE m.state='media_pending' AND a.next_try<=? AND m.sender=? AND m.chat=? ORDER BY m.created LIMIT 1",
                (time.time(), self.setting('sender'), self.setting('chat'))).fetchone()
        if not row:
            return
        mid, chat, text, keys, attempts = row
        try:
            paths = download_images(self.client, RUNTIME, mid, json.loads(keys))
            with self.lock:
                changed = self.db.execute("UPDATE messages SET text=?,state='queued' WHERE id=? AND state='media_pending'", (attachment_text(text, paths), mid))
                self.db.commit()
            if changed.rowcount:
                self.reply(chat, f'已接收{len(paths)}张图片，已保存并排队转交当前任务。', mid + ':image-ready')
                self.wakeup.set()
        except Exception as error:
            attempts += 1
            terminal = isinstance(error, ValueError) or attempts >= 5
            with self.lock:
                self.db.execute('UPDATE media SET attempts=?,next_try=? WHERE id=?',
                    (attempts, time.time() + min(300, 30 * 2 ** (attempts - 1)), mid))
                if terminal:
                    self.db.execute("UPDATE messages SET state='media_failed' WHERE id=? AND state='media_pending'", (mid,))
                self.db.commit()
            LOG.error('image preparation failed: %s', type(error).__name__)
            if terminal:
                reason = str(error) if isinstance(error, ValueError) else '下载重试仍未成功'
                self.reply(chat, '图片未转交：' + reason + '。请重新发送图片；原文字已保留。', mid + ':image-failed')
            elif attempts == 1:
                self.reply(chat, '图片已收到，下载暂时失败，已保存并自动重试，无需重复发送。', mid + ':image-retry')

    def media_worker(self):
        while True:
            try:
                self.prepare_media()
            except Exception as error:
                LOG.error('media worker: %s', type(error).__name__)
            time.sleep(3)

    def finish(self, mid, state):
        with self.lock:
            self.db.execute('UPDATE messages SET state=?, text=? WHERE id=?', (state, '', mid))
            self.db.execute('DELETE FROM settings WHERE key=?', ('wait_notice:' + mid,))
            self.db.execute("DELETE FROM messages WHERE state IN ('done','failed','interrupted') AND created < ?", (time.time()-7*86400,))
            self.db.commit()

    def forward_pending(self, app):
        """Deliver additions during an active task instead of waiting for its final answer."""
        with self.lock:
            rows = self.db.execute("SELECT id,chat,text FROM messages WHERE state='queued' ORDER BY created LIMIT 10").fetchall()
        for mid, chat, text in rows:
            with self.lock:
                claimed = self.db.execute("UPDATE messages SET state='dispatching' WHERE id=? AND state='queued'", (mid,))
                self.db.commit()
                if claimed.rowcount != 1:
                    continue
            try:
                app.call('send_message_to_thread', {'threadId': THREAD,
                    'prompt': '【来自已配对用户的飞书补充指示】\n' + text})
                with self.lock:
                    self.db.execute("UPDATE messages SET state='attached' WHERE id=?", (mid,))
                    self.db.commit()
                    self.save_status(last_dispatched=time.time())
                self.reply(chat, '补充指示已送入当前任务，正在结合你的新要求处理。', mid + ':ack')
            except Exception as error:
                # Ambiguous sends are never replayed automatically.
                LOG.error('follow-up failed: %s', type(error).__name__)
                self.finish(mid, 'failed')
                self.reply(chat, '补充指示转交未能确认，请在电脑端查看；没有自动重发。', mid + ':error')

    def recover_history(self):
        """Read only the paired private chat, bounded to five small pages per recovery."""
        from lark_oapi.api.im.v1 import ListMessageRequest
        with self.lock:
            chat, owner = self.setting('chat'), self.setting('sender')
        if not chat or not owner:
            return
        until, token, recovered = time.time(), None, 0
        for _ in range(5):
            builder = (ListMessageRequest.builder().container_id_type('chat').container_id(chat)
                .start_time(str(int(self.history_since))).end_time(str(int(until)))
                .sort_type('ByCreateTimeAsc').page_size(50))
            if token:
                builder.page_token(token)
            response = self.client.im.v1.message.list(builder.build())
            if not response.success():
                raise RuntimeError('离线补收失败 code=' + str(response.code))
            for item in response.data.items or []:
                if item.deleted or not item.sender or item.sender.sender_type != 'user' or item.sender.id != owner:
                    continue
                with self.lock:
                    exists = self.db.execute('SELECT 1 FROM messages WHERE id=?', (item.message_id,)).fetchone()
                if exists:
                    continue
                self.receive(SimpleNamespace(event=SimpleNamespace(
                    message=SimpleNamespace(chat_type='p2p', message_type=item.msg_type,
                        content=item.body.content, message_id=item.message_id, chat_id=chat),
                    sender=SimpleNamespace(sender_type='user', sender_id=SimpleNamespace(open_id=owner)))))
                recovered += 1
            if not response.data.has_more:
                self.history_since = until - 120
                with self.lock:
                    self.save_status(history_checked=until, history_recovered=recovered, history_error=None)
                return
            token = response.data.page_token
        raise RuntimeError('离线记录超过本轮 250 条上限，需人工检查；未跳过记录')

    def run_codex(self, text, resume=False, app=None, mid=None):
        from feishu_desktop import Desktop
        self.stop_request.clear()
        app = app or Desktop()
        try:
            before = app.call('read_thread', {'threadId': THREAD, 'turnLimit': 1,
                'includeOutputs': False, 'maxOutputCharsPerItem': 80})
            if Path(before['thread']['cwd']).resolve() != PROJECT:
                raise RuntimeError('任务目录不匹配，已停止')
            old = before.get('turns', [{}])[0]
            active = old.get('status') == 'inProgress'
            if not resume:
                app.call('send_message_to_thread', {'threadId': THREAD,
                    'prompt': '【来自已配对用户的飞书私聊】\n' + text})
            with self.lock:
                if mid:
                    self.db.execute("UPDATE messages SET state='waiting' WHERE id=?", (mid,))
                    self.db.commit()
                self.save_status('awaiting_desktop', desktop_connected=True, error=None,
                                 last_dispatched=time.time())
            # Read only a compact active-turn snapshot. No model polling or extra turns.
            while True:
                self.forward_pending(app)
                self.stop_request.wait(15)
                if self.stop_request.is_set():
                    app.call('send_message_to_thread', {'threadId': THREAD,
                        'prompt': '【来自已配对用户的飞书私聊】停止当前任务，汇报已经完成的进度。'})
                    self.stop_request.clear()
                snapshot = app.call('read_thread', {'threadId': THREAD, 'turnLimit': 1,
                    'includeOutputs': False, 'maxOutputCharsPerItem': 12000})
                turns = snapshot.get('turns', [])
                if not turns:
                    continue
                turn = turns[0]
                if not resume and not active and turn['id'] == old.get('id'):
                    continue
                if turn['status'] == 'inProgress':
                    continue
                finals = [item.get('text', '') for item in turn.get('items', [])
                    if item.get('type') == 'agentMessage' and item.get('phase') != 'commentary']
                if turn['status'] != 'completed':
                    raise DesktopTaskStopped('桌面任务状态：' + turn['status'] + '；请在电脑端查看')
                return '\n\n'.join(finals) or '桌面任务已结束，请在电脑端查看结果。'
        finally:
            app.close()

    def worker(self):
        while True:
            self.wakeup.wait(10)
            self.wakeup.clear()
            with self.lock:
                row = self.db.execute("SELECT id,chat,text,state FROM messages WHERE state IN ('queued','waiting') ORDER BY created LIMIT 1").fetchone()
            if not row:
                continue
            mid, chat, text, state = row
            # A failed local connection is known not to have dispatched anything.
            # Keep the durable queue instead of losing a command while Codex is closed.
            from feishu_desktop import Desktop
            try:
                app = Desktop()
            except Exception as error:
                with self.lock:
                    self.save_status('waiting_desktop', desktop_connected=False, error=type(error).__name__)
                try:
                    with self.lock:
                        notified = self.setting('wait_notice:' + mid)
                    if not notified:
                        self.reply(chat, '已收到并保存。Codex 桌面暂不可用，打开后会自动转交；无需重复发送。', mid + ':waiting')
                        with self.lock:
                            self.db.execute('INSERT OR REPLACE INTO settings VALUES (?,?)', ('wait_notice:' + mid, '1'))
                            self.db.commit()
                except Exception:
                    LOG.error('waiting notice could not be sent')
                time.sleep(30)
                continue
            try:
                if state == 'queued':
                    self.reply(chat, '已收到，正在转交桌面原任务；完成后回传文字结果。', mid + ':ack')
                with self.lock:
                    self.db.execute("UPDATE messages SET state=? WHERE id=?", ('waiting' if state == 'waiting' else 'running', mid))
                    self.db.commit()
                    self.save_status('processing')
                result = self.run_codex(text, resume=(state == 'waiting'), app=app, mid=mid)
                self.reply(chat, result, mid + ':result')
                self.finish(mid, 'done')
                with self.lock:
                    self.db.execute("UPDATE messages SET state='done',text='' WHERE state='attached'")
                    self.db.commit()
                    self.save_status('ready', last_codex_completed=time.time())
            except Exception as error:
                if app.proc.poll() is None:
                    app.close()
                LOG.error('message failed: %s', type(error).__name__)
                with self.lock:
                    delivered = self.db.execute('SELECT state FROM messages WHERE id=?', (mid,)).fetchone()
                    if delivered and delivered[0] == 'waiting':
                        self.save_status('waiting_desktop', desktop_connected=False, error=type(error).__name__)
                if delivered and delivered[0] == 'waiting' and not isinstance(error, DesktopTaskStopped):
                    # The desktop acknowledged dispatch; reconnect only to wait
                    # for its result. Never replay this user's action.
                    time.sleep(30)
                    continue
                self.finish(mid, 'failed')
                try:
                    self.reply(chat, '本次未完成：' + str(error)[:400] + '\n没有自动重跑，请在电脑端查看。', mid + ':error')
                except Exception:
                    LOG.error('failure notice could not be sent')
                with self.lock:
                    self.save_status('needs_attention', error=type(error).__name__)

    def controls(self):
        """Health and stop messages remain responsive while a model turn is running."""
        was_connected = None
        heartbeat = 0
        recovered_connection = False
        recovery_retry = 0
        while True:
            with self.lock:
                row = self.db.execute("SELECT id,chat,text,state FROM messages WHERE state IN ('control','paired','pair_notice','interrupted_notice') ORDER BY created LIMIT 1").fetchone()
                connected = self.ws is not None and self.ws._conn is not None
                if connected != was_connected:
                    if was_connected and recovered_connection:
                        self.history_since = max(self.history_since, heartbeat - 120)
                    self.save_status(connected=connected)
                    was_connected = connected
                    recovered_connection = False
                if time.time() - heartbeat >= 15:
                    extra = {'last_live': time.time()} if connected and recovered_connection else {}
                    self.save_status(connected=connected, heartbeat=time.time(), workers_ok=all(t.is_alive() for t in self.workers), **extra)
                    heartbeat = time.time()
            if connected and not recovered_connection and time.time() >= recovery_retry:
                try:
                    self.recover_history()
                    recovered_connection = True
                except Exception as error:
                    LOG.error('history recovery failed: %s', str(error)[:120])
                    with self.lock:
                        self.save_status(history_error=str(error)[:120])
                    recovery_retry = time.time() + 300
            if not row:
                time.sleep(1)
                continue
            mid, chat, text, state = row
            try:
                if state == 'interrupted_notice':
                    answer = '上次有指令在重启或连接中断时尚未确认完成。为避免重复操作，没有自动重跑，请在桌面检查进度。'
                elif state == 'pair_notice':
                    answer = text
                elif state == 'paired':
                    answer = '配对成功：飞书私聊收发已接通。发送“状态”检查连接；直接发文字可提交 MageKnight 指令。消息会进入当前桌面任务，沿用原上下文。'
                elif text.rstrip('?？') in ('停止', '取消排队'):
                    if text.rstrip('?？') == '停止':
                        self.stop_request.set()
                    with self.lock:
                        self.db.execute("UPDATE messages SET state='cancelled',text='' WHERE state IN ('queued','waiting','media_pending')")
                        self.db.commit()
                    answer = '已取消尚未执行的飞书指令。' + ('已请求停止飞书发起的当前运行。' if text.rstrip('?？') == '停止' else '')
                else:
                    from feishu_desktop import Desktop
                    try:
                        desktop = Desktop()
                        desktop.close()
                        desktop_state = '可转交'
                    except Exception:
                        desktop_state = '暂不可用；新指令会保存等待'
                    with self.lock:
                        pending = self.db.execute("SELECT count(*) FROM messages WHERE state IN ('queued','waiting','running','attached','dispatching','media_pending')").fetchone()[0]
                    answer = f'飞书收发：在线\nCodex 桌面：{desktop_state}\n项目：MageKnight_from_zero\n待完成指令：{pending}\n本次状态查询未调用模型。'
                self.reply(chat, answer, mid)
                self.finish(mid, 'interrupted' if state == 'interrupted_notice' else 'done')
            except Exception as error:
                self.finish(mid, 'failed')
                LOG.error('control failed: %s', type(error).__name__)

    def run(self):
        # The user also enabled read/reaction/recalled events; acknowledge without storing them.
        ignore = lambda event: None
        handler = (self.lark.EventDispatcherHandler.builder('', '')
            .register_p2_im_message_receive_v1(self.receive)
            .register_p2_im_message_message_read_v1(ignore)
            .register_p2_im_message_reaction_created_v1(ignore)
            .register_p2_im_message_reaction_deleted_v1(ignore)
            .register_p2_im_message_recalled_v1(ignore).build())
        self.ws = self.lark.ws.Client(self.config['app_id'], self.config['app_secret'],
            event_handler=handler, log_level=self.lark.LogLevel.ERROR)
        self.save_status('listening')
        self.workers = [threading.Thread(target=self.worker, daemon=True),
                        threading.Thread(target=self.media_worker, daemon=True),
                        threading.Thread(target=self.deliver_outbox, daemon=True)]
        for worker in self.workers:
            worker.start()
        threading.Thread(target=self.controls, daemon=True).start()
        self.ws.start()


def main():
    RUNTIME.mkdir(parents=True, exist_ok=True)
    # Process-local singleton; no settings/VPN/firewall changes.
    guard = socket.socket()
    guard.setsockopt(socket.SOL_SOCKET, socket.SO_EXCLUSIVEADDRUSE, 1)
    guard.bind(('127.0.0.1', 19571))
    handler = RotatingFileHandler(RUNTIME / 'bridge.log', maxBytes=65536, backupCount=1, encoding='utf-8')
    LOG.addHandler(handler)
    LOG.setLevel(logging.INFO)
    handler.setFormatter(logging.Formatter('%(asctime)s %(levelname)s %(message)s'))
    try:
        Bridge().run()
    except Exception as error:
        LOG.error('bridge stopped: %s', type(error).__name__)
        raise


if __name__ == '__main__':
    main()
