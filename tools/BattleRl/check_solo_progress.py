import json
from pathlib import Path
import tempfile
from unittest.mock import patch
from solo_progress import publish_progress

root=Path(tempfile.mkdtemp(prefix='solo_progress_gate_'));target=root/'progress.json'
assert publish_progress(target,dict(step=1))
with patch('solo_progress.os.replace',side_effect=PermissionError('simulated Windows sharing violation')):
    assert not publish_progress(target,dict(step=2))
assert json.loads(target.read_text())==dict(step=1)
assert not list(root.glob('*.pending'))
assert publish_progress(target,dict(step=3))
assert json.loads(target.read_text())==dict(step=3)
print(json.dumps(dict(passed=True,scope='display_only_sharing_violation_and_recovery',path=str(root))))
