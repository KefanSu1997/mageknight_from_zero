"""Run pinned RSIGame unchanged round algorithms against the live Unity substrate.

Engine dispatch, storage and model SDK are adapted; controller prompts/parsers,
one_round, art_round and checkpoint cadence remain upstream code.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import time
from types import SimpleNamespace

from run_luna_bridge_experiment import Queue, write

PIN = '1214b342fce22e97fe19bfc56be189d06f44634b'
EXCLUDED = {'.git', 'Library', 'Temp', 'Logs', 'obj', 'Build', 'Builds',
            'AutomationOutputs', 'AutomationLogs', '__pycache__', '.vs', 'node_modules',
            '.venv_rl', '.venv_ui_text', '.ccr_bridge', 'tmp', 'multi-agent-workspace'}


ROOT_EXCLUDED = EXCLUDED - {'.git', '__pycache__', '.vs', 'node_modules'}
TREE_EXCLUDED = {'.git', '__pycache__', '.vs', 'node_modules'}
SOURCE_SCOPE = 'Root-level Unity/build/evidence caches excluded; tool caches and virtual environments excluded at every depth; nested authored Temp/Library directories retained.'


def source_files(root):
    """Include dirty and untracked project files, without traversing Unity caches."""
    root = Path(root)
    for directory, names, files in os.walk(root):
        excluded = TREE_EXCLUDED | (ROOT_EXCLUDED if Path(directory) == root else set())
        names[:] = sorted(n for n in names if n not in excluded and not n.startswith(('.venv', 'venv')))
        for name in sorted(files):
            p = Path(directory) / name
            if p.suffix not in {'.pyc', '.sln', '.csproj', '.user'}:
                yield p


def snapshot(root, destination, store, hash_cache=None):
    if destination.is_file():
        return str(destination)  # A resumed run never rewrites historical trees.
    entries = {}
    for p in source_files(root):
        if hash_cache is not None:
            sha, size = hash_cache.file(p, store)
            entries[p.relative_to(root).as_posix()] = {'sha256': sha, 'bytes': size}
            continue
        raw = p.read_bytes()
        sha = hashlib.sha256(raw).hexdigest()
        blob = store / sha[:2] / sha
        if not blob.exists():
            blob.parent.mkdir(parents=True, exist_ok=True)
            blob.write_bytes(raw)
        entries[p.relative_to(root).as_posix()] = {'sha256': sha, 'bytes': len(raw)}
    result = {'schema': 1, 'root': str(root), 'files': entries,
              'storage': 'content-addressed source snapshot; restore relative paths from blob store',
              'excluded_root_directories': sorted(ROOT_EXCLUDED),
              'excluded_at_all_depths': sorted(TREE_EXCLUDED),
              'source_scope': SOURCE_SCOPE}
    result['tree_sha256'] = hashlib.sha256(json.dumps(entries, sort_keys=True).encode()).hexdigest()
    write(destination, result)
    return str(destination)


def snapshot_round(game, round_number, project, store, hash_cache=None):
    destination = game.dir / f'round_{round_number:02d}' / 'tree_manifest.json'
    if destination.is_file():
        return str(destination)  # Never attach today's checklist to an old tree.
    state = game.dir / 'checklist_state.json'
    target = destination.parent / 'checklist_state.json'
    if state.is_file() and not target.exists():
        target.parent.mkdir(parents=True, exist_ok=True)
        with target.open('xb') as stream:
            stream.write(state.read_bytes())
    return snapshot(project, destination, store, hash_cache)


UNITY_ART = '''THIS IS A UNITY PROJECT. Work in its existing Assets hierarchy and
preserve .meta GUIDs. Wire assets into the actual scenes, prefabs or UI drawing
code; an unused file is no visible improvement. Keep existing layout sizes and
verify actual screenshots after import and compilation. Use the supplied Unity
build gate and live session, which must report the loaded candidate assembly.
Generated files require Unity import through the supplied build gate.
Only request image generation if an available tool actually supports it.
'''


def compile_candidate(adapter, receipt_path):
    from capture_ui_design_pairs import editor_call
    # A previous receipt cannot certify newly edited C# or imported assets.
    deadline = time.monotonic() + 600
    editor_call('stop', timeout=max(1, deadline - time.monotonic()))
    while editor_call('status', timeout=max(1, deadline - time.monotonic())).get('isPlaying'):
        if time.monotonic() > deadline:
            raise TimeoutError('Unity did not leave Play mode for compilation')
        time.sleep(.5)
    editor_call('refresh', timeout=max(1, deadline - time.monotonic()))
    time.sleep(2)
    observations, stable_id = [], None
    while time.monotonic() < deadline:
        # Domain reload may suspend the editor update callback longer than the
        # normal request allowance. Poll the same request; do not enqueue retries.
        status = editor_call('status', timeout=max(1, deadline - time.monotonic()))
        passed = (status.get('success') and not status.get('isCompiling') and
                  not status.get('isPlaying') and status.get('actualConsoleCountsAvailable') and
                  status.get('actualConsoleErrorCount') == 0)
        module = status.get('runtimeModuleVersionId')
        if passed and module and (stable_id is None or stable_id == module):
            observations.append(status)
            stable_id = module
            if len(observations) >= 2:
                break
        else:
            observations, stable_id = [], None
            if status.get('actualConsoleCountsAvailable') and status.get('actualConsoleErrorCount', 0) > 0:
                return {'ok': False, 'tail': json.dumps(status), 'unity_gate': status}
        time.sleep(2)
    else:
        raise TimeoutError('Unity compiler/Console gate failed to stabilize')
    write(receipt_path, {'success': True, 'errors': 0,
          'runtimeModuleVersionId': stable_id, 'observations': observations,
          'verification': 'forced refresh/compile then two real zero-error Console observations'})
    receipt = adapter.build_gate(receipt_path)
    return {'ok': bool(receipt.get('success')), 'tail': json.dumps(receipt, ensure_ascii=False), 'unity_gate': receipt}


def install_unity_prompts():
    import rsigame.controller.repair_prompt as prompt
    # Retain the complete upstream prompt skeleton and closing JSON schema.
    render, art = prompt.render, prompt.render_art
    # Replace only the engine's launch instructions in the original skeletons.
    for key in ('_HEAD', '_HEAD_LEAN', '_ART_HEAD'):
        value = getattr(prompt, key)
        start = value.find('  - you can run the build and watch it:')
        end = value.find('{project_map}', start)
        if start >= 0 and end >= 0:
            value = value[:start] + ('  - The shared Unity operator records live candidate screenshots. '
                'Use the Unity build command above after edits; do not start a second Editor or pilot.\n') + value[end:]
        value = value.replace('Prefer `play_cli`, which cleans up after itself.',
                              'Use the shared Unity operator for live checks.').replace(
                              '`play_cli`, which cleans up after itself.',
                              'the shared Unity operator for live checks.')
        setattr(prompt, key, value)
    def unity_render(packet, **kw):
        kw['engine'] = 'godot'
        old, lean = prompt._ART, prompt._ART_LEAN
        try:
            prompt._ART = prompt._ART_LEAN = UNITY_ART
            return render(packet, **kw)
        finally:
            prompt._ART, prompt._ART_LEAN = old, lean
    def unity_art(brief_text, **kw):
        kw['engine'] = 'godot'
        old = prompt._ART
        try:
            prompt._ART = UNITY_ART
            return art(brief_text, **kw).replace('then import,', 'then compile in Unity,').replace('AFTER the import', 'AFTER Unity import and compilation')
        finally:
            prompt._ART = old
    prompt.render, prompt.render_art = unity_render, unity_art


def install_reader_transport(queue):
    """Cover both loop plumbing and the independent estimator/judge clients."""
    import rsigame.agent.llm as llm
    def create(**kwargs):
        metadata = {k: v for k, v in kwargs.items() if k not in ('messages', 'model')}
        metadata.update(upstream_requested_model=kwargs.get('model'), model='gpt-6.1-sol',
                        executed_model='gpt-6.1-sol', usage_measurement='unavailable in queued transport')
        raw = queue.ask(kwargs['messages'], metadata=metadata)
        return SimpleNamespace(choices=[SimpleNamespace(message=SimpleNamespace(content=raw), finish_reason='stop')], usage=None)
    def client(*args, **kwargs):
        return SimpleNamespace(chat=SimpleNamespace(completions=SimpleNamespace(create=create)))
    llm._client = client
    # These deliberately bypass llm._client in upstream. Replace only their SDK
    # construction; retain original messages, retries, parsers and aggregation.
    import rsigame.eval.estimator.rubric as rubric
    import rsigame.eval.pairwise.judge as pairwise
    rubric._client = client
    pairwise.OpenAI = client
    return client


def install(args, queue):
    hash_cache = getattr(args, 'hash_cache', None)
    from unity_source_view import EditorPngScope
    png_scope = EditorPngScope(args.project, args.out)
    install_reader_transport(queue)
    import rsigame.agent.generator.repair as repair
    def edit(work, prompt, **kwargs):
        png_scope.begin()
        before = (hash_cache.hashes(work, source_files(work)) if hash_cache else
                  {p.relative_to(work).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in source_files(work)})
        raw = queue.ask([{'role': 'system', 'content': args.editor_system.read_text(encoding='utf8')},
                         {'role': 'user', 'content': prompt}], kind='editor', work=work,
                        metadata={'original_repair_options': kwargs, 'tool_trace': 'unavailable; do not interpret as zero measured tools'})
        after = (hash_cache.hashes(work, source_files(work)) if hash_cache else
                 {p.relative_to(work).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in source_files(work)})
        png_scope.capture(before, after)
        return {'success': True, 'messages': [raw], 'tools': [], 'tool_trace_unavailable': True,
                'files_edited': sorted(k for k in before.keys() | after.keys() if before.get(k) != after.get(k))}
    repair._run_repair_agent = edit
    import rsigame.project as pj
    install_unity_prompts()
    from rsigame import loop
    import rsigame.evidence.fingerprint as fingerprint
    # Hash real source/assets, never continuously changing Editor caches or the
    # experiment's own evidence (which would invalidate every replay cache).
    fingerprint._walk = lambda root: iter(sorted(source_files(Path(root))))
    if hash_cache is not None:
        fingerprint.project_hash = lambda root: hash_cache.project_hash(
            Path(root), sorted(source_files(Path(root))))
        # Upstream binds these at import time, before this adapter is installed.
        # Rebind the known aliases as well as fingerprint()'s module global.
        loop.project_hash = fingerprint.project_hash
        import rsigame.evidence.artifact as artifact
        artifact.project_hash = fingerprint.project_hash
        import rsigame.planning.pipeline as pipeline
        pipeline.project_hash = fingerprint.project_hash
    import rsigame.view.game_files as game_files
    game_files._ALL_TOP = game_files._ALL_TOP | {'Assets', 'Packages', 'ProjectSettings', 'resources'}
    from unity_upstream_session import install_sessions
    adapter = install_sessions(args, pj, loop)
    from unity_source_view import install as install_source_view
    install_source_view(pj, loop, compact_source_map=getattr(args, 'compact_source_map', False),
                        archive_directory=args.out / 'source_map_archives')
    original_fix_pngs = loop.fix_fake_pngs
    loop.fix_fake_pngs = lambda work: (png_scope.fix(work) if
        Path(work).resolve() == args.project.resolve() or
        (Path(work) / 'ProjectSettings/ProjectVersion.txt').is_file() else original_fix_pngs(work))
    pj.engine_of = lambda work: 'unity'
    pj.BUILD_CMD['unity'] = f'"{sys.executable}" "{Path(__file__).resolve()}" --build-gate --compile-receipt "{args.compile_receipt}"'
    def build(work):
        return compile_candidate(adapter, args.compile_receipt)
    pj.build = build
    pj.godot_import = lambda *a, **k: {'engine': 'unity', 'note': 'Unity Editor imports assets; compiler gate is separate'}
    def game_init(self, game, root, corpus, port):
        self.game, self.port = game, port
        self.dir = root / 'runs' / game
        self.dir.mkdir(parents=True, exist_ok=True)
        self.work = args.project
        self.import0 = pj.godot_import(self.work)
        self.build0 = build(self.work)
        if not self.build0['ok']:
            raise RuntimeError('Initial Unity compilation gate failed: ' + self.build0['tail'])
        self.ledger = loop.Ledger(self.dir / 'targets.json')
        f = self.dir / 'rounds.json'
        self.rounds = json.loads(f.read_text()) if f.is_file() else []
        self.demo_desc = loop.describe(self.work, self.dir / 'demo_descriptions.json')
        self.proxy = None
        self.write_settings()
    loop.Game.__init__ = game_init
    loop.Game.start_proxy = lambda self: setattr(self, 'proxy', None)
    loop.snapshot_tree = lambda g, r: snapshot_round(g, r, args.project, args.out / 'source_blobs', hash_cache)
    if getattr(args, 'resume_build_suffix', None) is not None:
        from resume_build_suffix import install as install_build_recovery
        install_build_recovery(loop, args)
    if getattr(args, 'resume_late_editor', None) is not None:
        from resume_late_editor import install as install_late_editor_recovery
        install_late_editor_recovery(loop, args)
    return loop


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--upstream', type=Path)
    ap.add_argument('--bench', type=Path)
    ap.add_argument('--project', type=Path, default=Path(__file__).resolve().parents[2])
    ap.add_argument('--out', type=Path)
    ap.add_argument('--live', type=Path)
    ap.add_argument('--demos', type=Path)
    ap.add_argument('--tasks', type=Path)
    ap.add_argument('--compile-receipt', type=Path)
    ap.add_argument('--editor-system', type=Path)
    ap.add_argument('--game', default='mageknight-unity')
    ap.add_argument('--rounds', type=int, default=30)
    ap.add_argument('--build-gate', action='store_true')
    ap.add_argument('--check-only', action='store_true')
    ap.add_argument('--auxiliary-evidence', type=Path,
                    help='Opt in to actual independent review proof/input manifest (schema 2 or delegated schema 3)')
    ap.add_argument('--resume-build-suffix', type=int,
                    help='Explicitly finish a saved no-primary round after an engine bridge build exception')
    ap.add_argument('--compact-source-map', action='store_true',
                    help='Opt in to complete C# file counts with full immutable source-map JSON archives')
    ap.add_argument('--incremental-source-hashes', action='store_true',
                    help='Opt in to process-local stat/change-time hash reuse; restart required')
    ap.add_argument('--resume-late-editor', type=int,
                    help='Explicitly recover a saved no-primary Editor queue timeout')
    ap.add_argument('--late-editor-call', help='Exact actual completed Editor call folder')
    args = ap.parse_args()
    if args.resume_late_editor is not None and (not args.late_editor_call or args.resume_build_suffix is not None):
        ap.error('Late Editor recovery requires its exact call and cannot combine with build recovery')
    args.project = args.project.resolve()
    args.hash_cache = None
    if args.incremental_source_hashes:
        from source_hash_cache import SourceHashCache
        args.hash_cache = SourceHashCache()
    if args.build_gate:
        from unity_adapter import UnityAdapter
        result = compile_candidate(UnityAdapter(args.project), args.compile_receipt)
        print(json.dumps(result, ensure_ascii=False))
        return 0 if result['ok'] else 1
    for name in ('upstream', 'bench', 'out', 'live', 'demos', 'tasks', 'compile_receipt', 'editor_system'):
        value = getattr(args, name)
        if value is None:
            ap.error('--' + name.replace('_', '-') + ' is required')
        setattr(args, name, value.resolve())
    actual = subprocess.check_output(['git', '-C', str(args.upstream), 'rev-parse', 'HEAD'], text=True).strip()
    if actual != PIN:
        raise RuntimeError(f'Upstream must be pinned at {PIN}; found {actual}')
    for path in (args.project / 'ProjectSettings/ProjectVersion.txt', args.editor_system,
                 args.tasks / args.game / 'instruction.md', args.compile_receipt):
        if not path.is_file():
            raise FileNotFoundError(path)
    if not list(args.demos.glob('*.json')):
        raise RuntimeError('Frozen actual input demos required before starting rounds')
    auxiliary = None
    if args.auxiliary_evidence is not None:
        from auxiliary_evidence import load_auxiliary_evidence
        auxiliary = load_auxiliary_evidence(args.auxiliary_evidence, args.project)
    if args.check_only:
        result = {'checked': 'files and pinned commit only; no Unity/model calls', 'pin': actual}
        if auxiliary is not None:
            result['auxiliary_evidence'] = auxiliary.payload()[1]
        print(json.dumps(result))
        return 0
    args.out.mkdir(parents=True, exist_ok=True)
    write(args.out / 'live_status.json', {'status': 'starting', 'pid': os.getpid()})
    try:
        snapshot(args.project, args.out / 'baseline/source_manifest.json', args.out / 'source_blobs', args.hash_cache)
        if not (args.out / 'baseline/git_status.txt').exists():
            dirty = subprocess.check_output(['git', '-C', str(args.project), 'status', '--porcelain=v1', '-uall'], text=True)
            (args.out / 'baseline/git_status.txt').write_text(dirty, encoding='utf8')
        empty = args.out / 'empty.env'
        empty.touch(exist_ok=True)
        os.environ.pop('RSIGAME_CONTROLLER_STOP_FILE', None)
        os.environ.update(GAMECRAFT_BENCH=str(args.bench), GAMECRAFT_BENCH_TASKS=str(args.tasks),
                          RSIGAME_PATHS_RUN=str(args.out / 'upstream_runtime'), RSIGAME_PATHS_ENV=str(empty),
                          RSIGAME_MODELS_LOOP_MODEL='gpt-6.1-sol', RSIGAME_MODELS_PLAY_MODEL='gpt-6.1-sol',
                          RSIGAME_REPAIR_MODEL='gpt-6.1-sol', RSIGAME_CONTROLLER_SNAPSHOT='1')
        sys.path[:0] = [str(args.upstream / 'src'), str(args.bench)]
        from rsigame import config
        cfg = config.load(args.upstream / 'configs/experiment/paper_godot_gpt.toml', validate_paths=False)
        write(args.out / 'file_configuration.json', {'data': cfg.data, 'sources': cfg.sources})
        write(args.out / 'substrate_differences.json', {
            'upstream_commit': actual, 'work_directory': str(args.project),
            'storage': 'actual Unity root; immutable baseline and per-round content-addressed source manifests replace corpus copies',
            'rounds': args.rounds, 'editor': 'gpt-6.1-sol', 'all_other_roles': 'gpt-6.1-sol',
            'unchanged_algorithms': ['one_round', 'art_round', 'controller.execute', 'parsers', 'checkpoints'],
            'differences': ['Unity engine dispatch/session/replay and art mechanics', 'queued Codex model transport', 'token/tool usage unavailable']})
        if auxiliary is None:
            queue = Queue(args.out)
        else:
            from auxiliary_evidence_queue import AuxiliaryQueue
            queue = AuxiliaryQueue(args.out, auxiliary)
        loop = install(args, queue)
        loop.main([args.game, '--rounds', str(args.rounds), '--root', str(args.out / 'development'), '--corpus', str(args.out / 'corpus')])
    except BaseException as exc:
        write(args.out / 'live_status.json', {'status': 'failed', 'error': repr(exc), 'pid': os.getpid()})
        raise
    write(args.out / 'live_status.json', {'status': 'upstream_returned', 'pid': os.getpid(), 'model_calls': queue.n})
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
