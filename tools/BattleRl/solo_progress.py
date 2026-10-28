"""Best-effort display telemetry. Never use for checkpoints or trajectory evidence."""
import json
import os
from pathlib import Path
import uuid


def publish_progress(path, value):
    path=Path(path)
    temporary=path.with_name(path.name+'.'+uuid.uuid4().hex+'.pending')
    try:
        temporary.write_text(json.dumps(value,indent=2,allow_nan=False),encoding='utf-8')
        os.replace(temporary,path)
        return True
    except OSError:
        # A reader/antivirus may temporarily hold the Windows destination.
        # The next progress snapshot retries; authoritative writes stay strict.
        try:temporary.unlink(missing_ok=True)
        except OSError:pass
        return False
