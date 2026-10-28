"""Execute upstream RSIGame with an explicit file-queued Luna transport.

The pinned upstream remains unedited. This is a backend/SDK substitution,
not a claim of API-message or paper-score equivalence. A broker supplies real
Luna responses; absence never becomes a synthetic model response.
"""
import argparse
import base64
import hashlib
import inspect
import json
import os
from pathlib import Path
import sys
import time
from types import SimpleNamespace
import uuid


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + '.tmp')
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf8')
    temporary.replace(path)


def windows_path(path):
    value = str(Path(path).resolve())
    if value.startswith('/mnt/') and len(value) > 7 and value[6] == '/':
        return value[5].upper() + ':/' + value[7:]
    return value


def tree_hashes(root):
    return {str(p.relative_to(root)): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in root.rglob('*') if p.is_file()
            and not any(x in {'.godot', '.qwen', '.git', 'node_modules'} for x in p.relative_to(root).parts)}


class Queue:
    def __init__(self, root, model_policy=None):
        self.root = Path(root)
        self.model_policy = dict(model_policy or {'reader': 'gpt-6.1-sol', 'editor': 'gpt-6.1-sol'})
        if not all(isinstance(v, str) and v for v in self.model_policy.values()):
            raise ValueError('Every model policy entry must name an explicit model')
        prefixes = [p.name.split('_', 1)[0] for p in (self.root / 'model_calls').glob('*') if p.is_dir()]
        self.n = max((int(value) for value in prefixes if value.isdigit()), default=0)
        self.runner_pid = os.getpid()
        self.runner_instance = uuid.uuid4().hex

    def requested_model(self, kind):
        if kind not in self.model_policy:
            raise ValueError('No explicit model routing for role: ' + kind)
        return self.model_policy[kind]

    @staticmethod
    def verify_model(answer, requested):
        executed = answer.get('executed_model')
        if executed != requested:
            raise RuntimeError(f'Model routing mismatch: requested {requested}, executed {executed!r}; fallback forbidden')
        return executed

    def ask(self, messages, *, kind='reader', work=None, metadata=None):
        requested = self.requested_model(kind)
        self.n += 1
        call = self.root / 'model_calls' / f'{self.n:04d}_{uuid.uuid4().hex[:8]}'
        call.mkdir(parents=True)
        write(call / 'original_messages.json', messages)
        frames, lines = [], []
        for n, message in enumerate(messages):
            lines.append(f'\n--- ORIGINAL MESSAGE {n + 1}: {message["role"]} ---\n')
            content = message.get('content', '')
            if isinstance(content, str):
                lines.append(content)
                continue
            for block in content:
                if block.get('type') == 'text':
                    lines.append(block.get('text', ''))
                elif block.get('type') == 'image_url':
                    url = block['image_url']['url']
                    if not url.startswith('data:image/'):
                        raise ValueError('Only original inlined image evidence is supported')
                    header, payload = url.split(',', 1)
                    extension = '.jpg' if 'jpeg' in header else '.png'
                    frame = call / f'image_{len(frames) + 1:02d}{extension}'
                    frame.write_bytes(base64.b64decode(payload))
                    frames.append(dict(path=windows_path(frame), sha256=hashlib.sha256(frame.read_bytes()).hexdigest()))
                    lines.append(f'[ORIGINAL IMAGE ATTACHMENT {len(frames)}]')
                else:
                    raise ValueError('Unsupported content block: ' + str(block.get('type')))
        (call / 'messages.txt').write_text('\n'.join(lines), encoding='utf8')
        stack = [dict(file=str(f.filename), function=f.function, line=f.lineno)
                 for f in inspect.stack()[1:9]]
        request = dict(status='pending', model=requested, requested_model=requested, kind=kind,
                       runner_pid=self.runner_pid, runner_instance=self.runner_instance,
                       request_path=windows_path(call / 'request.json'),
                       original_messages_path=windows_path(call / 'original_messages.json'),
                       text_path=windows_path(call / 'messages.txt'), images=frames,
                       response_path=windows_path(call / 'response.json'),
                       work_directory=windows_path(work) if work else None,
                       metadata=metadata or {}, caller_stack=stack,
                       transport_difference='Explicitly routed Codex worker receives original role-labelled messages; upstream API/SDK is replaced. No custom UI role schema.')
        write(call / 'request.json', request)
        write(self.root / 'live_status.json', dict(status='waiting_for_model', requested_model=requested, call=self.n, request=windows_path(call / 'request.json'), pid=self.runner_pid, runner_instance=self.runner_instance))
        print(f'MODEL_REQUEST {self.n} {kind} {requested} {windows_path(call / "request.json")}', flush=True)
        start = time.monotonic()
        response = call / 'response.json'
        while not response.exists():
            if time.monotonic() - start > 1800:
                raise TimeoutError('No routed model response for ' + str(call))
            time.sleep(.25)
        answer = json.loads(response.read_text(encoding='utf-8-sig'))
        if answer.get('error'):
            raise RuntimeError(answer['error'])
        executed = self.verify_model(answer, requested)
        raw = answer['raw']
        if not isinstance(raw, str):
            raise TypeError('raw must be the actual final model text')
        write(call / 'receipt.json', dict(seconds=time.monotonic() - start, requested_model=requested, executed_model=executed, broker_agent_id=answer.get('broker_agent_id'), worker_agent_id=answer.get('worker_agent_id'), raw_sha256=hashlib.sha256(raw.encode()).hexdigest()))
        return raw


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--upstream', type=Path, required=True)
    parser.add_argument('--bench', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--godot', type=Path, required=True)
    parser.add_argument('--editor-system', type=Path, required=True)
    parser.add_argument('--rounds', type=int, default=30)
    parser.add_argument('--stop-after', type=int, default=0)
    args = parser.parse_args()
    out = args.out.resolve()
    out.mkdir(parents=True, exist_ok=True)
    empty_env = out / 'empty.env'
    empty_env.touch(exist_ok=True)
    os.environ.update(GAMECRAFT_BENCH=str(args.bench.resolve()),
        GAMECRAFT_BENCH_TASKS=str(args.bench.resolve() / 'tasks'),
        GAMECRAFT_BENCH_GODOT_BIN=str(args.godot.resolve()),
        RSIGAME_PATHS_RUN=str(out / 'upstream_runtime'),
        RSIGAME_PATHS_ENV=str(empty_env), RSIGAME_MODELS_LOOP_MODEL='gpt-6.1-sol',
        RSIGAME_MODELS_PLAY_MODEL='gpt-6.1-sol', RSIGAME_REPAIR_MODEL='gpt-6.1-sol',
        RSIGAME_CONTROLLER_STOP_FILE=str(out / 'round_boundary.stop'))
    os.environ['PATH'] = str(args.godot.parent) + os.pathsep + os.environ.get('PATH', '')
    sys.path[:0] = [str(args.upstream.resolve() / 'src'), str(args.bench.resolve())]
    from rsigame import config
    cfg = config.load(args.upstream / 'configs/experiment/paper_godot_gpt.toml', validate_paths=False)
    write(out / 'file_configuration.json', dict(data=cfg.data, sources=cfg.sources,
        note='Original file config plus explicit transport environment overrides; path/SDK preflight replaced by recorded runtime gates, not silently passed.'))
    queue = Queue(out)
    import rsigame.agent.llm as llm

    def create(**kwargs):
        raw = queue.ask(kwargs['messages'], metadata={k: v for k, v in kwargs.items() if k != 'messages'})
        return SimpleNamespace(choices=[SimpleNamespace(message=SimpleNamespace(content=raw), finish_reason='stop')])

    llm._client = lambda model: SimpleNamespace(chat=SimpleNamespace(completions=SimpleNamespace(create=create)))
    import rsigame.agent.generator.repair as repair

    def edit(work, prompt, **kwargs):
        work = Path(work)
        before = tree_hashes(work)
        raw = queue.ask([{'role': 'system', 'content': args.editor_system.read_text(encoding='utf8')},
                         {'role': 'user', 'content': prompt}], kind='editor', work=work,
                         metadata=dict(original_repair_options=kwargs,
                            tool_count_observation='unavailable in collaboration transport; never interpret empty tools array as measured zero'))
        after = tree_hashes(work)
        changed = sorted(k for k in before.keys() | after.keys() if before.get(k) != after.get(k))
        return dict(success=True, files_edited=changed, messages=[raw], tools=[],
                    tool_trace_unavailable=True, transport='Explicitly routed Codex worker', requested_model=queue.requested_model('editor'))

    repair._run_repair_agent = edit
    from rsigame import loop
    # Disable only the upstream API metering proxy: this backend never uses an API key.
    loop.Game.start_proxy = lambda self: setattr(self, 'proxy', None)
    original_save = loop.Game.save

    def save(self):
        original_save(self)
        completed = [r for r in self.rounds if r.get('tree_snapshot')]
        if args.stop_after and len(completed) >= args.stop_after:
            Path(os.environ['RSIGAME_CONTROLLER_STOP_FILE']).write_text(
                'Requested pilot boundary: one actual development round; 30-round context retained. Not convergence.', encoding='utf8')

    loop.Game.save = save
    write(out / 'live_status.json', dict(status='starting_upstream', pid=os.getpid()))
    try:
        loop.main(['puzzle-sokoban-dungeon', '--rounds', str(args.rounds),
                   '--root', str(out / 'development'), '--corpus', str(out / 'corpus')])
    except BaseException as exc:
        write(out / 'live_status.json', dict(status='failed', error=type(exc).__name__ + ': ' + str(exc), pid=os.getpid(), model_calls=queue.n))
        raise
    write(out / 'live_status.json', dict(status='round_boundary_reached', pid=os.getpid(), model_calls=queue.n))


if __name__ == '__main__':
    main()
