"""Copy immutable history, hashing the actual copied bytes and destination independently."""
import concurrent.futures as cf
import hashlib
import json
import os
import shutil
from pathlib import Path

SOURCE = Path(r'D:\study_and_work\unity-MK-test\MageKnight_from_zero\AutomationOutputs\UiFluency')
TARGET = Path(r'H:\MageKnightArchives\20261025_history_additional\UiFluency')
MANIFEST = TARGET.parent / 'UiFluency.manifest.json'
assert SOURCE.is_dir() and not SOURCE.is_junction() and not TARGET.exists()
paths = []
directories = []
for directory, children, files in os.walk(SOURCE):
    base = Path(directory)
    assert not any((base / name).is_junction() or (base / name).is_symlink() for name in children + files)
    directories.append(base.relative_to(SOURCE))
    paths.extend(base / name for name in files)
total = sum(p.stat().st_size for p in paths)
assert shutil.disk_usage(TARGET.parent).free > total + 2 * 1024**3
for relative in directories:
    (TARGET / relative).mkdir(parents=True, exist_ok=True)

def copy_verified(source):
    before = source.stat()
    destination = TARGET / source.relative_to(SOURCE)
    digest = hashlib.sha256()
    with source.open('rb') as incoming, destination.open('xb') as outgoing:
        for chunk in iter(lambda: incoming.read(4 * 1024**2), b''):
            digest.update(chunk)
            outgoing.write(chunk)
    shutil.copystat(source, destination)
    check = hashlib.sha256()
    with destination.open('rb') as saved:
        for chunk in iter(lambda: saved.read(4 * 1024**2), b''):
            check.update(chunk)
    after = source.stat()
    assert (before.st_size, before.st_mtime_ns) == (after.st_size, after.st_mtime_ns), source
    assert digest.digest() == check.digest() and destination.stat().st_size == before.st_size, source
    return dict(relative=str(source.relative_to(SOURCE)), bytes=before.st_size,
                mtime_ns=before.st_mtime_ns, sha256=digest.hexdigest())

entries = []
copied = 0
print(f'Copying and verifying {len(paths)} files, {total / 2**30:.2f} GiB', flush=True)
with cf.ThreadPoolExecutor(max_workers=8) as pool:
    futures = [pool.submit(copy_verified, p) for p in paths]
    for future in cf.as_completed(futures):
        row = future.result()
        entries.append(row)
        copied += row['bytes']
        if len(entries) % 2000 == 0:
            print(f'Verified {len(entries)}/{len(paths)} files, {copied / 2**30:.2f} GiB', flush=True)
current = {str(Path(d, f).relative_to(SOURCE)) for d, _, fs in os.walk(SOURCE) for f in fs}
assert current == {entry['relative'] for entry in entries}
for entry in entries:
    status = (SOURCE / entry['relative']).stat()
    assert (status.st_size, status.st_mtime_ns) == (entry['bytes'], entry['mtime_ns'])
record = dict(source=str(SOURCE), destination=str(TARGET), files=len(entries), bytes=total,
              status='all_copied_bytes_and_destination_sha256_verified', entries=entries)
MANIFEST.write_text(json.dumps(record, ensure_ascii=False, indent=2), encoding='utf-8')
print('All files verified; originals retained pending junction switch.', flush=True)
