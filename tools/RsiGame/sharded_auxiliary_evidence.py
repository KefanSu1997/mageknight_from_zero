"""Schema 3 intake for an actual delegated combat_focus_v3.1 FINAL.

Minimal manifest (descriptors contain path and sha256; paths relative to manifest):
{"schema":3,"review":{"path":"final/verification.json","sha256":"..."},
 "input":{"path":"final_packet/input.json","sha256":"..."},
 "feedback":{"path":"final/actual_final.txt","sha256":"..."},
 "routing":"controller_and_editor", "side_roles":{"A":"candidate","B":"baseline"},
 "source_bindings":{"D:/project/Assets/example.cs":"reviewed-source-sha256"},
 "recorded_mvid":"optional recorded value"}

Use run_unity_experiment.py --auxiliary-evidence MANIFEST with the normal
run arguments (or --check-only). This module creates no reviews or scores.
source_bindings/recorded_mvid are intake annotations, not proof of FINAL
reading additional evidence, and never prove the currently loaded Unity DLL.
"""
from __future__ import annotations
import json
import tempfile
from pathlib import Path
from auxiliary_evidence import AuxiliaryEvidence, digest
import verify_sharded_combat_review as verifier


class ShardedAuxiliaryEvidence(AuxiliaryEvidence):
    def __init__(self, manifest, project):
        self.path = Path(manifest).resolve()
        self.project = Path(project).resolve()
        self.raw = self.path.read_bytes()
        self.manifest = json.loads(self.raw)
        if self.manifest.get('schema') != 3:
            raise ValueError('Sharded actual FINAL intake requires schema 3')
        if self.manifest.get('routing', 'controller_and_editor') != 'controller_and_editor':
            raise ValueError('Schema 3 scores are restricted to controller_and_editor')
        self.manifest['routing'] = 'controller_and_editor'
        self.archive_mode = self.manifest.get('archive_mode', 'inline')
        if self.archive_mode not in {'inline', 'references_only'}:
            raise ValueError('Unknown archival context mode')
        self.sources, self.frozen, self.artifacts = {}, {}, {}
        self._raw_cache = {}
        for name in ('feedback', 'review', 'input'):
            self.artifacts[name] = self.read(name, self.manifest[name])
        proof = json.loads(self.artifacts['review'])
        packet = json.loads(self.artifacts['input'])
        if proof.get('verified') is not True or proof.get('model') != 'gpt-6.1-sol':
            raise ValueError('Actual verified Sol proof required')
        for key, name in (('input', 'input'), ('actual_final', 'feedback')):
            self.check_descriptor(proof[key], name)
        # Freeze all proof/packet/dispatch dependencies before the verifier replay.
        # Only transport documents are traversed; evidence JSON is data, whose
        # UI hierarchy paths must never be mistaken for filesystem descriptors.
        self.capture('final_transcript', proof['transcript'])
        dispatch = self.capture('run_manifest', packet['manifest'], parse=True)
        self.artifacts['run_manifest'] = json.dumps(dispatch, ensure_ascii=False)
        source = self.capture('source_input', dispatch['source_input'], parse=True)
        for owner, doc in (('source_input', source), ('input', packet)):
            for key in ('sp', 'rubric', 'score_schema'):
                self.capture(owner + '_' + key, {'path': doc[key + '_path'], 'sha256': doc[key + '_sha256']})
            for kind in ('required_images', 'required_text'):
                for i, descriptor in enumerate(doc[kind]):
                    self.capture(f'{owner}_{kind}_{i}', descriptor)
        for i, task in enumerate(dispatch['tasks']):
            for j, descriptor in enumerate(task['must_read'] + task['images']):
                self.capture(f'task_{i}_{j}', descriptor)
        for i, descriptor in enumerate(dispatch['final_context'] + dispatch['all_original_images'] + dispatch['all_original_text']):
            self.capture(f'dispatch_evidence_{i}', descriptor)
        for i, descriptor in enumerate(packet['team_proofs']):
            shard = self.capture(f'team_proof_{i}', descriptor, parse=True)
            for key in ('transcript', 'actual_final', 'manifest'):
                self.capture(f'team_{i}_{key}', shard[key])
        with tempfile.TemporaryDirectory(prefix='rsi_schema3_reverify_') as folder:
            replay = verifier.final_verify(self.sources['input']['path'],
                proof['transcript']['path'], proof['worker'], Path(folder) / 'final')
            raw = Path(replay['actual_final']['path']).read_bytes()
            if raw != self.frozen['feedback']:
                raise ValueError('Actual FINAL replay differs from intake raw feedback')
        self.artifacts['protocol'] = self.frozen['input_rubric'].decode('utf-8-sig')
        self.artifacts['system_prompt'] = self.frozen['input_sp'].decode('utf-8-sig')
        self.sources['protocol'] = self.sources['input_rubric']
        self.frozen['protocol'] = self.frozen['input_rubric']
        self.sources['system_prompt'] = self.sources['input_sp']
        self.frozen['system_prompt'] = self.frozen['input_sp']
        self.side_roles = self.manifest.get('side_roles')
        if 'side_roles' in self.manifest:
            if (not isinstance(self.side_roles, dict) or set(self.side_roles) != {'A', 'B'}
                    or sorted(str(v) for v in self.side_roles.values()) != ['baseline', 'candidate']):
                raise ValueError('side_roles requires exact A/B and candidate/baseline once each')
        self.candidate_side = (next(side for side, role in self.side_roles.items() if role == 'candidate')
                               if self.side_roles is not None else None)
        self.source_bindings = self.manifest.get('source_bindings', {})
        if not isinstance(self.source_bindings, dict):
            raise ValueError('source_bindings must map original absolute paths to SHA256')
        for path, sha in self.source_bindings.items():
            if not Path(path).is_absolute() or not isinstance(sha, str) or len(sha) != 64 or any(c not in '0123456789abcdef' for c in sha):
                raise ValueError('Invalid source binding path/SHA')
        self.receipt = {'runtimeModuleVersionId': self.manifest.get('recorded_mvid')}
        for name in ('iteration_policy', 'active_rubric', 'active_workflow'):
            if name in self.manifest:
                self.artifacts[name] = self.read(name, self.manifest[name])
        for name, descriptor in self.manifest.get('measurements', {}).items():
            key = 'supplemental_unreviewed_' + name
            self.artifacts[key] = self.read(key, descriptor)
        # Reject concurrent evidence changes before exposing any context.
        self.assert_unchanged()

    def read(self, name, descriptor, binary=False):
        # Original PNGs recur in source, task and dispatch descriptors. Retain
        # one immutable byte object per path rather than multiplying image RAM.
        path = (self.path.parent / descriptor['path']).resolve()
        if path not in self._raw_cache:
            self._raw_cache[path] = path.read_bytes()
        raw = self._raw_cache[path]
        if digest(raw) != descriptor['sha256']:
            raise ValueError('Auxiliary SHA mismatch: ' + name)
        if 'bytes' in descriptor and descriptor['bytes'] != len(raw):
            raise ValueError('Descriptor byte count mismatch: ' + name)
        self.sources[name] = {'path': str(path), 'sha256': descriptor['sha256']}
        self.frozen[name] = raw
        return raw if binary else raw.decode('utf-8-sig')

    def freeze(self, out):
        self.assert_unchanged()
        return super().freeze(out)

    def check_descriptor(self, descriptor, name):
        expected = self.sources[name]
        if (Path(descriptor['path']).resolve() != Path(expected['path'])
                or descriptor['sha256'] != expected['sha256']
                or ('bytes' in descriptor and descriptor['bytes'] != len(self.frozen[name]))):
            raise ValueError('Original proof descriptor mismatch: ' + name)

    def capture(self, name, descriptor, parse=False):
        raw = self.read(name, descriptor, binary=True)
        if 'bytes' in descriptor and descriptor['bytes'] != len(raw):
            raise ValueError('Descriptor byte count mismatch: ' + name)
        return json.loads(raw) if parse else raw

    def assert_unchanged(self):
        if self.path.read_bytes() != self.raw:
            raise ValueError('Intake manifest changed')
        checked = set()
        for name, descriptor in self.sources.items():
            identity = (descriptor['path'], descriptor['sha256'])
            if identity in checked:
                continue
            checked.add(identity)
            if digest(Path(descriptor['path']).read_bytes()) != descriptor['sha256']:
                raise ValueError('Evidence changed: ' + name)

    def classification(self):
        if not self.source_bindings:
            return 'historical_no_current_source_binding'
        for path, sha in self.source_bindings.items():
            if not Path(path).is_file() or digest(Path(path).read_bytes()) != sha:
                return 'historical_source_binding_stale'
        return 'scoped_current_source_files_match_recorded_not_live_verified'

    def payload(self):
        self.assert_unchanged()
        status = self.classification()
        inlined = ([] if self.archive_mode == 'references_only' else ['protocol', 'feedback']) + [k for k in self.artifacts if k.startswith('supplemental_unreviewed_')]
        text = ('\n[AUXILIARY VERIFIED EVIDENCE; supplemental context only]\n'
                'Preserve the original task and JSON schema. Status: ' + status + '.\n'
                'Scope: the original frozen two-side packet and complete verified team shard chain. '
                'Keep A/B all32 rows, nulls, missing reasons, hard failures, denominators, evidence limits and comparability unchanged. '
                'Never average, rescore or convert to a single-side legacy items list. Team original-image coverage differs from FINAL personal representative-image coverage. '
                'Verification establishes transport/schema/semantics, not judgment correctness or UI acceptance. '
                'These archived scores are not a fresh current-runtime evaluation. Source bindings and recorded MVID are intake annotations; '
                'file matches do not prove Unity has loaded those files. MVID is recorded_not_live_verified. '
                'Supplemental_unreviewed content was not independently certified by this FINAL.\n')
        if self.side_roles is None:
            text += ('\n[POST-REVIEW SIDE MAPPING]\nSide roles: unknown. '
                     'Do not automatically treat either blind side as the current candidate. '
                     'Source bindings alone do not identify a reviewed side.\n')
        else:
            text += ('\n[POST-REVIEW SIDE MAPPING]\n'
                     'operator_declared_post_review_mapping (not independently proved by FINAL): '
                     + json.dumps(self.side_roles, ensure_ascii=False)
                     + '. Candidate side: ' + self.candidate_side + '. '
                     'source_bindings refer to the operator-declared candidate current source files; '
                     'a stale binding makes the archived score historical. Mapping does not establish '
                     'that Unity loaded those files or that baseline problems persist in candidate.\n')
        for name in inlined:            text += '\n--- ' + name.upper() + ' (verbatim frozen input) ---\n' + self.artifacts[name] + '\n'
        text += '\n--- VALIDATED ARCHIVE DEPENDENCIES ---\nNot mandatory current-role reads.\n' + json.dumps(self.sources, ensure_ascii=False)
        sidecar = dict(schema=3, manifest_path=str(self.path), manifest_sha256=digest(self.raw),
            sources=self.sources, snapshot_status=status, source_bindings=self.source_bindings,
            side_roles=self.side_roles if self.side_roles is not None else 'unknown',
            candidate_side=self.candidate_side if self.candidate_side is not None else 'unknown',
            side_mapping_authority='operator_declared_post_review_mapping' if self.side_roles is not None else 'unknown',
            source_bindings_role='candidate' if self.side_roles is not None else 'unknown',
            recorded_MVID=self.receipt['runtimeModuleVersionId'], runtime_freshness='recorded_not_live_verified',
            inlined_artifacts=inlined, dependency_only_names=[k for k in self.sources if k not in inlined],
            archive_mode=self.archive_mode, context_sha256=digest(text.encode()))
        return text, sidecar
