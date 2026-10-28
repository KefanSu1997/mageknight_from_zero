"""Read-only remote tracking mirror; no training launch, stop or deletion."""
import argparse,json,os,posixpath,time,uuid
from pathlib import Path,PurePosixPath
import paramiko
from solo_tracking_page import render


def sync(remote,local):
    credentials=json.loads(Path('C:/Users/sukefan/.codex/private/autodl-mageknight/connection.json').read_text())
    client=paramiko.SSHClient();client.load_host_keys('C:/Users/sukefan/.codex/private/autodl-mageknight/known_hosts')
    client.set_missing_host_key_policy(paramiko.RejectPolicy())
    client.connect(credentials['host'],port=credentials['port'],username=credentials['user'],password=credentials['password'],timeout=20)
    try:
        with client.open_sftp() as sftp:
            sftp.get_channel().settimeout(45)
            names=sftp.listdir(remote)
            files=[name for name in names if name in ('metrics.json','diagnostics.json','phase.json','optimizer_progress.json','review_required.json','training_complete.json','contract.json')
                   or name.startswith('trajectory_review_') and name.endswith('.json')]
            for name in names:
                if name.startswith('eval_') and name[5:].isdigit():
                    for child in ('summary.json','collection/progress.json'):
                        try:sftp.stat(posixpath.join(remote,name,child));files.append(name+'/'+child)
                        except FileNotFoundError:pass
            rollouts=sorted(name for name in names if name.startswith('rollout_') and name[8:].isdigit())
            if rollouts:
                try:sftp.stat(posixpath.join(remote,rollouts[-1],'progress.json'));files.append(rollouts[-1]+'/progress.json')
                except FileNotFoundError:pass
            try:files+=['evaluation_requests/'+n for n in sftp.listdir(remote+'/evaluation_requests') if n.endswith('.json')]
            except FileNotFoundError:pass
            for name in files:
                rel=PurePosixPath(name)
                if rel.is_absolute() or '..' in rel.parts:raise ValueError('Unsafe remote file name')
                with sftp.open(posixpath.join(remote,name),'rb') as stream:data=stream.read()
                json.loads(data)
                target=local/name;target.parent.mkdir(parents=True,exist_ok=True)
                pending=target.with_name(target.name+'.'+uuid.uuid4().hex+'.pending')
                pending.write_bytes(data);os.replace(pending,target)
            (local/'sync_status.json').write_text(json.dumps(dict(last_success_unix=time.time(),remote=remote,files=len(files),
                scope='Tracking metadata only; full trajectory/model backup not implied')))
            target=local/'compare.html';pending=target.with_name(target.name+'.'+uuid.uuid4().hex+'.pending')
            pending.write_text(render(local),encoding='utf-8');os.replace(pending,target)
    finally:client.close()

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--remote',required=True);p.add_argument('--local',required=True);p.add_argument('--watch',action='store_true');a=p.parse_args()
    local=Path(a.local);local.mkdir(parents=True,exist_ok=True)
    while True:
        try:sync(a.remote,local)
        except Exception as error:
            if not a.watch:raise
            print(type(error).__name__,str(error),flush=True)
        if not a.watch:break
        time.sleep(30)
