"""Local health snapshot; never trust a stale connected flag as online."""
import json
import time
from feishu_runtime import RUNTIME


def read(name):
    try:
        return json.loads((RUNTIME/name).read_text(encoding='utf-8'))
    except (OSError, ValueError):
        return {}


def snapshot():
    service, status = read('service.json'), read('status.json')
    now = time.time()
    service_age, bridge_age = now-service.get('updated',0), now-status.get('heartbeat',0)
    current = bool(status.get('instance')) and service.get('instance') == status.get('instance')
    healthy = service_age < 45 and bridge_age < 180 and current and status.get('workers_ok',False)
    return {'runtime':str(RUNTIME), 'service_state':service.get('state','missing'),
        'service_age_seconds':round(service_age,1), 'bridge_age_seconds':round(bridge_age,1),
        'current_instance':current, 'feishu_online':bool(healthy and status.get('connected')),
        'workers_ok':status.get('workers_ok',False), 'outbox_pending':status.get('outbox_pending'),
        'history_error':status.get('history_error'), 'history_checked':status.get('history_checked'),
        'last_received':status.get('last_received'), 'last_reply':status.get('last_reply')}


if __name__ == '__main__':
    print(json.dumps(snapshot(),ensure_ascii=False,indent=2))
