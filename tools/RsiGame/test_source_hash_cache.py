"""Run with python -m unittest discover -s tools/RsiGame -p test_source_hash_cache.py."""
import hashlib
import json
import os
from pathlib import Path
import tempfile
import unittest

from run_unity_experiment import snapshot, source_files
from source_hash_cache import SourceHashCache


def original_project_hash(root):
    result = hashlib.sha256()
    for path in sorted(source_files(root)):
        result.update(path.relative_to(root).as_posix().encode())
        result.update(b'\0')
        result.update(hashlib.sha256(path.read_bytes()).digest())
    return result.hexdigest()[:16]


class SourceHashesTest(unittest.TestCase):
    def test_equivalence_mutation_and_immutable_snapshots(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp)
            root = base / 'project'
            (root / 'Assets').mkdir(parents=True)
            code = root / 'Assets/code.cs'
            code.write_bytes(b'initial-code')
            asset = root / 'Assets/texture.bin'
            asset.write_bytes(bytes(range(256)) * 512)
            (root / 'Library').mkdir()
            (root / 'Library/ignored').write_bytes(b'cache')
            cache = SourceHashCache()

            def compare(round_number):
                paths = list(source_files(root))
                self.assertEqual(cache.hashes(root, paths),
                    {p.relative_to(root).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in paths})
                self.assertEqual(cache.project_hash(root, sorted(paths)), original_project_hash(root))
                plain = base / f'plain{round_number}.json'
                cached = base / f'cached{round_number}.json'
                snapshot(root, plain, base / 'plain_blobs')
                snapshot(root, cached, base / 'cached_blobs', cache)
                self.assertEqual(json.loads(plain.read_text()), json.loads(cached.read_text()))
                for entry in json.loads(cached.read_text())['files'].values():
                    sha = entry['sha256']
                    self.assertEqual(hashlib.sha256((base / 'cached_blobs' / sha[:2] / sha).read_bytes()).hexdigest(), sha)

            compare(0)
            reads = cache.reads
            compare(1)
            self.assertEqual(cache.reads, reads)  # unchanged assets do not reread
            old_stat = code.stat()
            code.write_bytes(b'changed-code')  # exact same size, mtime restored
            os.utime(code, ns=(old_stat.st_atime_ns, old_stat.st_mtime_ns))
            compare(2)
            # One digest read and one CAS population read for changed code;
            # the much larger unchanged asset remains a metadata hit.
            self.assertEqual(cache.reads, reads + 2)
            (root / 'Assets/new.asset').write_bytes(b'new')
            compare(3)
            code.unlink()
            compare(4)
            manifest = base / 'cached0.json'
            old = manifest.read_bytes()
            snapshot(root, manifest, base / 'cached_blobs', cache)
            self.assertEqual(manifest.read_bytes(), old)

    def test_corrupt_preexisting_blob_and_deleted_source_fail_closed(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source = root / 'source'
            source.write_bytes(b'source')
            sha = hashlib.sha256(b'source').hexdigest()
            blob = root / 'store' / sha[:2] / sha
            blob.parent.mkdir(parents=True)
            blob.write_bytes(b'corrupt')
            cache = SourceHashCache()
            with self.assertRaises(RuntimeError):
                cache.file(source, root / 'store')
            source.unlink()
            with self.assertRaises(OSError):
                cache.file(source)


if __name__ == '__main__':
    unittest.main()
