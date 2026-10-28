"""Stable per-user bridge data, independent of packaged-app LocalAppData redirection."""
import os
from pathlib import Path

RUNTIME = Path(os.environ.get('MAGEKNIGHT_FEISHU_HOME', str(Path.home() / '.mageknight-feishu')))


def configure_transport():
    # Only the bridge's Feishu traffic bypasses inherited/system HTTP proxies.
    # Do not alter the user's proxy, VPN, desktop environment or other domains.
    names = [part.strip() for part in os.environ.get('NO_PROXY', os.environ.get('no_proxy', '')).split(',') if part.strip()]
    for domain in ('.feishu.cn', '.larksuite.com'):
        if domain not in names:
            names.append(domain)
    os.environ['NO_PROXY'] = os.environ['no_proxy'] = ','.join(names)


configure_transport()
