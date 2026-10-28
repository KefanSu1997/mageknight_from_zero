"""Atomic cost evidence output; compact JSON preserves all captured fields."""
import json
import shutil
import os
import time
from pathlib import Path
from structured_ui_ppo import write_json


def write_result(path,value,compact=False):
    if not compact:
        return write_json(path,value)
    path=Path(path);path.parent.mkdir(parents=True,exist_ok=True)
    temporary=path.with_suffix('.tmp')
    temporary.write_text(json.dumps(value,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
    for attempt in range(40):
        try:
            os.replace(temporary,path)
            return
        except PermissionError:
            if attempt==39:raise
            time.sleep(.025)


def write_case_bundle(path,case_paths,metadata):
    """Reuse immutable JSON cases without repeatedly encoding their snapshots."""
    if 'samples' in metadata:
        raise ValueError('Bundle metadata cannot override samples')
    path=Path(path);path.parent.mkdir(parents=True,exist_ok=True)
    temporary=path.with_suffix('.tmp')
    with temporary.open('wb') as output:
        output.write(b'{"samples":[')
        for index,case_path in enumerate(case_paths):
            if index:output.write(b',')
            with Path(case_path).open('rb') as source:
                shutil.copyfileobj(source,output,1024*1024)
        output.write(b']')
        encoded=json.dumps(metadata,ensure_ascii=False,separators=(',',':')).encode('utf-8')
        if metadata:output.write(b','+encoded[1:-1])
        output.write(b'}')
    for attempt in range(40):
        try:
            os.replace(temporary,path)
            return
        except PermissionError:
            if attempt==39:raise
            time.sleep(.025)
