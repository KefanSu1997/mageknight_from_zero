"""Install Unity substrate and external-demo paths in pinned upstream modules.

The upstream checkout is never edited. Function source substitutions below
retarget engine readiness and demo storage; upstream prompts and loops survive.
"""
from __future__ import annotations
import inspect
import json
from pathlib import Path
import textwrap
import time

from unity_adapter import (UnityAdapter, session_class, replay_one, capture_demo,
                           recorded_replay, require_actual_work)


def _retarget(module, name, replacements, demo_root):
    function = getattr(module, name)
    source = textwrap.dedent(inspect.getsource(function))
    changed = source
    for before, after in replacements:
        changed = changed.replace(before, after)
    if changed == source:
        return False
    namespace = function.__globals__
    namespace['_unity_demos_root'] = demo_root
    compiled = {}
    exec(compile(changed, inspect.getsourcefile(function) + ':unity-demo-storage', 'exec'), namespace, compiled)
    setattr(module, name, compiled[name])
    return True


def install_explore_build_gate(explore, adapter, compile_receipt, project):
    """Retarget only upstream's engine readiness branch, preserving its loop."""
    function = explore.explore
    source = textwrap.dedent(inspect.getsource(function))
    before = "    if (src / 'project.godot').is_file():\n        from . import godot_build"
    after = "    if (src / 'ProjectSettings' / 'ProjectVersion.txt').is_file():\n        never_built_why = _unity_build_failure(src)\n    elif (src / 'project.godot').is_file():\n        from . import godot_build"
    if source.count(before) != 1:
        raise RuntimeError('Pinned upstream explore build gate changed; refusing ambiguous retarget')
    def failure(src):
        require_actual_work(src, project)
        result = adapter.build_gate(compile_receipt)
        return (None if result['success'] else
                'Unity loaded assembly is not ready: zero-error compile receipt and current Editor assembly must agree. ' + json.dumps(result, default=str))
    namespace = function.__globals__
    namespace['_unity_build_failure'] = failure
    compiled = {}
    exec(compile(source.replace(before, after), inspect.getsourcefile(function) + ':unity-build-gate', 'exec'), namespace, compiled)
    explore.explore = compiled['explore']


def install_explore_reader(explore, game, task_document):
    """Unity reader seam: authoritative task and explicit scanner limits."""
    original = explore._brief
    def brief(src):
        if not (Path(src) / 'ProjectSettings' / 'ProjectVersion.txt').is_file():
            return original(src)
        raw, source = task_document(game, Path(src))
        return ('## WHAT THE GAME SAYS IT IS (current task document)\n\n'
                + str(raw) + '\n\nTask source: ' + str(source)
                + '\n\n## CONTROLS\n\n'
                'The upstream TypeScript/GDScript binding parsers do not support Unity C#. '
                'No complete binding inventory has been established. Use visible UI controls; '
                'read_bindings returns only partial C# source lines, not proof of input support. '
                'Keyboard input and named scenario launches are unavailable on this substrate.')
    explore._brief = brief


