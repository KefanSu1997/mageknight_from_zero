"""Freeze upstream source and index literal prompts without importing its code."""
import argparse
import ast
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import tomllib


def sha(data):
    return hashlib.sha256(data).hexdigest()


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf8')


def merge(base, overlay):
    for key, value in overlay.items():
        if isinstance(value, dict) and isinstance(base.get(key), dict):
            merge(base[key], value)
        else:
            base[key] = value
    return base


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--expected-commit', required=True)
    args = parser.parse_args()
    source, out = args.source.resolve(), args.out.resolve()
    commit = subprocess.check_output(['git', '-C', str(source), 'rev-parse', 'HEAD'], text=True).strip()
    if commit != args.expected_commit:
        raise ValueError('Unexpected upstream commit: ' + commit)
    out.mkdir(parents=True, exist_ok=False)
    tracked = subprocess.check_output(['git', '-C', str(source), 'ls-files'], text=True).splitlines()
    files, literals, callsites, functions = [], [], [], []
    for relative in tracked:
        rel = Path(relative)
        if not (rel.parts[0] in {'src', 'configs', 'scripts', 'tests', 'patches'}
                or relative in {'README.md', 'LICENSE', 'NOTICE', 'pyproject.toml'}):
            continue
        if rel.suffix.lower() not in {'.py', '.toml', '.json', '.md', '.sh', '.ts', '.txt', '.patch'} and rel.name not in {'LICENSE', 'NOTICE'}:
            continue
        raw = (source / rel).read_bytes()
        target = out / 'upstream' / rel
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source / rel, target)
        assert sha(target.read_bytes()) == sha(raw)
        files.append(dict(path=relative, sha256=sha(raw), bytes=len(raw)))
        if rel.suffix != '.py' or rel.parts[0] != 'src':
            continue
        text = raw.decode('utf-8-sig')
        tree = ast.parse(text)
        for node in ast.walk(tree):
            if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef, ast.ClassDef)):
                functions.append(dict(source_path=relative, name=node.name, kind=type(node).__name__, line=node.lineno, end_line=node.end_lineno))
            if isinstance(node, (ast.Assign, ast.AnnAssign)):
                value = node.value
                names = [t.id for t in node.targets if isinstance(t, ast.Name)] if isinstance(node, ast.Assign) else ([node.target.id] if isinstance(node.target, ast.Name) else [])
                if isinstance(value, ast.Constant) and isinstance(value.value, str) and len(value.value) >= 100:
                    for name in names:
                        if name.isupper() or any(w in name.lower() for w in ('prompt', 'system', 'template')):
                            destination = Path('literal_templates') / rel.with_suffix('') / (name + '__L' + str(node.lineno) + '.txt')
                            (out / destination).parent.mkdir(parents=True, exist_ok=True)
                            (out / destination).write_text(value.value, encoding='utf8')
                            literals.append(dict(source_path=relative, variable=name, line=node.lineno,
                                end_line=node.end_lineno, path=destination.as_posix(), characters=len(value.value),
                                decoded_literal_sha256=sha(value.value.encode('utf8')),
                                classification='literal_candidate_not_proof_of_active_call'))
            if isinstance(node, ast.Call):
                name = ast.unparse(node.func)
                keywords = {kw.arg for kw in node.keywords}
                if (any(w in name.lower() for w in ('_ask', '_chat', 'completions.create', 'responses.create'))
                        or 'messages' in keywords):
                    callsites.append(dict(source_path=relative, line=node.lineno, end_line=node.end_lineno,
                        call=name, source=ast.get_source_segment(text, node)))
    write(out / 'source_manifest.json', dict(repository='https://github.com/WenyiWU0111/RSIGame', commit=commit,
        copied_files=files, scope='Tracked textual implementation/configuration/scripts/tests; original bytes. No runtime evidence or external benchmark corpus.'))
    write(out / 'prompt_catalog.json', dict(commit=commit, templates=literals,
        warning='Decoded literal fragments, including legacy/conditional paths. Full prompt composition and message roles remain authoritative in upstream source.'))
    write(out / 'message_callsites.json', dict(commit=commit, calls=callsites,
        warning='Static call-site index; not evidence a branch executed.'))
    write(out / 'function_index.json', functions)
    default = tomllib.loads((source / 'configs/default.toml').read_text(encoding='utf8'))
    overlay = tomllib.loads((source / 'configs/experiment/paper_godot_gpt.toml').read_text(encoding='utf8'))
    effective = merge(default, overlay)
    write(out / 'paper_file_config.json', dict(config=effective,
        sources=['configs/default.toml', 'configs/experiment/paper_godot_gpt.toml'],
        scope='File layers only. No environment overrides, credentials, argparse defaults, provider/model replacement or runtime resolution.'))
    print(json.dumps(dict(commit=commit,source_files=len(files),literal_candidates=len(literals),message_callsites=len(callsites),out=str(out))))


if __name__ == '__main__':
    main()
