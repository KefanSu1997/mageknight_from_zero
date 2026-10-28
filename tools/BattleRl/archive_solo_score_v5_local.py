"""Copy every closed evidence group to H with per-file SHA; never delete remote."""
import argparse,hashlib,json,os,shlex,time,uuid
from pathlib import Path,PurePosixPath
import paramiko
REMOTE_SCRIPT='/root/autodl-tmp/solo_score_v5_archive_remote.py'
LOCAL=Path('H:/MageKnight_RL_Runs/autodl_20261013/solo_conquest_A_score_v5_100_20261022')
def sha(path):
    h=hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(8*1024*1024),b''):h.update(block)
    return h.hexdigest()
def run():
    c=json.loads(Path('C:/Users/sukefan/.codex/private/autodl-mageknight/connection.json').read_text())
    s=paramiko.SSHClient();s.load_host_keys('C:/Users/sukefan/.codex/private/autodl-mageknight/known_hosts');s.set_missing_host_key_policy(paramiko.RejectPolicy())
    s.connect(c['host'],port=c['port'],username=c['user'],password=c['password'],timeout=20);s.get_transport().set_keepalive(15)
    def command(*args):
        _,out,err=s.exec_command(' '.join(shlex.quote(x) for x in ['/root/autodl-tmp/mkvenv/bin/python',REMOTE_SCRIPT,*args]),timeout=60)
        data=out.read();error=err.read()
        if out.channel.recv_exit_status()!=0:raise RuntimeError(error.decode(errors='replace'))
        return json.loads(data)
    try:
        groups=command('list')
        with s.open_sftp() as f:
            f.get_channel().settimeout(60)
            for group in groups:
                assert '/' not in group and '..' not in group
                receipt=LOCAL/'archive_receipts'/(group+'.json')
                if receipt.exists():continue
                started=time.perf_counter();manifest=command('manifest',group);copied=0
                for row in manifest['files']:
                    rel=PurePosixPath(row['path']);assert not rel.is_absolute() and '..' not in rel.parts
                    target=LOCAL/rel;assert target.resolve().is_relative_to(LOCAL.resolve())
                    if target.exists() and target.stat().st_size==row['bytes'] and sha(target)==row['sha256']:continue
                    target.parent.mkdir(parents=True,exist_ok=True);pending=target.with_name(target.name+'.'+uuid.uuid4().hex+'.pending')
                    f.get(manifest['root']+'/'+row['path'],str(pending))
                    if pending.stat().st_size!=row['bytes'] or sha(pending)!=row['sha256']:raise ValueError('Transferred file SHA differs')
                    os.replace(pending,target);copied+=1
                value=dict(verified=True,manifest=manifest,transfer_and_verification_seconds=time.perf_counter()-started,completed_unix=time.time())
                receipt.parent.mkdir(parents=True,exist_ok=True);pending=receipt.with_name(receipt.name+'.'+uuid.uuid4().hex+'.pending')
                pending.write_text(json.dumps(value));os.replace(pending,receipt)
                print(json.dumps(dict(group=group,files=len(manifest['files']),copied=copied,seconds=value['transfer_and_verification_seconds'])),flush=True)
    finally:s.close()
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--watch',action='store_true');a=p.parse_args()
    while True:
        try:run()
        except Exception as error:
            if not a.watch:raise
            print(type(error).__name__,str(error),flush=True)
        if not a.watch:break
        time.sleep(30)
