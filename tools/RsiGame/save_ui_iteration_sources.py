"""Save an exclusive, scoped UI version, including loaded DLL bytes after compilation."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[2]


def save(out, reference):
    old = json.loads(reference.read_text(encoding='utf8'))
    names = set(old)
    # Preserve complete partial-class implementations, not just the last edited file.
    names.update(p.relative_to(ROOT).as_posix() for p in (ROOT/'Assets/Scripts/Battle').glob('PveBattleController*.cs*'))
    names.update(p.relative_to(ROOT).as_posix() for p in (ROOT/'Assets/Scripts/SceneAutomation').glob('StructuredUiPilot*.cs*'))
    if (out/'source_manifest.json').exists():
        raise FileExistsError(out/'source_manifest.json')
    manifest = {}
    for name in sorted(names):
        source = ROOT/name
        target = out/'source'/name
        target.parent.mkdir(parents=True, exist_ok=True)
        with source.open('rb') as src, target.open('xb') as dst:
            shutil.copyfileobj(src, dst)
        raw = target.read_bytes()
        manifest[name] = dict(sha256=hashlib.sha256(raw).hexdigest(), bytes=len(raw), backup=str(target.resolve()))
    with (out/'source_manifest.json').open('x', encoding='utf8') as stream:
        json.dump(manifest, stream, ensure_ascii=False, indent=2)
    return dict(saved_files=len(manifest), scope='UI partial classes, referenced assets/scene and compiled DLLs; not whole-project backup')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--reference', type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(save(args.out.resolve(), args.reference.resolve())))
