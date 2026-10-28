"""Synthetic intake transport fixtures; these are never actual UI reviews."""
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
from build_sharded_combat_review import IDS, entry, write
from auxiliary_evidence import load_auxiliary_evidence
from auxiliary_evidence_queue import AuxiliaryQueue
from run_luna_bridge_experiment import write as queue_write
import verify_sharded_combat_review as verifier


class ShardedIntakeTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        def file(name, value):
            return write(self.root / name, value)
        self.file = file
        r = file('rubric.json', {'items': [{'id': i} for i in IDS]})
        s = file('score_schema.json', {'type': 'object'})
        sp = file('sp.json', {'instruction': 'synthetic transport only'})
        fs = file('facts_schema.json', {'type': 'object'})
        scope = file('scope.json', {'scope': 'synthetic fixture only'})
        original = dict(required_images=[], required_text=[scope])
        for name, e in [('sp', sp), ('rubric', r), ('score_schema', s)]:
            original[name + '_path'] = e['path']
            original[name + '_sha256'] = e['sha256']
        src = file('source_input.json', original)
        ti = file('task_input.json', {'shard_id': 'text_01'})
        task = dict(shard_id='text_01', kind='text', input=ti,
                    must_read=[ti, r, sp, fs, scope], images=[])
        dispatch = dict(protocol_version='combat_focus_v3.1.0', source_scope='synthetic',
                        source_input=src, tasks=[task], final_context=[r, s, sp, scope],
                        representative_images=[], all_original_images=[], all_original_text=[scope])
        dm = file('dispatch.json', dispatch)
        facts = dict(shard_id='text_01', read_ledger=[dict(e, actually_read_or_viewed=True) for e in task['must_read']],
                     coverage=[dict(path=scope['path'], item_ids=IDS)],
                     item_facts=[dict(id=i) for i in IDS], limitations=['synthetic'])
        trace = self.trace('reader', facts, task['must_read'])
        verifier.persist(dm['path'], 'text_01', trace, '/synthetic/reader', self.root / 'shard')
        packet = verifier.assemble(dm['path'], [self.root / 'shard/verification.json'], self.root / 'assembled')
        final = dict(rubric_version=packet['rubric_version'], gate_verdict='unverified',
                     sides={side: [dict(id=i, score=None, status='unverified', reason='synthetic gap', missing_reasons=['unknown']) for i in IDS] for side in ('A', 'B')},
                     evidence_method=dict(personal_direct_view_scope=[], team_coverage=['text_01']),
                     hard_failures=['preserved synthetic hard failure'])
        final['gate_verdict'] = 'failed'
        required = [entry(self.root / 'assembled/input.json'), sp, r, s] + packet['required_text']
        trace = self.trace('judge', final, required)
        verifier.final_verify(self.root / 'assembled/input.json', trace, '/synthetic/judge', self.root / 'final')
        self.manifest = dict(schema=3, review=entry(self.root / 'final/verification.json'),
                             input=entry(self.root / 'assembled/input.json'), feedback=entry(self.root / 'final/actual_final.txt'))
        self.save()

    def trace(self, worker, final, descriptors):
        rows = [dict(type='session_meta', payload={'agent_path': '/synthetic/' + worker}),
                dict(type='turn_context', payload={'model': 'gpt-6.1-sol'})]
        rows += [dict(payload=dict(type='function_call_output', output=Path(e['path']).read_text(encoding='utf8'))) for e in descriptors]
        rows += [dict(payload=dict(role='assistant', type='message', phase='final', content=[dict(type='output_text', text=json.dumps(final))]))]
        path = self.root / (worker + '.jsonl')
        path.write_text('\n'.join(json.dumps(r) for r in rows), encoding='utf8')
        return path

    def save(self):
        self.path = self.root / 'intake.json'
        write(self.path, self.manifest)

    def load(self):
        return load_auxiliary_evidence(self.path, self.root)

    def test_replay_raw_both_sides_and_freeze(self):
        evidence = self.load()
        text, sidecar = evidence.payload()
        self.assertIn(evidence.artifacts['feedback'], text)
        self.assertEqual({'A', 'B'}, set(json.loads(evidence.artifacts['feedback'])['sides']))
        self.assertIn('preserved synthetic hard failure', text)
        self.assertIn('null', text)
        self.assertEqual(3, sidecar['schema'])
        evidence.freeze(self.root / 'frozen')

    def test_changed_shard_trace_rejected_even_if_verified(self):
        with (self.root / 'reader.jsonl').open('a') as f:
            f.write('\n{}')
        with self.assertRaises(ValueError): self.load()

    def test_incomplete_team_chain_rejected_despite_rebound_proof(self):
        packet = json.loads((self.root / 'assembled/input.json').read_bytes())
        packet['team_proofs'] = []
        self.manifest['input'] = self.file('assembled/input.json', packet)
        proof = json.loads((self.root / 'final/verification.json').read_bytes())
        proof['input'] = self.manifest['input']
        self.manifest['review'] = self.file('final/verification.json', proof)
        self.save()
        with self.assertRaisesRegex(ValueError, 'Incomplete team coverage'): self.load()

    def test_evidence_or_manifest_changed_after_load_rejected(self):
        evidence = self.load()
        self.path.write_text('{}')
        with self.assertRaisesRegex(ValueError, 'manifest changed'): evidence.freeze(self.root / 'frozen')
        with self.assertRaisesRegex(ValueError, 'manifest changed'): evidence.payload()

    def test_missing_shard_rejected(self):
        (self.root / 'shard/actual_final.txt').unlink()
        with self.assertRaises(FileNotFoundError): self.load()

    def test_changed_source_evidence_rejected(self):
        (self.root / 'scope.json').write_text('{}')
        with self.assertRaises(ValueError): self.load()

    def test_feedback_descriptor_cannot_replace_actual_final(self):
        self.manifest['feedback'] = self.file('fake.json', {})
        self.save()
        with self.assertRaisesRegex(ValueError, 'descriptor mismatch'): self.load()

    def test_source_changes_mark_history_on_every_payload(self):
        source = self.root / 'current.cs'
        source.write_text('old')
        self.manifest.update(source_bindings={str(source): entry(source)['sha256']}, recorded_mvid='recorded-only')
        self.save()
        evidence = self.load()
        self.assertIn('scoped_current_source_files_match', evidence.payload()[1]['snapshot_status'])
        source.write_text('changed')
        self.assertEqual('historical_source_binding_stale', evidence.payload()[1]['snapshot_status'])
        self.assertEqual('recorded_not_live_verified', evidence.payload()[1]['runtime_freshness'])

    def test_scores_routed_only_controller_editor_policy_all_roles(self):
        self.manifest['side_roles'] = {'A': 'candidate', 'B': 'baseline'}
        self.manifest['iteration_policy'] = self.file('policy.json', {'design': 'current policy'})
        self.save()
        evidence = self.load()
        queue = AuxiliaryQueue(self.root / 'queue', evidence)
        self.assertTrue(queue.include_context('reader', [{'file': '/rsigame/controller/plan.py'}]))
        self.assertTrue(queue.include_context('editor', []))
        self.assertFalse(queue.include_context('reader', []))
        messages = [{'role': 'user', 'content': 'original'}]
        def answer(path, value):
            queue_write(path, value)
            if path.name == 'request.json':
                queue_write(path.parent / 'response.json', {'raw': 'answer', 'executed_model': 'gpt-6.1-sol'})
        for kind in ('reader', 'editor'):
            with patch('auxiliary_evidence_queue.write', side_effect=answer):
                queue.ask(messages, kind=kind)
        calls = sorted((self.root / 'queue/model_calls').iterdir())
        for call, kind in zip(calls, ('reader', 'editor')):
            enhanced = json.loads((call / 'enhanced_messages.json').read_text())
            self.assertEqual(messages, json.loads((call / 'original_messages.json').read_text()))
            self.assertIn('CURRENT USER-REQUESTED', enhanced[-1]['content'])
            joined = json.dumps(enhanced)
            self.assertEqual(kind == 'editor', 'operator_declared_post_review_mapping' in joined)
            self.assertEqual(kind == 'editor', 'Candidate side: A' in joined)
            self.assertEqual(kind == 'editor', evidence.artifacts['feedback'] in '\n'.join(m['content'] for m in enhanced))

    def test_unknown_side_roles_prohibit_automatic_current_side(self):
        text, sidecar = self.load().payload()
        self.assertEqual('unknown', sidecar['side_roles'])
        self.assertEqual('unknown', sidecar['candidate_side'])
        self.assertIn('Do not automatically treat either blind side as the current candidate', text)

    def test_invalid_or_duplicate_side_roles_rejected(self):
        for value in (None, [], {}, {'A': 'candidate'}, {'A': 'candidate', 'B': 'candidate'},
                      {'A': 'baseline', 'B': 'baseline'}, {'A': 'candidate', 'C': 'baseline'},
                      {'A': 'current', 'B': 'baseline'}, {'A': 'candidate', 'B': ['baseline']}):
            with self.subTest(side_roles=value):
                self.manifest['side_roles'] = value
                self.save()
                with self.assertRaisesRegex(ValueError, 'side_roles'): self.load()

    def test_legal_side_mapping_does_not_rewrite_raw_final(self):
        self.manifest['side_roles'] = {'A': 'baseline', 'B': 'candidate'}
        self.save()
        evidence = self.load()
        context, sidecar = evidence.payload()
        self.assertEqual('B', sidecar['candidate_side'])
        self.assertEqual('operator_declared_post_review_mapping', sidecar['side_mapping_authority'])
        self.assertEqual('candidate', sidecar['source_bindings_role'])
        self.assertIn('Candidate side: B', context)
        self.assertEqual((self.root / 'final/actual_final.txt').read_bytes(), evidence.frozen['feedback'])
        queue = AuxiliaryQueue(self.root / 'mapping_queue', evidence)
        archive = self.root / 'archive.json'
        compact = queue.compact_role_context(context, sidecar, archive)
        self.assertIn('Candidate side: B', compact)
        self.assertTrue(queue.include_context('reader', [{'file': '/rsigame/controller/plan.py'}]))
        self.assertFalse(queue.include_context('reader', []))

    def test_all_roles_score_routing_forbidden(self):
        self.manifest['routing'] = 'all_roles'
        self.save()
        with self.assertRaises(ValueError): self.load()


if __name__ == '__main__':
    unittest.main()
