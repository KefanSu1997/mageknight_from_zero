"""Optional process-local source hashing; no file-list or digest-format changes.

Never persist metadata hits across restarts. A changed or unreadable file is
rehashed or fails the caller, never certified from stale cache state.
"""
from __future__ import annotations

import hashlib
import os
from pathlib import Path


def _windows_change_time(path):
    # Windows st_ctime_ns is creation time, not the NTFS change timestamp.
    # Failure disables reuse for this file instead of weakening the key.
    import ctypes
    from ctypes import wintypes

    class BasicInfo(ctypes.Structure):
        _fields_ = [(name, ctypes.c_longlong) for name in
                    ('creation', 'access', 'write', 'change')] + [('attributes', wintypes.DWORD)]

    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    create = kernel.CreateFileW
    create.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD,
                       ctypes.c_void_p, wintypes.DWORD, wintypes.DWORD, wintypes.HANDLE]
    create.restype = wintypes.HANDLE
    info = kernel.GetFileInformationByHandleEx
    info.argtypes = [wintypes.HANDLE, ctypes.c_int, ctypes.c_void_p, wintypes.DWORD]
    info.restype = wintypes.BOOL
    close = kernel.CloseHandle
    close.argtypes = [wintypes.HANDLE]
    handle = create(str(path), 0x80, 7, None, 3, 0, None)
    if handle == wintypes.HANDLE(-1).value:
        return None
    try:
        basic = BasicInfo()
        if not info(handle, 0, ctypes.byref(basic), ctypes.sizeof(basic)):
            return None
        return basic.change
    finally:
        close(handle)


class SourceHashCache:
    def __init__(self):
        self._entries = {}
        self._verified_blobs = {}
        self.hits = self.reads = self.bytes_read = 0

    def _key(self, path):
        stat = path.stat()
        change = _windows_change_time(path) if os.name == 'nt' else stat.st_ctime_ns
        if change is None:
            return None
        return (str(path.resolve()), stat.st_mtime_ns, stat.st_ctime_ns,
                stat.st_size, stat.st_dev, stat.st_ino, change)

    def _blob_valid(self, blob):
        key = self._key(blob) if blob.is_file() else None
        return key is not None and self._verified_blobs.get(str(blob.resolve())) == key

    def _put_blob(self, store, digest, raw):
        blob = Path(store) / digest[:2] / digest
        if self._blob_valid(blob):
            return
        blob.parent.mkdir(parents=True, exist_ok=True)
        try:
            with blob.open('xb') as stream:
                stream.write(raw)
        except FileExistsError:
            before = self._key(blob)
            if hashlib.sha256(blob.read_bytes()).hexdigest() != digest:
                raise RuntimeError(f'Corrupt content-addressed source blob: {blob}')
            if self._key(blob) != before:
                raise RuntimeError(f'Source blob changed while verifying: {blob}')
        self._verified_blobs[str(blob.resolve())] = self._key(blob)

    def file(self, path, store=None):
        path = Path(path)
        for _ in range(3):
            key = self._key(path)
            previous = self._entries.get(str(path.resolve()))
            if key is not None and previous is not None and previous[0] == key:
                digest, size = previous[1:]
                blob = Path(store) / digest[:2] / digest if store is not None else None
                if blob is None or self._blob_valid(blob):
                    if self._key(path) == key:
                        self.hits += 1
                        return digest, size
            raw = path.read_bytes()
            self.reads += 1
            self.bytes_read += len(raw)
            after = self._key(path)
            if key is not None and after != key:
                continue
            digest = hashlib.sha256(raw).hexdigest()
            if store is not None:
                self._put_blob(store, digest, raw)
            # Missing native metadata: hash normally on every request.
            if key is not None:
                self._entries[str(path.resolve())] = (key, digest, len(raw))
            return digest, len(raw)
        raise RuntimeError(f'Source changed repeatedly while hashing: {path}')

    def hashes(self, root, files):
        root = Path(root)
        return {p.relative_to(root).as_posix(): self.file(p)[0] for p in files}

    def project_hash(self, root, files):
        root = Path(root)
        result = hashlib.sha256()
        for path in files:
            result.update(path.relative_to(root).as_posix().encode())
            result.update(b'\0')
            # Fail closed on I/O errors instead of allowing an unreadable-file
            # placeholder to become a passed replay-comparability gate.
            result.update(bytes.fromhex(self.file(path)[0]))
        return result.hexdigest()[:16]
