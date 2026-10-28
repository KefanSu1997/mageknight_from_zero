import io,json,sqlite3,tempfile,threading,unittest
from pathlib import Path
from types import SimpleNamespace as NS
from unittest.mock import patch
import feishu_bridge as bridge
import feishu_media as media
from test_feishu_bridge import message


def image_event(kind='image',content=None):
    event=message()
    event.event.message.message_type=kind
    event.event.message.content=json.dumps(content or {'image_key':'img-original'})
    return event


class ImageBridgeTests(unittest.TestCase):
    def app(self):
        app=bridge.Bridge.__new__(bridge.Bridge)
        app.lock=threading.RLock();app.wakeup=threading.Event();app.db=sqlite3.connect(':memory:')
        app.db.executescript('CREATE TABLE messages(id TEXT PRIMARY KEY,sender TEXT,chat TEXT,text TEXT,state TEXT,created REAL); CREATE TABLE settings(key TEXT PRIMARY KEY,value TEXT); CREATE TABLE media(id TEXT PRIMARY KEY,image_keys TEXT,attempts INTEGER,next_try REAL);')
        app.db.executemany('INSERT INTO settings VALUES (?,?)',[('sender','owner'),('chat','chat1')]);app.db.commit()
        app.save_status=lambda *a,**k:None
        app.notices=[];app.reply=lambda *args:app.notices.append(args)
        app.client=None
        return app

    def test_image_and_rich_text_images_preserve_caption_and_deduplicate(self):
        self.assertIn('图片',bridge.incoming(image_event())[-1])
        post={'zh_cn':{'title':'黑屏','content':[[{'tag':'text','text':'请查看'}, {'tag':'img','image_key':'one'},{'tag':'img','image_key':'one'},{'tag':'img','image_key':'two'}]]}}
        event=image_event('post',post)
        self.assertEqual(bridge.incoming(event)[-1],'黑屏\n请查看')
        self.assertEqual(media.image_keys('post',event.event.message.content),['one','two'])

    def test_only_paired_user_can_queue_and_duplicate_event_is_single(self):
        app=self.app();event=image_event();event.event.sender.sender_id.open_id='stranger'
        app.receive(event);self.assertEqual(app.db.execute('SELECT count(*) FROM media').fetchone()[0],0)
        event.event.sender.sender_id.open_id='owner';app.receive(event);app.receive(event)
        self.assertEqual(app.db.execute('SELECT state FROM messages').fetchall(),[('media_pending',)])
        self.assertEqual(app.db.execute('SELECT count(*) FROM media').fetchone()[0],1)
        app.db.close()

    def test_download_error_keeps_message_then_success_forwards_path_once(self):
        app=self.app();app.receive(image_event())
        with patch.object(bridge,'download_images',side_effect=ConnectionError('offline')):app.prepare_media()
        self.assertEqual(app.db.execute('SELECT state FROM messages').fetchone()[0],'media_pending')
        app.db.execute('UPDATE media SET next_try=0');app.db.commit()
        with patch.object(bridge,'download_images',return_value=['C:\\paired\\original.png']):app.prepare_media()
        text,state=app.db.execute('SELECT text,state FROM messages').fetchone()
        self.assertEqual(state,'queued');self.assertIn('C:\\paired\\original.png',text);self.assertIn('不自动作为用户指令',text)
        sent=[];desktop=NS(call=lambda name,args:sent.append(args['prompt']))
        app.forward_pending(desktop);app.forward_pending(desktop)
        self.assertEqual(len(sent),1);self.assertIn('original.png',sent[0]);app.db.close()

    def test_completed_download_is_cached_across_retries_and_partial_file_ignored(self):
        calls=[]
        def get(request):
            calls.append(request.message_id)
            return NS(success=lambda:True,file=io.BytesIO(b'\x89PNG\r\n\x1a\n'+b'test-image'))
        client=NS(im=NS(v1=NS(message_resource=NS(get=get))))
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);first=media.download_images(client,root,'original-message',['../../untrusted-key'])
            self.assertEqual(first,media.download_images(client,root,'original-message',['../../untrusted-key']))
            self.assertTrue(Path(first[0]).is_relative_to(root));self.assertEqual(calls,['original-message'])

    def test_invalid_or_oversize_image_has_explicit_failure(self):
        app=self.app();app.receive(image_event())
        with patch.object(bridge,'download_images',side_effect=ValueError('单张图片超过10MiB')):app.prepare_media()
        self.assertEqual(app.db.execute('SELECT state FROM messages').fetchone()[0],'media_failed')
        self.assertIn('超过10MiB',app.notices[-1][1]);app.db.close()
        bad_response=lambda request:NS(success=lambda:True,file=io.BytesIO(b'<html>not image'))
        client=NS(im=NS(v1=NS(message_resource=NS(get=bad_response))))
        with tempfile.TemporaryDirectory() as folder:
            with self.assertRaises(ValueError):media.download_images(client,Path(folder),'m',['key'])
            self.assertEqual(list((Path(folder)/'incoming').iterdir()),[])

    def test_cancelled_attachment_is_not_resurrected_by_inflight_download(self):
        app=self.app();app.receive(image_event())
        def downloaded(*args):
            app.db.execute("UPDATE messages SET state='cancelled',text=''");app.db.commit()
            return ['C:\\test.png']
        with patch.object(bridge,'download_images',side_effect=downloaded):app.prepare_media()
        self.assertEqual(app.db.execute('SELECT state,text FROM messages').fetchone(),('cancelled',''))
        self.assertEqual(app.notices,[]);app.db.close()

if __name__=='__main__':unittest.main()
