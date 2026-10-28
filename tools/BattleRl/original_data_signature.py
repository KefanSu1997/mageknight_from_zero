"""Runtime JSON inputs are part of rule identity, independently of DLL bytes."""
import hashlib
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]


def world_source_hash():
    return hashlib.sha256((ROOT/'resources/original_maps/tutorial_opening.json').read_bytes()).hexdigest()


def data_signature(folder=None):
    folder=Path(folder) if folder is not None else ROOT/'resources/text_json'
    files={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(folder.glob('*.json'))}
    if not files:
        raise ValueError('No original runtime JSON inputs found')
    encoded=json.dumps(files,sort_keys=True,separators=(',',':')).encode()
    return dict(schema_version=1,sha256=hashlib.sha256(encoded).hexdigest(),files=files)
