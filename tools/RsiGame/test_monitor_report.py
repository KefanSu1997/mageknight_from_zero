import json
import os
from pathlib import Path
import tempfile
import unittest

from monitor_report import collect, render, atomic_write, process_liveness, redact


class MonitorReportTests(unittest.TestCase):
    def test_pending_pid_and_counts_are_independent(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            run = root / 'development/runs/mageknight-unity'
            run.mkdir(parents=True)
            (run / 'rounds.json').write_text(json.dumps([{'status': 'crashed'}] + [{}] * 29))
            call = root / 'model_calls/0001_a'
            call.mkdir(parents=True)
            request = call / 'request.json'
            request.write_text(json.dumps({'kind': 'editor', 'requested_model': 'sol',
                                          'caller_stack': [{'function': 'repair'}]}))
            os.utime(request, (1000, 1000))
            (root / 'live_status.json').write_text(json.dumps({'status': 'waiting_for_model',
                  'request': str(request), 'pid': 99999999}))
            stale = collect(root, now=1400, probe=lambda _: {'state': 'dead'})
            live = collect(root, now=1400, probe=lambda _: {'state': 'live'})
            self.assertEqual(stale['counts']['recorded_rounds'], 30)
            self.assertEqual(stale['counts']['crash_rounds_explicit'], 1)
            self.assertEqual(stale['counts']['editor_attempts'], 1)
            self.assertEqual(stale['counts']['accepted_calls'], 0)
            self.assertEqual(len(stale['inspection_flags']), 2)
            self.assertEqual(len(live['inspection_flags']), 1)
            self.assertIn('unknown', live['completion_audit'])
            self.assertEqual(live['generated_local'], '1970-01-01T08:23:20+08:00')
            self.assertEqual(live['generated_utc'], '1970-01-01T00:23:20+00:00')
            (call / 'response.json').write_text(json.dumps({'executed_model': 'sol', 'raw': 'ok'}))
            self.assertEqual(collect(root)['counts']['accepted_calls'], 0)
            (call / 'receipt.json').write_text(json.dumps({'executed_model': 'sol', 'raw_sha256': 'abc'}))
            accepted = collect(root, now=1400)
            self.assertEqual(accepted['counts']['accepted_calls'], 1)
            self.assertIn('reply accepted; runtime advancing', accepted['display_stage'])
            self.assertIsNone(accepted['pending_seconds'])
            atomic_write(root / 'index.html', render(live, root))
            self.assertIn('repair', (root / 'index.html').read_text(encoding='utf8'))

    def test_real_windows_process_and_redaction(self):
        if os.name == 'nt':
            self.assertEqual(process_liveness(os.getpid())['state'], 'live')
            self.assertEqual(process_liveness(99999999)['state'], 'dead')
        self.assertNotIn('private', redact('Authorization: Bearer private api_key=private sk-private'))


if __name__ == '__main__':
    unittest.main()
