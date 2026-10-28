"""Second cleanup pass, only after complete volume hash verification succeeded."""
import json
from pathlib import Path
from clean_verified_local_traces import ROOT,BACKUP,sha

assert (ROOT/'AutomationOutputs/BackupInventory_20261012/volumes_reverified.json').exists()
count=total=0
with (ROOT/'AutomationOutputs/BackupInventory_20261012/bulk_cleanup.jsonl').open('a') as log:
    for line in (BACKUP/'files.sha256.jsonl').open(encoding='utf-8'):
        r=json.loads(line);p=ROOT/r['path']
        selected=(p.name.startswith('episode_') and p.suffix in ('.json','.npz')) or p.name=='trajectory_audits.json'
        if not r['path'].startswith('AutomationOutputs/BattleRl/') or not selected or not p.exists():continue
        assert p.resolve().is_relative_to((ROOT/'AutomationOutputs/BattleRl').resolve())
        assert not p.is_symlink() and sha(p)==r['sha256'],str(p)
        log.write(json.dumps(dict(**r,action='delete_H_verified_local_evidence',cloud_verified=False))+'\n');log.flush()
        p.unlink();count+=1;total+=r['bytes']
print(json.dumps(dict(deleted_files=count,freed_bytes=total)),flush=True)
