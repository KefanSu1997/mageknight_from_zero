"""Offline gates only: never instantiate or operate the live Unity pilot."""
import unittest
import json
import tempfile
from pathlib import Path
from unittest.mock import patch
import run_luna_bridge_experiment as transport
from unity_adapter import public_view, visible_digest
from run_luna_bridge_experiment import Queue


class OfflineGates(unittest.TestCase):
    def test_real_queue_response_read_and_receipt_roundtrip(self):
        # Respond only after each newly persisted request, exercising the real
        # Queue.ask read/decode/model validation and receipt writes end to end.
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            queue = Queue(root)
            original_write = transport.write
            seen = []
            def broker(path, value):
                original_write(path, value)
                if path.name == 'request.json':
                    seen.append(path)
                    encoding = 'utf-8-sig' if len(seen) == 1 else 'utf-8'
                    (path.parent / 'response.json').write_text(json.dumps({
                        'raw': '实际模型回复 ' + str(len(seen)),
                        'executed_model': value['requested_model'],
                        'worker_agent_id': 'offline-test-worker'}), encoding=encoding)
            with patch.object(transport, 'write', side_effect=broker):
                self.assertEqual(queue.ask([{'role': 'user', 'content': 'first'}]), '实际模型回复 1')
                self.assertEqual(queue.ask([{'role': 'user', 'content': 'second'}], kind='editor'), '实际模型回复 2')
            self.assertEqual(queue.n, 2)
            self.assertEqual(len(set(seen)), 2)
            receipts = [json.loads((p.parent / 'receipt.json').read_text(encoding='utf-8')) for p in seen]
            self.assertEqual([r['executed_model'] for r in receipts], ['gpt-6.1-sol', 'gpt-6.1-sol'])
            self.assertTrue(all(r['raw_sha256'] for r in receipts))

    def test_explicit_model_policy(self):
        queue = Queue('.')
        self.assertEqual(queue.requested_model('editor'), 'gpt-6.1-sol')
        self.assertEqual(queue.requested_model('reader'), 'gpt-6.1-sol')
        with self.assertRaises(ValueError):
            queue.requested_model('unknown')
        custom = Queue('.', {'reader': 'chosen-reader', 'editor': 'chosen-editor'})
        self.assertEqual(custom.requested_model('editor'), 'chosen-editor')

    def test_no_silent_model_fallback(self):
        for answer in ({}, {'executed_model': 'gpt-6-luna'}, {'model': 'gpt-6.1-sol'}):
            with self.assertRaises(RuntimeError):
                Queue.verify_model(answer, 'gpt-6.1-sol')
        self.assertEqual(Queue.verify_model({'executed_model': 'gpt-6.1-sol'},
                                           'gpt-6.1-sol'), 'gpt-6.1-sol')

    def test_audit_does_not_reach_visible_payload(self):
        observation = {'version': 'first', 'controls': [], 'state': [
            {'key': 'audit.costNativeState', 'value': 'secret'},
            {'key': 'hand.hiddenOrder', 'value': 'secret'},
            {'key': 'ui.Details', 'value': 'visible'}]}
        self.assertNotIn('secret', str(public_view(observation)))
        self.assertIn('visible', str(public_view(observation)))
        original = visible_digest(observation)
        observation['version'] = 'new opaque audit hash'
        self.assertEqual(original, visible_digest(observation))


if __name__ == '__main__':
    unittest.main()
