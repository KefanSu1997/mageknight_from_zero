"""Make verified ZIP volumes and SHA256 inventory. Never delete source files."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import zipfile


def sha_file(path):
    h = hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda: f.read(4 * 1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--root', required=True)
    p.add_argument('--destination', required=True)
    args = p.parse_args()
    root = Path(args.root).resolve()
    out = Path(args.destination).resolve()
    out.mkdir(parents=True, exist_ok=False)
    scopes = ['AutomationOutputs/BattleRl', 'AutomationOutputs/StructuredUiPpo',
              'tools/BattleRl', 'vibe_coding/codex', 'AGENTS.md']
    files = []
    for scope in scopes:
        source = root / scope
        candidates = [source] if source.is_file() else source.rglob('*')
        for f in candidates:
            if f.is_file() and '__pycache__' not in f.parts:
                if f.is_symlink() or not f.resolve().is_relative_to(root):
                    raise ValueError(f'Unexpected external source: {f}')
                files.append(f)
    files.sort()
    expected = {str(f.relative_to(root).as_posix()): (f.stat().st_size, f.stat().st_mtime_ns) for f in files}
    (out/'inventory.json').write_text(json.dumps({'root':str(root), 'scopes':scopes,
        'files':len(files), 'bytes':sum(x[0] for x in expected.values()),
        'source_metadata':expected}, ensure_ascii=False), encoding='utf-8')
    volumes = []
    entries = []
    volume = None
    size = 0
    manifest = (out/'files.sha256.jsonl').open('x', encoding='utf-8')

    def close_volume():
        nonlocal volume, entries
        if volume is None:
            return
        name = Path(volume.filename)
        volume.close()
        with zipfile.ZipFile(name) as check:
            for item in entries:
                h = hashlib.sha256()
                with check.open(item['path']) as f:
                    for block in iter(lambda:f.read(4*1024*1024), b''):
                        h.update(block)
                if h.hexdigest() != item['sha256']:
                    raise ValueError('Archive hash mismatch: '+item['path'])
        done = name.with_suffix('.zip')
        name.rename(done)
        record = {'name':done.name, 'bytes':done.stat().st_size,
                  'sha256':sha_file(done), 'files':len(entries), 'verified':True}
        volumes.append(record)
        (out/'volumes.json').write_text(json.dumps(volumes,indent=2),encoding='utf-8')
        print(json.dumps(record), flush=True)
        entries = []
        volume = None

    try:
        for f in files:
            rel = f.relative_to(root).as_posix()
            if volume is None or size >= 2*1024**3:
                close_volume()
                volume = zipfile.ZipFile(out/f'part_{len(volumes)+1:03d}.pending', 'x',
                                         compression=zipfile.ZIP_DEFLATED, compresslevel=1)
                size = 0
            stat = f.stat()
            if (stat.st_size,stat.st_mtime_ns) != expected[rel]:
                raise ValueError('Source changed before archive: '+rel)
            h = hashlib.sha256()
            with f.open('rb') as src, volume.open(rel,'w',force_zip64=True) as dst:
                for block in iter(lambda:src.read(4*1024*1024),b''):
                    dst.write(block); h.update(block)
            after = f.stat()
            if (after.st_size,after.st_mtime_ns) != expected[rel]:
                raise ValueError('Source changed during archive: '+rel)
            record = {'path':rel,'bytes':stat.st_size,'mtime_ns':stat.st_mtime_ns,
                      'sha256':h.hexdigest(),'volume':f'part_{len(volumes)+1:03d}.zip'}
            manifest.write(json.dumps(record,ensure_ascii=False)+'\n');manifest.flush()
            entries.append(record);size+=stat.st_size
        close_volume()
        # Verify that earlier source files also stayed unchanged throughout packing.
        for f in files:
            stat=f.stat()
            if (stat.st_size,stat.st_mtime_ns)!=expected[f.relative_to(root).as_posix()]:
                raise ValueError('Source changed after archive: '+str(f))
        (out/'LOCAL_VERIFIED.json').write_text(json.dumps({'files':len(files),
            'volumes':len(volumes),'cloud_verified':False,'deletion_authorized_by_this_file':False}),encoding='utf-8')
        print('LOCAL_ARCHIVE_VERIFIED; CLOUD_BACKUP_STILL_REQUIRED',flush=True)
    finally:
        manifest.close()
        if volume is not None:volume.close()


if __name__ == '__main__':
    main()
