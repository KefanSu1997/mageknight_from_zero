"""User-authorized local cleanup after verified H-drive backup; no cloud claim."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BACKUP = Path('H:/MageKnight_RL_Backups/20261012_before_reward_v2')

def sha(path):
    h = hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda: f.read(4*1024*1024), b''):
            h.update(block)
    return h.hexdigest()

if __name__ == '__main__':
    assert (BACKUP/'LOCAL_VERIFIED.json').exists()
    volumes = json.loads((BACKUP/'volumes.json').read_text())
    for v in volumes:
        assert v['verified'] and sha(BACKUP/v['name']) == v['sha256'], v['name']
    log = ROOT/'AutomationOutputs/BackupInventory_20261012/local_cleanup.jsonl'
    count = total = 0
    with log.open('a', encoding='utf-8') as out:
        for line in (BACKUP/'files.sha256.jsonl').open(encoding='utf-8'):
            r = json.loads(line)
            p = ROOT/r['path']
            # Only old archived SB3 per-episode evidence, not datasets/checkpoints/code.
            if not (r['path'].startswith('AutomationOutputs/BattleRl/') and
                    p.name.startswith('worker_') and '_episode_' in p.name and
                    p.suffix == '.json' and r['bytes'] > 1024*1024):
                continue
            if not p.exists(): continue
            assert p.resolve().is_relative_to((ROOT/'AutomationOutputs/BattleRl').resolve())
            assert not p.is_symlink() and sha(p) == r['sha256'], str(p)
            out.write(json.dumps(dict(**r, action='delete_verified_local_trace', cloud_verified=False))+'\n')
            out.flush()
            p.unlink()
            count += 1; total += r['bytes']
    print(json.dumps(dict(deleted_files=count, freed_bytes=total)), flush=True)
