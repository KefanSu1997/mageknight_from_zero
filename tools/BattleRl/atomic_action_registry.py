"""Immutable disk-backed supports referenced by rollout observations, not recomputed in PPO."""
import json
import threading
from functools import lru_cache
from pathlib import Path

class Registry:
    def __init__(self,path):
        self.path=Path(path);self.path.parent.mkdir(parents=True,exist_ok=True)
        self.lock=threading.RLock();self.offsets=[]
        self.stream=self.path.open('a+b')
        self.stream.seek(0)
        while True:
            offset=self.stream.tell();line=self.stream.readline()
            if not line:break
            if not line.endswith(b'\n'):raise ValueError('Incomplete atomic support registry')
            self.offsets.append(offset)
        if not self.offsets:self.add([[0]]) # Sequence padding / terminal dummy.

    def add(self,sequences):
        with self.lock:
            self.stream.seek(0,2);offset=self.stream.tell()
            self.stream.write(json.dumps(sequences,separators=(',',':')).encode()+b'\n');self.stream.flush()
            self.offsets.append(offset)
            index=len(self.offsets)-1
            if index>=2**24:raise ValueError('Support ID exceeds lossless float32 observation range')
            return index

    @lru_cache(maxsize=64)
    def get(self,index):
        with self.lock:
            self.stream.seek(self.offsets[index]);return json.loads(self.stream.readline())

_registries={}
_lock=threading.Lock()
def registry(path):
    path=str(Path(path).resolve())
    with _lock:
        if path not in _registries:_registries[path]=Registry(path)
        return _registries[path]
