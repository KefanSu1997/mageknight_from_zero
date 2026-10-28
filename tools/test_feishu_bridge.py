"""Focused local checks; no model requests or Feishu API traffic."""
import json
import logging
import tempfile
from pathlib import Path
from types import SimpleNamespace as NS
import unittest
from unittest.mock import patch

import feishu_bridge as bridge
from feishu_setup import protect


def message(text='状态', mid='m1', sender='owner', chat_type='p2p', sender_type='user'):
    return NS(event=NS(message=NS(chat_type=chat_type, message_type='text',
        content=json.dumps({'text': text}), message_id=mid, chat_id='chat1'),
        sender=NS(sender_type=sender_type, sender_id=NS(open_id=sender))))


class BridgeTests(unittest.TestCase):
    def test_restart_history_recovers_owner_only_and_deduplicates(self):
        import sqlite3
        import threading
        import time
        app = bridge.Bridge.__new__(bridge.Bridge)
        app.lock = threading.RLock()
        app.wakeup = threading.Event()
        app.db = sqlite3.connect(':memory:')
        app.db.execute('CREATE TABLE messages(id TEXT PRIMARY KEY,sender TEXT,chat TEXT,text TEXT,state TEXT,created REAL)')
        app.db.execute('CREATE TABLE settings(key TEXT PRIMARY KEY,value TEXT)')
        app.db.executemany('INSERT INTO settings VALUES (?,?)', [('sender','owner'),('chat','chat1')])
        app.save_status = lambda *a, **k: None
        app.history_since = time.time() - 300
        def item(mid, sender='owner', kind='user', deleted=False, text='继续任务'):
            return NS(message_id=mid, sender=NS(sender_type=kind,id=sender), deleted=deleted,
                msg_type='text', body=NS(content=json.dumps({'text':text})))
        result = NS(success=lambda:True, data=NS(items=[item('hello',text='Hello?'), item('work'),
            item('bot',kind='app'),item('stranger',sender='other'),item('deleted',deleted=True)],has_more=False))
        app.client = NS(im=NS(v1=NS(message=NS(list=lambda request:result))))
        try:
            app.recover_history()
            app.recover_history()
            self.assertEqual(app.db.execute('SELECT id,state FROM messages ORDER BY id').fetchall(),
                             [('hello','control'),('work','queued')])
        finally:
            app.db.close()

    def test_desktop_replaces_missing_runtime_and_stale_pipe(self):
        import feishu_desktop as desktop
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder)/'current/bin/node.exe'
            path.parent.mkdir(parents=True)
            path.write_text('placeholder')
            with patch.dict(desktop.os.environ, {'CODEX_MCP_NODE_PATH':'missing.exe'}):
                self.assertEqual(desktop.runtime_file('CODEX_MCP_NODE_PATH',Path(folder),'*/bin/node.exe'),path)
        with patch.object(desktop.os, 'listdir', return_value=['unrelated','codex-browser-use-current']), \
             patch.dict(desktop.os.environ, {'CODEX_APP_TOOLS_PIPE_PATH':'stale'}):
            self.assertEqual(desktop.connection_candidates(), ['\\\\.\\pipe\\codex-browser-use-current'])

    def test_desktop_offline_keeps_command_queued(self):
        import feishu_desktop
        import sqlite3
        import threading
        app = bridge.Bridge.__new__(bridge.Bridge)
        app.lock = threading.RLock()
        app.wakeup = threading.Event()
        app.wakeup.set()
        app.db = sqlite3.connect(':memory:')
        app.db.execute('CREATE TABLE messages(id TEXT PRIMARY KEY,sender TEXT,chat TEXT,text TEXT,state TEXT,created REAL)')
        app.db.execute("INSERT INTO messages VALUES ('cmd','owner','chat','必须保留','queued',1)")
        app.db.execute('CREATE TABLE settings(key TEXT PRIMARY KEY,value TEXT)')
        notices = []
        app.reply = lambda *args: notices.append(args)
        app.save_status = lambda *a, **k: None
        try:
            with patch.object(feishu_desktop, 'Desktop', side_effect=RuntimeError('offline')), \
                 patch.object(bridge.time, 'sleep', side_effect=InterruptedError('end test')):
                with self.assertRaises(InterruptedError):
                    app.worker()
            self.assertEqual(app.db.execute('SELECT text,state FROM messages').fetchone(),('必须保留','queued'))
            self.assertIn('无需重复发送', notices[0][1])
        finally:
            app.db.close()

    def test_transport_log_removes_secret_and_endpoint_query(self):
        handler = bridge.TransportLog('test-private-secret')
        record = logging.LogRecord('sdk', logging.ERROR, '', 0,
            'failed test-private-secret wss://example.invalid/ws?ticket=private', (), None)
        with patch.object(bridge.LOG, 'error') as sink:
            handler.emit(record)
        logged = sink.call_args.args[1]
        self.assertNotIn('test-private-secret', logged)
        self.assertNotIn('ticket=', logged)

    def test_rich_text_pairing_plain_and_locale_posts(self):
        document = {'title':'','content':[[{'tag':'text','text':'配对\u00a05c4fc323','style':['codeInline']}]]}
        for content in (document, {'zh_cn':document}):
            event = message()
            event.event.message.message_type = 'post'
            event.event.message.content = json.dumps(content)
            self.assertEqual(bridge.incoming(event)[-1], '配对\u00a05c4fc323')
    def test_desktop_dispatch_once_and_wait_for_correct_completion(self):
        import feishu_desktop
        import threading
        for active in (True, False):
            with self.subTest(active=active):
                def snapshot(turn_id, status, text=''):
                    return {'thread': {'cwd': str(bridge.PROJECT)}, 'turns': [{'id':turn_id,
                        'status':status,'items':[{'type':'agentMessage','text':text,'phase':'final_answer'}]}]}
                pages = [snapshot('old', 'inProgress' if active else 'completed', '旧结果')]
                if not active:
                    pages.append(snapshot('old','completed','旧结果'))
                pages.extend([snapshot('old' if active else 'new','inProgress'),
                    snapshot('old' if active else 'new','completed','本次结果')])
                sent = []
                class Fake:
                    def call(self, name, args):
                        if name == 'read_thread': return pages.pop(0)
                        sent.append(args['prompt'])
                        return {'threadId':bridge.THREAD}
                    def close(self): pass
                instance = bridge.Bridge.__new__(bridge.Bridge)
                instance.lock = threading.RLock()
                instance.stop_request = threading.Event()
                instance.save_status = lambda *a, **k: None
                instance.forward_pending = lambda app: None
                with patch.object(feishu_desktop, 'Desktop', Fake), patch.object(instance.stop_request,'wait'):
                    self.assertEqual(instance.run_codex('用户指令'), '本次结果')
                self.assertEqual(sent, ['【来自已配对用户的飞书私聊】\n用户指令'])

    def test_followups_are_forwarded_without_waiting_for_final(self):
        import sqlite3
        import threading
        instance = bridge.Bridge.__new__(bridge.Bridge)
        instance.lock = threading.RLock()
        instance.db = sqlite3.connect(':memory:')
        instance.db.execute('CREATE TABLE messages(id TEXT PRIMARY KEY,sender TEXT,chat TEXT,text TEXT,state TEXT,created REAL)')
        instance.db.executemany('INSERT INTO messages VALUES (?,?,?,?,?,?)', [
            ('original','owner','chat','原任务','running',1),
            ('correction','owner','chat','不要上传测试截图','queued',2),
            ('hello','owner','chat','hello?','queued',3)])
        sent, acknowledgements = [], []
        app = NS(call=lambda name,args: sent.append(args['prompt']))
        instance.save_status = lambda *a, **k: None
        instance.reply = lambda chat,text,key: acknowledgements.append(key)
        try:
            instance.forward_pending(app)
            instance.forward_pending(app)
            self.assertEqual(len(sent),2)
            self.assertIn('不要上传测试截图',sent[0])
            self.assertEqual(len(acknowledgements),2)
            self.assertEqual(instance.db.execute("SELECT state FROM messages WHERE id='original'").fetchone()[0],'running')
            self.assertEqual(instance.db.execute("SELECT count(*) FROM messages WHERE state='attached'").fetchone()[0],2)
        finally:
            instance.db.close()

    def test_rejects_groups_and_bots(self):
        self.assertIsNone(bridge.incoming(message(chat_type='group')))
        self.assertIsNone(bridge.incoming(message(sender_type='app')))

    def test_rejects_missing_identity_and_oversized_input(self):
        self.assertIsNone(bridge.incoming(message(sender='')))
        self.assertIsNone(bridge.incoming(message(text='x' * 12001)))

    def test_dpapi_round_trip(self):
        plain = b'local-test-placeholder'
        encrypted = protect(plain)
        self.assertNotIn(plain, encrypted)
        self.assertEqual(plain, protect(encrypted, True))

    def test_pairing_dedup_and_authorization(self):
        with tempfile.TemporaryDirectory() as directory, patch.object(bridge, 'RUNTIME', Path(directory)):
            (Path(directory)/'credentials.dpapi').write_bytes(protect(json.dumps(
                {'app_id':'test-id','app_secret':'test-placeholder'}).encode()))
            app = bridge.Bridge()
            try:
                app.receive(message())
                self.assertEqual(app.db.execute('SELECT count(*) FROM messages').fetchone()[0], 0)
                app.receive(message('配对\u00a0  ' + app.pair_code, mid='pair'))
                app.receive(message('do the work', mid='work'))
                app.receive(message('do the work', mid='work'))
                app.receive(message('untrusted', mid='other', sender='outsider'))
                self.assertEqual(app.db.execute('SELECT count(*) FROM messages').fetchone()[0], 2)
                self.assertEqual(app.setting('sender'), 'owner')
                app.receive(message('停止', mid='stop'))
                self.assertEqual(app.db.execute('SELECT state FROM messages WHERE id=?', ('stop',)).fetchone()[0], 'control')
                for i, health in enumerate(('状态？', '状态?', ' 状态？ ')):
                    app.receive(message(health, mid='mobile-health-'+str(i)))
                    self.assertEqual(app.db.execute('SELECT state FROM messages WHERE id=?', ('mobile-health-'+str(i),)).fetchone()[0], 'control')
            finally:
                app.db.close()

    def test_expired_pairing_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory, patch.object(bridge, 'RUNTIME', Path(directory)):
            (Path(directory)/'credentials.dpapi').write_bytes(protect(json.dumps(
                {'app_id':'test-id','app_secret':'test-placeholder'}).encode()))
            app = bridge.Bridge()
            try:
                app.pair_until = 0
                app.receive(message('配对 ' + app.pair_code))
                self.assertIsNone(app.setting('sender'))
                row = app.db.execute('SELECT state,text FROM messages').fetchone()
                self.assertEqual(row[0], 'pair_notice')
                self.assertIn('已过期', row[1])
            finally:
                app.db.close()


if __name__ == '__main__':
    unittest.main()
