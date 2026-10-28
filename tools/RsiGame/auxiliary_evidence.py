"""Schema 2 consumes actual persist_retrospective_review proof, never a new attestation.

Manifest descriptors {path, sha256}: feedback, review, input, run_manifest,
protocol, system_prompt, source_manifest, compile_receipt. Paths are relative
 to this manifest. source_manifest and compile_receipt must be required_text
in the reviewed packet. Optional measurements is a mapping of names to descriptors;
these are explicitly supplemental/unreviewed, unless independently present in packet.
"""
from __future__ import annotations
import base64
import hashlib
import json
from pathlib import Path
from persist_retrospective_review import delivered
from verify_reader_delivery import text_parts


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


class AuxiliaryEvidence:
    def __init__(self, manifest, project):
        self.path = Path(manifest).resolve()
        self.project = Path(project).resolve()
        self.raw = self.path.read_bytes()
        self.manifest = json.loads(self.raw)
        if self.manifest.get('schema') != 2:
            raise ValueError('Actual independent review manifest requires schema 2')
        self.sources, self.frozen, self.artifacts = {}, {}, {}
        self.archive_mode = self.manifest.get('archive_mode', 'inline')
        if self.archive_mode not in {'inline', 'references_only'}:
            raise ValueError('Unknown archival context mode')
        for name in ('iteration_policy', 'active_rubric', 'active_workflow'):
            if name in self.manifest:
                self.artifacts[name] = self.read(name, self.manifest[name])
        for name in ('feedback', 'review', 'input', 'run_manifest', 'protocol',
                     'system_prompt', 'source_manifest', 'compile_receipt'):
            self.artifacts[name] = self.read(name, self.manifest[name])
        proof = json.loads(self.artifacts['review'])
        packet = json.loads(self.artifacts['input'])
        frozen = json.loads(self.artifacts['run_manifest'])
        protocol = json.loads(self.artifacts['protocol'])
        final = json.loads(self.artifacts['feedback'])
        representations = (json.dumps(protocol, ensure_ascii=False, indent=2),
                           json.dumps(protocol), json.dumps(protocol, ensure_ascii=False))
        # Compare the embedded JSON after newline normalization only. Raw SP
        # bytes and their recorded SHA remain authoritative and unchanged.
        prompt_for_comparison = self.artifacts['system_prompt'].replace('\r\n', '\n')
        if not any(value in prompt_for_comparison for value in representations):
            raise ValueError('Frozen protocol is not the actual packet system-prompt rubric')
        if (proof.get('verified') is not True or proof.get('errors') != []
                or proof.get('model') != ['gpt-6.1-sol'] or not proof.get('worker')):
            raise ValueError('Actual verified independent worker/model proof required')
        for key in ('input', 'protocol'):
            if frozen.get(key + '_sha256') != self.sources[key]['sha256']:
                raise ValueError('Frozen run manifest mismatch: ' + key)
        sp_sha = self.sources['system_prompt']['sha256']
        input_sha = self.sources['input']['sha256']
        if (frozen.get('sp_sha256') != sp_sha or packet.get('sp_sha256') != sp_sha
                or final.get('sp_sha256') != sp_sha or final.get('input_sha256') != input_sha
                or proof.get('input_sha256') != input_sha
                or proof.get('actual_final_sha256') != self.sources['feedback']['sha256']):
            raise ValueError('Actual final/input/SP hash mismatch')
        if Path(packet['sp_path']).resolve() != Path(self.sources['system_prompt']['path']):
            raise ValueError('Packet SP path mismatch')
        ids = [f'{g}.{i}' for g in range(1, 9) for i in range(1, 5)]
        if ([x.get('id') for x in protocol.get('items', [])] != ids
                or [x.get('id') for x in final.get('items', [])] != ids
                or not protocol.get('scores')
                or any(not x.get('name') or not x.get('method') for x in protocol['items'])
                or final.get('round') != packet.get('round') or not final.get('gate_verdict')):
            raise ValueError('Actual full32 protocol/final schema mismatch')
        for item in final['items']:
            score = item.get('score')
            if (score is not None and (type(score) is not int or not 1 <= score <= 5)) or (item.get('status') == 'unverified' and score is not None) or not item.get('reason'):
                raise ValueError('Invalid actual final item')
        required_paths = set()
        images = set()
        for kind in ('required_images', 'required_text'):
            for i, descriptor in enumerate(packet[kind]):
                name = f'{kind}_{i}'
                value = self.read(name, descriptor, binary=kind == 'required_images')
                required_paths.add(Path(self.sources[name]['path']))
                if kind == 'required_images':
                    images.add(descriptor['sha256'])
                else:
                    self.artifacts[name] = value
        for name in ('source_manifest', 'compile_receipt'):
            if Path(self.sources[name]['path']) not in required_paths:
                raise ValueError('Snapshot authority was not reviewed: ' + name)
        if (proof.get('required_unique_images') != len(images)
                or proof.get('byte_verified_images') != len(images)
                or proof.get('actual_image_blocks', 0) < len(images)):
            raise ValueError('Incomplete actual image verification')
        transcript = self.read('transcript', {'path': proof['transcript'], 'sha256': proof['transcript_sha256']})
        models, finals, outputs, rendered = set(), [], [], set()
        for line in transcript.splitlines():
            record = json.loads(line)
            item = record.get('payload', {})
            if record.get('type') == 'turn_context':
                models.add(item.get('model'))
            if item.get('type') == 'message' and item.get('role') == 'assistant' and item.get('phase') in ('final', 'final_answer'):
                finals.append(''.join(x.get('text', '') for x in item.get('content', []) if x.get('type') == 'output_text'))
            if item.get('type') in ('custom_tool_call_output', 'function_call_output'):
                output = item.get('output', '')
                outputs.append(text_parts(output).replace('\r\n', '\n'))
                if isinstance(output, list):
                    for block in output:
                        if isinstance(block, dict) and block.get('type') == 'input_image':
                            url = block.get('image_url', '')
                            if isinstance(url, str) and url.startswith('data:') and ';base64,' in url:
                                rendered.add(digest(base64.b64decode(url.split(',', 1)[1])))
        if models != {'gpt-6.1-sol'} or not finals or digest(finals[-1].encode()) != proof['actual_final_sha256'] or not images.issubset(rendered):
            raise ValueError('Independent transcript final/model/image identity mismatch')
        texts = [self.artifacts['input'], self.artifacts['system_prompt']]
        texts += [v for k, v in self.artifacts.items() if k.startswith('required_text_')]
        if any(not delivered(value, outputs) for value in texts):
            raise ValueError('Complete reviewed text delivery unverified')
        self.snapshot = json.loads(self.artifacts['source_manifest'])
        self.receipt = json.loads(self.artifacts['compile_receipt'])
        if not self.snapshot or not self.receipt.get('runtimeModuleVersionId') or self.receipt.get('success') is not True:
            raise ValueError('Reviewed snapshot/compile MVID missing')
        for i, (rel, entry) in enumerate(self.snapshot.items()):
            if Path(rel).is_absolute() or '..' in Path(rel).parts:
                raise ValueError('Snapshot path outside project')
            self.read(f'snapshot_backup_{i}', {'path': entry['backup'], 'sha256': entry['sha256']}, binary=True)
        for i, (name, descriptor) in enumerate(self.manifest.get('measurements', {}).items()):
            artifact = f'supplemental_unreviewed_{i}_{name}'
            self.artifacts[artifact] = self.read(artifact, descriptor)

    def read(self, name, descriptor, binary=False):
        path = (self.path.parent / descriptor['path']).resolve()
        raw = path.read_bytes()
        if digest(raw) != descriptor['sha256']:
            raise ValueError('Auxiliary SHA mismatch: ' + name)
        self.sources[name] = {'path': str(path), 'sha256': digest(raw)}
        self.frozen[name] = raw
        return raw if binary else raw.decode('utf-8-sig')

    def classification(self):
        # A recorded compile MVID alone does not bind today's DLL bytes.
        if not any(rel.lower().endswith('.dll') for rel in self.snapshot):
            return 'historical'
        for rel, entry in self.snapshot.items():
            path = self.project / rel
            if not path.is_file() or digest(path.read_bytes()) != entry['sha256']:
                return 'historical'
        return 'scoped_current_files_match_with_recorded_MVID'

    def payload(self):
        status = self.classification()
        inlined = ([] if self.archive_mode == 'references_only' else ['protocol', 'feedback']) + [
            name for name in self.artifacts if name.startswith('supplemental_unreviewed_')]
        dependencies = [name for name in self.sources if name not in inlined]
        text = ('\n[AUXILIARY VERIFIED EVIDENCE; supplemental context only]\n'
                'Preserve the original task and output JSON schema. Status: ' + status + '.\n'
                'Scope: exactly the reviewed source_manifest paths, reviewed screenshots and required text; '
                'not all Assets, all rules, or a fresh live runtime verification. Recorded MVID: ' + self.receipt['runtimeModuleVersionId'] + '. '
                'Historical feedback is not a current score. The rubric retains general scores and item name/method; '
                'missing item-specific numeric anchors remain missing. Null/unverified measurements are gaps, '
                'not confirmed UI failures. Do not infer totals or fill gaps with zero. '
                'Additional supplemental_unreviewed measurements were not certified by this independent review.\n')
        for name in inlined:
            text += '\n--- ' + name.upper() + ' (verbatim frozen input) ---\n' + self.artifacts[name] + '\n'
        text += ('\n--- VALIDATED ARCHIVE DEPENDENCIES ---\n'
                 'Not inlined into this role prompt. Read references if needed; the independent '
                 'reviewer read the required packet evidence. Hash validation does not mean this '
                 'role has read these files.\n' + json.dumps(self.sources, ensure_ascii=False))
        sidecar = {'schema': 2, 'manifest_path': str(self.path), 'manifest_sha256': digest(self.raw),
                   'sources': self.sources, 'snapshot_status': status,
                   'snapshot_scope_paths': list(self.snapshot), 'recorded_MVID': self.receipt['runtimeModuleVersionId'],
                   'inlined_artifacts': inlined, 'dependency_only_names': dependencies,
                   'runtime_freshness': 'not independently rechecked', 'archive_mode': self.archive_mode,
                   'context_sha256': digest(text.encode())}
        return text, sidecar

    def freeze(self, out):
        folder = Path(out) / 'auxiliary_evidence' / digest(self.raw)
        folder.mkdir(parents=True, exist_ok=True)
        values = {'manifest.json': self.raw}
        for name, raw in self.frozen.items():
            if digest(Path(self.sources[name]['path']).read_bytes()) != self.sources[name]['sha256']:
                raise ValueError('Evidence changed before freeze: ' + name)
            values[digest(name.encode()) + '.frozen'] = raw
        for name, raw in values.items():
            target = folder / name
            if target.exists():
                if target.read_bytes() != raw:
                    raise ValueError('Frozen evidence changed: ' + str(target))
            else:
                with target.open('xb') as stream:
                    stream.write(raw)
        return folder


def load_auxiliary_evidence(manifest, project):
    """Dispatch by schema without changing the legacy schema 2 contract."""
    schema = json.loads(Path(manifest).read_bytes()).get('schema')
    if schema == 3:
        from sharded_auxiliary_evidence import ShardedAuxiliaryEvidence
        return ShardedAuxiliaryEvidence(manifest, project)
    return AuxiliaryEvidence(manifest, project)
