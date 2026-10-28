"""Synthetic fixtures test transport only, never constitute model/UI review."""
import base64
import copy
import json
import tempfile
import unittest
from pathlib import Path

from persist_combat_focus_review import digest, persist


class PersistenceTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        repo = Path(__file__).resolve().parents[2]
        config = repo / 'AutomationConfigs/RsiGame/evaluation/v3_combat_focus'
        rubric = json.loads((config / 'rubric.json').read_text(encoding='utf-8-sig'))
        self.packet = {'rubric_version': rubric['rubric_version'], 'required_images': [], 'required_text': []}
        for name, filename in [('sp', 'judge_system.txt'), ('rubric', 'rubric.json'), ('score_schema', 'score_schema.json')]:
            path = config / filename
            self.packet[name + '_path'] = str(path)
            self.packet[name + '_sha256'] = digest(path.read_bytes())
        image = self.root / 'raw.png'
        image.write_bytes(base64.b64decode('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jB9kAAAAASUVORK5CYII='))
        text = self.root / 'evidence.txt'
        text.write_text('Full required text\nincluding tail.', encoding='utf8')
        for kind, path in [('required_images', image), ('required_text', text)]:
            self.packet[kind].append({'path': str(path), 'sha256': digest(path.read_bytes())})
        self.input = self.root / 'input.json'
        self.input.write_text(json.dumps(self.packet), encoding='utf8')
        row = dict(score=None, status='unverified', raw_metrics=[], visual_observations=[], denominator=None,
                   missing_reasons=['missing_dynamic_evidence'], unit_results=[], reason='Not measured', evidence=[])
        rows = [dict(copy.deepcopy(row), id=item['id']) for item in rubric['items']]
        self.final = dict(rubric_version=self.packet['rubric_version'], input_status='valid', read_ledger=[],
                          blind_review={'preserved': True, 'limitations': []}, sides={'A': rows, 'B': copy.deepcopy(rows)},
                          hard_failures=[], unverified_gates=['dynamic'], gate_verdict='unverified',
                          overall_numeric_score=None, paired_comparison=[], scope_limits=['Synthetic transport fixture'])

    def run_fixture(self, final=True, model='gpt-6.1-sol', omit=False, worker='/root/judge', omit_image=False):
        records = [{'type': 'session_meta', 'payload': {'agent_path': '/root/judge'}},
                   {'type': 'turn_context', 'payload': {'model': model}}]
        paths = [self.input] + [Path(self.packet[n + '_path']) for n in ('sp', 'rubric', 'score_schema')]
        paths += [Path(e['path']) for e in self.packet['required_text']]
        for p in paths[:-1] if omit else paths:
            records.append({'payload': {'type': 'function_call_output', 'output': p.read_text(encoding='utf-8-sig')}})
        image = Path(self.packet['required_images'][0]['path']).read_bytes()
        if not omit_image:
            records.append({'payload': {'type': 'function_call_output', 'output': [{'type': 'input_image',
                            'image_url': 'data:image/png;base64,' + base64.b64encode(image).decode()}]}})
        self.raw = ' \n' + json.dumps(self.final, ensure_ascii=False, indent=1) + '\n'
        if final:
            records.append({'payload': {'type': 'message', 'role': 'assistant', 'phase': 'final',
                                       'content': [{'type': 'output_text', 'text': self.raw}]}})
        trace = self.root / 'trace.jsonl'
        trace.write_text('\n'.join(json.dumps(r) for r in records), encoding='utf8')
        self.out = self.root / 'out'
        return persist(self.input, trace, worker, self.out)

    def test_exact_actual_final(self):
        self.assertTrue(self.run_fixture()['verified'])
        self.assertEqual((self.out / 'actual_final.txt').read_bytes(), self.raw.encode())
        self.assertEqual((self.out / 'review.json').read_bytes(), self.raw.encode())

    def test_no_final_no_draft(self):
        with self.assertRaisesRegex(ValueError, 'No actual FINAL'):
            self.run_fixture(final=False)
        self.assertFalse((self.root / 'out').exists())

    def test_wrong_model(self):
        self.assertFalse(self.run_fixture(model='other')['verified'])
        self.assertFalse((self.out / 'review.json').exists())

    def test_wrong_worker(self):
        self.assertFalse(self.run_fixture(worker='/root/other')['verified'])

    def test_missing_delivery(self):
        self.assertFalse(self.run_fixture(omit=True)['verified'])

    def test_png_bytes_required(self):
        self.assertFalse(self.run_fixture(omit_image=True)['verified'])

    def test_changed_png(self):
        path = Path(self.packet['required_images'][0]['path'])
        path.write_bytes(path.read_bytes() + b'changed')
        self.assertFalse(self.run_fixture()['verified'])

    def test_changed_evidence(self):
        Path(self.packet['required_text'][0]['path']).write_text('changed', encoding='utf8')
        self.assertFalse(self.run_fixture()['verified'])

    def test_missing_evidence(self):
        missing = self.root / 'missing.txt'
        self.packet['required_text'].append({'path': str(missing), 'sha256': '0' * 64})
        self.input.write_text(json.dumps(self.packet), encoding='utf8')
        # Deliver existing text while manifest still requires the missing file.
        self.packet['required_text'].pop()
        self.assertFalse(self.run_fixture()['verified'])

    def test_missing_id(self):
        self.final['sides']['A'].pop()
        self.assertFalse(self.run_fixture()['verified'])

    def test_new_density_item_required(self):
        self.final['sides']['A'] = [x for x in self.final['sides']['A'] if x['id'] != '4.5']
        self.assertFalse(self.run_fixture()['verified'])

    def test_duplicate_id(self):
        self.final['sides']['B'][0]['id'] = '1.2'
        self.assertFalse(self.run_fixture()['verified'])

    def test_hard_failure_cannot_pass(self):
        self.final['hard_failures'] = [{'side': 'A', 'item_id': '1.1', 'reason': 'actual failure', 'evidence': ['raw.png']}]
        self.final['gate_verdict'] = 'passed_observed_scope'
        self.assertFalse(self.run_fixture()['verified'])

    def test_input_invalid_cannot_pass(self):
        self.final['input_status'] = 'input_invalid'
        self.final['gate_verdict'] = 'passed_observed_scope'
        self.assertFalse(self.run_fixture()['verified'])

    def test_missing_evidence_cannot_score(self):
        self.final['sides']['A'][0]['score'] = 5
        self.assertFalse(self.run_fixture()['verified'])


if __name__ == '__main__':
    unittest.main()
