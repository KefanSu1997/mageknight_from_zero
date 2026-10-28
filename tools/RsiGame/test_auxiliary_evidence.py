import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from auxiliary_evidence import AuxiliaryEvidence
from auxiliary_evidence_queue import AuxiliaryQueue
from run_luna_bridge_experiment import Queue, write as original_write


class AuxiliaryTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.project = self.root / 'project'
        self.project.mkdir()
        self.manifest = {'schema': 2}
        ids = [f'{g}.{i}' for g in range(1,9) for i in range(1,5)]
        protocol = {'scores': {'1':'low','5':'high'}, 'items': [dict(id=i,name='criterion',method='method') for i in ids]}
        self.manifest['protocol'] = self.file('protocol', protocol)
        self.manifest['system_prompt'] = self.file('system_prompt', protocol)
        backup = self.file('backup', {'source':'old'})
        self.manifest['source_manifest'] = self.file('source_manifest', {'Assets/test.cs': {'backup': str(self.root / backup['path']), 'sha256': backup['sha256']}})
        self.manifest['compile_receipt'] = self.file('compile_receipt', {'success':True,'runtimeModuleVersionId':'known'})
        required_text = [{**self.manifest[k], 'path': str(self.root / self.manifest[k]['path'])} for k in ('source_manifest','compile_receipt')]
        packet = {'round':1,'sp_path':str(self.root / self.manifest['system_prompt']['path']), 'sp_sha256':self.manifest['system_prompt']['sha256'], 'required_text':required_text,'required_images':[]}
        self.manifest['input'] = self.file('input', packet)
        self.manifest['run_manifest'] = self.file('run_manifest', {'input_sha256':self.manifest['input']['sha256'],'protocol_sha256':self.manifest['protocol']['sha256'],'sp_sha256':packet['sp_sha256']})
        final = {'round':1,'sp_sha256':packet['sp_sha256'],'input_sha256':self.manifest['input']['sha256'],'gate_verdict':'partial','items':[dict(id=i,score=None,status='unverified',reason='missing evidence') for i in ids]}
        self.manifest['feedback'] = self.file('feedback', final)
        output = '\n'.join((self.root / self.manifest[k]['path']).read_text() for k in ('input','system_prompt','source_manifest','compile_receipt'))
        records = [{'type':'turn_context','payload':{'model':'gpt-6.1-sol'}}, {'payload':{'type':'function_call_output','output':output}}, {'payload':{'type':'message','role':'assistant','phase':'final','content':[{'type':'output_text','text':(self.root / 'feedback.json').read_text()}]}}]
        transcript = self.root / 'transcript.jsonl'
        transcript.write_text('\n'.join(json.dumps(x) for x in records))
        self.review = {'verified':True,'errors':[],'worker':'/root/independent','model':['gpt-6.1-sol'],'transcript':str(transcript),'transcript_sha256':hashlib.sha256(transcript.read_bytes()).hexdigest(),'input_sha256':self.manifest['input']['sha256'],'actual_final_sha256':self.manifest['feedback']['sha256'],'required_unique_images':0,'byte_verified_images':0,'actual_image_blocks':0}
        self.save()

    def file(self, name, value):
        raw = json.dumps(value).encode()
        path = self.root / (name + '.json')
        path.write_bytes(raw)
        return {'path': path.name, 'sha256': hashlib.sha256(raw).hexdigest()}

    def save(self):
        self.manifest['review'] = self.file('review', self.review)
        self.path = self.root / 'manifest.json'
        self.path.write_text(json.dumps(self.manifest))

    def load(self):
        return AuxiliaryEvidence(self.path, self.project)

    def rebind_prompt(self, raw, protocol):
        """Recreate the actual packet/proof chain without normalizing SP bytes."""
        self.manifest['protocol'] = self.file('protocol', protocol)
        path = self.root / 'system_prompt.json'
        path.write_bytes(raw)
        self.manifest['system_prompt'] = {'path': path.name, 'sha256': hashlib.sha256(raw).hexdigest()}
        packet = json.loads((self.root / 'input.json').read_bytes())
        packet['sp_sha256'] = self.manifest['system_prompt']['sha256']
        self.manifest['input'] = self.file('input', packet)
        self.manifest['run_manifest'] = self.file('run_manifest', {
            'input_sha256': self.manifest['input']['sha256'],
            'protocol_sha256': self.manifest['protocol']['sha256'],
            'sp_sha256': packet['sp_sha256']})
        final = json.loads((self.root / 'feedback.json').read_bytes())
        final.update(sp_sha256=packet['sp_sha256'], input_sha256=self.manifest['input']['sha256'])
        self.manifest['feedback'] = self.file('feedback', final)
        # Delivery can contain CRLF verbatim; helper checks delivery and comparison
        # with normalized line endings while preserving SHA over the original bytes.
        output = '\n'.join((self.root / self.manifest[k]['path']).read_bytes().decode()
                           for k in ('input', 'system_prompt', 'source_manifest', 'compile_receipt'))
        records = [{'type': 'turn_context', 'payload': {'model': 'gpt-6.1-sol'}},
                   {'payload': {'type': 'function_call_output', 'output': output}},
                   {'payload': {'type': 'message', 'role': 'assistant', 'phase': 'final',
                                'content': [{'type': 'output_text', 'text': (self.root / 'feedback.json').read_bytes().decode()}]}}]
        transcript = self.root / 'transcript.jsonl'
        transcript.write_bytes('\n'.join(json.dumps(x) for x in records).encode())
        self.review.update(transcript_sha256=hashlib.sha256(transcript.read_bytes()).hexdigest(),
                           input_sha256=self.manifest['input']['sha256'],
                           actual_final_sha256=self.manifest['feedback']['sha256'])
        self.save()

    def test_multiline_crlf_prompt_preserves_raw_hash(self):
        protocol = json.loads((self.root / 'protocol.json').read_bytes())
        raw = ('Actual rubric follows:\n' + json.dumps(protocol, ensure_ascii=False, indent=2)
               + '\nPreserve the requested schema.\n').replace('\n', '\r\n').encode()
        self.rebind_prompt(raw, protocol)
        evidence = self.load()
        self.assertEqual(raw, evidence.frozen['system_prompt'])
        self.assertEqual(hashlib.sha256(raw).hexdigest(), evidence.sources['system_prompt']['sha256'])
        self.assertNotEqual(hashlib.sha256(raw.replace(b'\r\n', b'\n')).hexdigest(), evidence.sources['system_prompt']['sha256'])

    def test_actual_cycle01_crlf_system_prompt(self):
        fixture = Path(__file__).resolve().parents[2] / 'AutomationOutputs/RsiGame/evaluation_iterations/20261023_cycle01/review_v3'
        if not (fixture / 'system_prompt.txt').is_file():
            self.skipTest('Historical cycle01 fixture unavailable')
        raw = (fixture / 'system_prompt.txt').read_bytes()
        self.assertIn(b'\r\n', raw)
        self.rebind_prompt(raw, json.loads((fixture / 'protocol.json').read_bytes()))
        self.assertEqual(raw, self.load().frozen['system_prompt'])

    def test_protocol_mismatch_still_rejected_with_crlf(self):
        protocol = json.loads((self.root / 'protocol.json').read_bytes())
        other = json.loads(json.dumps(protocol))
        other['items'][0]['method'] = 'Different actual rubric method'
        raw = json.dumps(other, ensure_ascii=False, indent=2).replace('\n', '\r\n').encode()
        self.rebind_prompt(raw, protocol)
        with self.assertRaisesRegex(ValueError, 'actual packet system-prompt rubric'):
            self.load()

    def test_missing(self):
        (self.root / 'feedback.json').unlink()
        with self.assertRaises(FileNotFoundError): self.load()

    def test_sha(self):
        (self.root / 'compile_receipt.json').write_text('{}')
        with self.assertRaisesRegex(ValueError, 'SHA'): self.load()

    def test_review_identity(self):
        self.review['model'] = ['other-model']
        self.save()
        with self.assertRaisesRegex(ValueError, 'independent'): self.load()

    def test_review_binding(self):
        self.review['actual_final_sha256'] = '0' * 64
        self.save()
        with self.assertRaisesRegex(ValueError, 'hash'): self.load()

    def test_evidence_sha(self):
        (self.root / 'backup.json').write_text('{}')
        with self.assertRaisesRegex(ValueError, 'SHA'): self.load()

    def test_historical_null_and_freeze(self):
        evidence = self.load()
        context, sidecar = evidence.payload()
        self.assertEqual('historical', sidecar['snapshot_status'])
        self.assertIn('null', context)
        evidence.freeze(self.root / 'out')
        (self.project / 'Assets').mkdir()
        (self.project / 'Assets/test.cs').write_bytes((self.root / 'backup.json').read_bytes())
        self.assertEqual('historical', evidence.classification())  # no reviewed DLL hash

    def test_compact_context_inlines_only_protocol_and_raw_final(self):
        evidence = self.load()
        context, sidecar = evidence.payload()
        self.assertEqual(['protocol', 'feedback'], sidecar['inlined_artifacts'])
        self.assertIn('system_prompt', sidecar['dependency_only_names'])
        self.assertIn('required_text_0', sidecar['dependency_only_names'])
        self.assertIn(evidence.artifacts['protocol'], context)
        self.assertIn(evidence.artifacts['feedback'], context)
        self.assertNotIn('--- SYSTEM_PROMPT ', context)
        self.assertNotIn('--- REQUIRED_TEXT_0 ', context)
        self.assertNotIn(evidence.artifacts['source_manifest'], context)
        self.assertEqual(1, context.count(evidence.artifacts['protocol']))
        self.assertEqual(32, len(json.loads(evidence.artifacts['protocol'])['items']))

    def test_queue_originals_and_default(self):
        messages = [{'role': 'user', 'content': 'original schema'}]
        def answer(request_path, value):
            original_write(request_path, value)
            if request_path.name == 'request.json':
                original_write(request_path.parent / 'response.json', {'raw': 'answer', 'executed_model': 'gpt-6.1-sol'})
        for cls, enhanced in [(Queue, False), (AuxiliaryQueue, True)]:
            out = self.root / cls.__name__
            queue = cls(out, self.load()) if enhanced else cls(out)
            module = 'auxiliary_evidence_queue' if enhanced else 'run_luna_bridge_experiment'
            with patch(module + '.write', side_effect=answer):
                self.assertEqual('answer', queue.ask(messages))
            call = next((out / 'model_calls').iterdir())
            self.assertEqual(messages, json.loads((call / 'original_messages.json').read_text()))
            self.assertEqual(enhanced, (call / 'enhanced_messages.json').exists())
            if enhanced:
                self.assertIn('AUXILIARY VERIFIED', (call / 'messages.txt').read_text())

    def test_current_policy_digest_and_freeze(self):
        self.manifest['iteration_policy'] = self.file('iteration_policy', {'user': 'combat focus'})
        self.save()
        evidence = self.load()
        self.assertIn('iteration_policy', evidence.frozen)
        evidence.freeze(self.root / 'policy_out')
        (self.root / 'iteration_policy.json').write_text('changed')
        with self.assertRaisesRegex(ValueError, 'SHA'):
            self.load()

    def test_reference_only_omits_archival_scores(self):
        self.manifest['archive_mode'] = 'references_only'
        self.save()
        evidence = self.load()
        context, sidecar = evidence.payload()
        self.assertEqual([], sidecar['inlined_artifacts'])
        self.assertNotIn('--- PROTOCOL ', context)
        self.assertNotIn('--- FEEDBACK ', context)
        self.assertIn('feedback', sidecar['dependency_only_names'])

    def test_bad_archive_mode_rejected(self):
        self.manifest['archive_mode'] = 'discard_history'
        self.save()
        with self.assertRaisesRegex(ValueError, 'archival context mode'):
            self.load()

    def test_non_controller_gets_current_policy_without_archive(self):
        self.manifest.update(routing='controller_and_editor', archive_mode='references_only')
        self.manifest['iteration_policy'] = self.file('iteration_policy', {'current_user': 'Wiki and log'})
        self.manifest['active_rubric'] = self.file('active_rubric', {'version': 'v3'})
        self.manifest['active_workflow'] = self.file('active_workflow', {'version': 'v3'})
        self.save()
        messages = [{'role': 'user', 'content': 'original reader JSON contract'}]
        out = self.root / 'current_policy_queue'
        queue = AuxiliaryQueue(out, self.load())
        def answer(path, value):
            original_write(path, value)
            if path.name == 'request.json':
                original_write(path.parent / 'response.json', {'raw': 'actual reader', 'executed_model': 'gpt-6.1-sol'})
        with patch('auxiliary_evidence_queue.write', side_effect=answer):
            self.assertEqual('actual reader', queue.ask(messages))
        call = next((out / 'model_calls').iterdir())
        self.assertEqual(messages, json.loads((call / 'original_messages.json').read_text()))
        enhanced = json.loads((call / 'enhanced_messages.json').read_text())
        self.assertEqual(2, len(enhanced))
        self.assertIn('CURRENT USER-REQUESTED', enhanced[-1]['content'])
        self.assertIn('Wiki and log', enhanced[-1]['content'])
        sidecar = json.loads((call / 'auxiliary_evidence.json').read_text())
        self.assertFalse(sidecar['context_inlined'])
        self.assertTrue(sidecar['iteration_policy_inlined'])
        self.assertEqual(self.manifest['iteration_policy']['sha256'], sidecar['iteration_policy_sha256'])


if __name__ == '__main__':
    unittest.main()
