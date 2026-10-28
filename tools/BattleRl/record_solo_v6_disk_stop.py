"""Record verified stopped-run facts for the existing tracking renderer."""
import json,time
from pathlib import Path
root=Path('/root/mageknight_solo_conquest_A_enemyshuffle_v6_100_20261022')
assert not Path('/proc/931976').exists()
log=Path(str(root)+'.train.log').read_text()
assert 'RuntimeError: 3 GiB disk protection' in log
metrics=json.loads((root/'metrics.json').read_text())
assert [r['update'] for r in metrics]==[1,2]
assert not (root/'checkpoint_0002.pt').exists()
record=dict(status='review_required',reason='disk_protection_exit_after_completed_update',
    verified_unix=time.time(),trainer_pid=931976,trainer_present=False,completed_updates=2,
    latest_saved_checkpoint=0,unsaved_updates=[1,2],
    restart_started=False,learning_acceptance=False,
    message='训练已停止；两次更新指标和轨迹保留，但对应权重未保存。独立速度门禁进行中。')
(root/'review_required.json').write_text(json.dumps(record,ensure_ascii=False,indent=2))
(root/'phase.json').write_text(json.dumps(dict(pid=931976,update=2,phase='stopped_disk_protection',
    verified_unix=time.time(),latest_saved_checkpoint=0)))
print(json.dumps(record,ensure_ascii=False))
