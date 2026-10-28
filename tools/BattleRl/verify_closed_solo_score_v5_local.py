"""Complete SHA audit of the closed gate; uploads receipt but does not delete."""
import hashlib,json,os,uuid
from pathlib import Path,PurePosixPath
import paramiko
LOCAL=Path('H:/MageKnight_RL_Runs/autodl_20261013/solo_conquest_A_score_v5_100_20261022')
REMOTE='/root/autodl-tmp/closed_solo_score_v5_retirement'
def sha(path):
    h=hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(8*1024*1024),b''):h.update(block)
    return h.hexdigest()
c=json.loads(Path('C:/Users/sukefan/.codex/private/autodl-mageknight/connection.json').read_text())
s=paramiko.SSHClient();s.load_host_keys('C:/Users/sukefan/.codex/private/autodl-mageknight/known_hosts');s.set_missing_host_key_policy(paramiko.RejectPolicy())
s.connect(c['host'],port=c['port'],username=c['user'],password=c['password'],timeout=20)
s.get_transport().set_keepalive(15)
try:
    with s.open_sftp() as f:
        f.get_channel().settimeout(60)
        with f.open(REMOTE+'/manifest.json') as stream:manifest=json.loads(stream.read())
        copied=0
        for row in manifest['files']:
            rel=PurePosixPath(row['path']);assert not rel.is_absolute() and '..' not in rel.parts
            local=LOCAL/rel;assert local.resolve().is_relative_to(LOCAL.resolve())
            if not local.exists() or local.stat().st_size!=row['bytes'] or sha(local)!=row['sha256']:
                local.parent.mkdir(parents=True,exist_ok=True);pending=local.with_name(local.name+'.'+uuid.uuid4().hex+'.pending')
                f.get(manifest['root']+'/'+row['path'],str(pending),max_concurrent_prefetch_requests=8)
                assert pending.stat().st_size==row['bytes'] and sha(pending)==row['sha256']
                os.replace(pending,local);copied+=1
        receipt=dict(verified=True,local=str(LOCAL),manifest=manifest)
        path=LOCAL/'retirement_full_sha_receipt.json';path.write_text(json.dumps(receipt))
        f.put(str(path),REMOTE+'/receipt.json')
        print(json.dumps(dict(verified=True,files=len(manifest['files']),copied=copied,bytes=sum(r['bytes'] for r in manifest['files']))))
finally:s.close()