def install_sessions(args, pj, loop):
    from capture_ui_design_pairs import editor_call
    import rsigame.agent.evolve.verifier_agent as verifier
    import rsigame.agent.evolve.explore_arm as explore
    import rsigame.agent.evolve.play_cli as play_cli
    import rsigame.verify.post_repair_replay as prr
    import rsigame.evidence.observe as observe
    import rsigame.controller.inputs as inputs
    import rsigame.controller.execute as execute
    import rsigame.monitor.replay_pair as replay_pair

    demo_root = Path(args.demos).resolve()
    adapter = UnityAdapter(args.live)
    # A resumed run may find the Editor stopped after compilation. There is
    # no runtime observer until the existing pilot has actually been launched.
    # Preserve an already-running session; never replay an action on timeout.
    if not editor_call('status').get('isPlaying'):
        editor_call('start_pilot')
    adapter.demo = capture_demo(args.live, attach=True)
    adapter.observation = adapter.demo.initial_observation

    def setup(bound):
        # Original demos begin at the real launch UI. Resetting an existing
        # native fixture is not equivalent, so use the existing Editor launcher.
        editor_call('stop')
        deadline = time.monotonic() + 60
        while editor_call('status').get('isPlaying'):
            if time.monotonic() > deadline:
                raise TimeoutError('Editor did not exit play mode for independent replay')
            time.sleep(.1)
        editor_call('start_pilot')
        bound.demo = capture_demo(args.live, attach=True)
        bound.observation = bound.demo.initial_observation
        bound.setup_events.append({'role': 'scripted_setup_not_player_input', 'operation': 'fresh_editor_pilot_launch'})

    Session = session_class(adapter, setup)

    class SourceSession(Session):
        def __init__(self, src, port=None, out_dir=None, max_frames=None, available_scenarios=(), **kw):
            require_actual_work(src, args.project)
            super().__init__(args.project, port=port, out_dir=out_dir, max_frames=max_frames,
                             available_scenarios=available_scenarios, **kw)

    from rsigame.checklist import task_checklist as TC
    install_explore_reader(explore, args.game, TC.task_document)
    install_explore_build_gate(explore, adapter, args.compile_receipt, args.project)
    factory = lambda src: SourceSession
    verifier._session_for = factory
    explore._session_for = factory
    play_cli._session_for = factory

    def replay(work, trace_path, out_dir):
        trace_path = Path(trace_path)
        if not trace_path.is_file() and trace_path.parent.name == 'demo_outputs':
            trace_path = demo_root / trace_path.name
        recorded = require_actual_work(work, args.project, out_dir, trace_path.stem)
        if recorded is not None:
            return recorded
        return replay_one(adapter, args.project, trace_path, out_dir, setup=setup)

    prr.replay_one = replay
    replacements = [("work / 'demo_outputs'", '_unity_demos_root')]
    _retarget(prr, 'replay_demos', replacements, demo_root)
    _retarget(observe, 'describe', replacements, demo_root)
    loop.describe = observe.describe
    _retarget(execute, 'run', replacements, demo_root)
    _retarget(replay_pair, '_rec_from_replay', replacements, demo_root)
    _retarget(replay_pair, 'pair_checkpoints', [("Path(frozen_corpus) / 'demo_outputs'", '_unity_demos_root')], demo_root)
    recorded = replay_pair._rec_from_recorded
    def recorded_unity(tree_dir, demo_id, frozen_trace=None):
        result = recorded_replay(tree_dir, demo_id)
        if result is not None:
            return result
        return recorded(tree_dir, demo_id, frozen_trace)
    replay_pair._rec_from_recorded = recorded_unity
    # Preserve Run.demos' original descriptors exactly, changing storage only.
    source = textwrap.dedent(inspect.getsource(inputs.Run.demos))
    source = source.replace("self.dir / 'work' / 'demo_outputs'", '_unity_demos_root')
    scope = {'_unity_demos_root': demo_root, **inputs.Run.demos.__globals__}
    functions = {}
    exec(compile(source, inspect.getsourcefile(inputs.Run.demos) + ':unity-demo-storage', 'exec'), scope, functions)
    inputs.Run.demos = functions['demos']
    # Functions whose frozen script fallback uses the actual work directory.
    for name, function in list(vars(loop).items()):
        if inspect.isfunction(function) and function.__module__ == loop.__name__:
            _retarget(loop, name, [("g.work / 'demo_outputs'", '_unity_demos_root')], demo_root)
    Path(args.out).mkdir(parents=True, exist_ok=True)
    (Path(args.out) / 'unity_session_contract.json').write_text(json.dumps({
        'live_directory': str(args.live), 'external_frozen_demos': str(demo_root),
        'source_project': str(args.project), 'input_coordinates': 'top-left viewport; exact point mapped using transformed control probes',
        'input_constraint': 'requested point must pass actual Unity HitAt raycast; keyboard and manufactured state unavailable',
        'source_tools': ['read_source', 'list_source', 'read_bindings'],
        'fresh_launch_setup': 'Editor stop/start; separately recorded, never player input',
        'runtime_substitutions': 'Unity session/replay, engine readiness gate, external demo storage; upstream checkout untouched'
    }, indent=2), encoding='utf-8')
    return adapter
