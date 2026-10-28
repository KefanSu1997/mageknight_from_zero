"""Focused restart/outbox regressions. No live network or model calls."""
import json
import sqlite3
import tempfile
import threading
import unittest
from pathlib import Path
from types import SimpleNamespace as NS
from unittest.mock import patch
import feishu_bridge as bridge
import feishu_runtime as runtime
from feishu_service import health_reason


class ReliabilityTests(unittest.TestCase):
    def test_confirmed_dispatch_survives_restart_without_replaying(self):
        from feishu_setup import protect
        with tempfile.TemporaryDirectory() as folder, patch.object(bridge,'RUNTIME',Path(folder)):
            (Path(folder)/'credentials.dpapi').write_bytes(protect(json.dumps({'app_id':'test','app_secret':'test'}).encode()))
            app=bridge.Bridge()
            for mid,state in [('a','waiting'),('b','attached'),('c','running')]:
                app.db.execute('INSERT INTO messages VALUES (?,?,?,?,?,?)',(mid,'owner','chat','command',state,1))
            app.db.commit();app.db.close()
            app=bridge.Bridge()
            self.assertEqual(app.db.execute('SELECT state FROM messages ORDER BY id').fetchall(),[('waiting',),('attached',),('interrupted_notice',)])
            app.db.close()

    def test_resume_waits_for_result_without_resending_user_command(self):
        app=bridge.Bridge.__new__(bridge.Bridge)
        app.lock=threading.RLock();app.stop_request=threading.Event()
        app.forward_pending=lambda desktop: None
        app.save_status=lambda *a,**k: None
        names=[]
        def call(name,args):
            names.append(name)
            return {'thread':{'cwd':str(bridge.PROJECT)},'turns':[{'id':'existing','status':'completed','items':[{'type':'agentMessage','phase':'final_answer','text':'已完成'}]}]}
        with patch.object(app.stop_request,'wait'):
            self.assertEqual(app.run_codex('already sent',resume=True,app=NS(call=call,close=lambda:None)),'已完成')
        self.assertEqual(names,['read_thread','read_thread'])

    def test_watchdog_distinguishes_startup_healthy_stale_dead_worker_and_offline(self):
        self.assertIsNone(health_reason({}, 'new', 0, 89, 0))
        self.assertIsNone(health_reason({'instance':'old','heartbeat':299}, 'new', 0, 299, 0))
        self.assertEqual(health_reason({'instance':'old','heartbeat':300}, 'new',0,300,0),'startup_timeout')
        self.assertIsNone(health_reason({'instance':'new','heartbeat':199,'connected':True,'workers_ok':True}, 'new',0,200,0))
        self.assertEqual(health_reason({'instance':'new','updated':200,'heartbeat':0}, 'new',0,200,0),'heartbeat_stale')
        self.assertEqual(health_reason({'instance':'new','heartbeat':200,'workers_ok':False}, 'new',0,200,0),'worker_exited')
        self.assertEqual(health_reason({'instance':'new','heartbeat':400,'connected':False}, 'new',0,400,0),'transport_offline')

    def test_proxy_bypass_is_limited_to_feishu_and_preserves_other_proxy_settings(self):
        import requests.utils
        with patch.dict(runtime.os.environ, {'NO_PROXY':'localhost','HTTPS_PROXY':'http://127.0.0.1:1'}):
            runtime.configure_transport()
            self.assertEqual(runtime.os.environ['HTTPS_PROXY'],'http://127.0.0.1:1')
            self.assertTrue(requests.utils.should_bypass_proxies('https://open.feishu.cn/', runtime.os.environ['NO_PROXY']))
            self.assertFalse(requests.utils.should_bypass_proxies('https://api.openai.com/', runtime.os.environ['NO_PROXY']))

    def app(self, path, send):
        app = bridge.Bridge.__new__(bridge.Bridge)
        app.db = sqlite3.connect(path)
        app.db.execute('CREATE TABLE IF NOT EXISTS outbox(id TEXT PRIMARY KEY,chat TEXT,text TEXT,state TEXT,attempts INTEGER,next_try REAL)')
        app.lock = threading.RLock()
        app.save_status = lambda *a,**k: None
        app.client = NS(im=NS(v1=NS(message=NS(create=send))))
        return app

    def test_failed_send_survives_restart_then_same_uuid_delivers_once(self):
        with tempfile.TemporaryDirectory() as folder:
            sent=[]
            def fail(req):
                sent.append(req.body.uuid)
                raise ConnectionError('offline')
            app=self.app(Path(folder)/'queue.db',fail)
            self.assertFalse(app.reply('paired-chat','用户结果','key'))
            self.assertEqual(app.db.execute('SELECT text,state,attempts FROM outbox').fetchone(),('用户结果','pending',1))
            app.db.close()
            def ok(req):
                sent.append(req.body.uuid)
                return NS(success=lambda:True)
            app=self.app(Path(folder)/'queue.db',ok)
            app.db.execute('UPDATE outbox SET next_try=0');app.db.commit()
            self.assertTrue(app.flush_outbox())
            self.assertTrue(app.reply('paired-chat','用户结果','key'))
            self.assertEqual(sent,[sent[0],sent[0]])
            self.assertEqual(app.db.execute('SELECT text,state FROM outbox').fetchone(),('','sent'))
            app.db.close()

    def test_http_api_error_is_pending_not_success(self):
        app=self.app(':memory:',lambda req:NS(success=lambda:False,code=999))
        self.assertFalse(app.reply('paired','结果','api-failure'))
        self.assertEqual(app.db.execute('SELECT state,attempts FROM outbox').fetchone(),('pending',1))
        app.db.close()

if __name__ == '__main__':
    unittest.main()
