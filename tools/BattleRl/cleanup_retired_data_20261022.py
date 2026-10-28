"""Explicit old experiments only: stream to H, verify both ends, then unlink verified files."""
import hashlib,json,time,os,posixpath,stat,traceback
from pathlib import Path
import paramiko

NAMES=['remote_constrained_A300','remote_constrained_fresh300','remote_low_cost_A300','paired_cost_diagnosis_20261017']
BASE='/root/autodl-tmp/mageknight'
LOCAL=Path('H:/MageKnight_RL_Runs/autodl_20261013')
STATE=LOCAL/'data_cleanup_20261022';STATE.mkdir(exist_ok=True)
C=json.loads(Path('C:/Users/sukefan/.codex/private/autodl-mageknight/connection.json').read_text())
def sha(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for b in iter(lambda:f.read(8*1024*1024),b''):h.update(b)
    return h.hexdigest()
def connect():
    s=paramiko.SSHClient();s.load_host_keys('C:/Users/sukefan/.codex/private/autodl-mageknight/known_hosts');s.set_missing_host_key_policy(paramiko.RejectPolicy())
    s.connect(C['host'],port=C['port'],username=C['user'],password=C['password'],timeout=20);s.get_transport().set_keepalive(15)
    return s
def remote(s,code):
    import shlex
    _,out,err=s.exec_command('/root/autodl-tmp/mkvenv/bin/python -c '+shlex.quote(code),timeout=120)
    data=out.read();error=err.read();rc=out.channel.recv_exit_status()
    if rc:raise RuntimeError(error.decode())
    return json.loads(data)
def guard(s,root):
    remote(s,"from pathlib import Path;import json; r="+repr(root)+";assert Path(r).resolve()==Path(r) and Path(r).is_dir(); hits=[]\nfor p in Path('/proc').glob('[0-9]*/cmdline'):\n try:c=p.read_bytes()\n except (FileNotFoundError,PermissionError):continue\n if r.encode() in c and b'python -c' not in c and b'/python\\x00-c\\x00' not in c:hits.append(str(p))\nassert not hits,hits\nprint(json.dumps(True))")
removed=0;verified=0;copied=0
for name in NAMES:
    root=BASE+'/'+name;local=LOCAL/name
    for attempt in range(10):
        s=None
        try:
            s=connect();guard(s,root)
            with s.open_sftp() as f:
                f.get_channel().settimeout(90)
                files=[];dirs=[]
                def walk(p):
                    dirs.append(p)
                    for e in f.listdir_attr(p):
                        q=p+'/'+e.filename
                        if stat.S_ISDIR(e.st_mode):walk(q)
                        elif stat.S_ISREG(e.st_mode) or stat.S_ISLNK(e.st_mode):files.append(q)
                        else:raise ValueError('Unsupported entry '+q)
                walk(root)
                for i,p in enumerate(files):
                    rel=p[len(root)+1:];dest=local/rel
                    assert dest.resolve().is_relative_to(local.resolve())
                    info=remote(s,"from pathlib import Path;import hashlib,json; p=Path("+repr(p)+");h=hashlib.sha256();f=p.open('rb')\nfor b in iter(lambda:f.read(8388608),b''):h.update(b)\nprint(json.dumps({'bytes':p.stat().st_size,'sha256':h.hexdigest(),'link':str(p.readlink()) if p.is_symlink() else None}))")
                    if not dest.exists() or dest.stat().st_size!=info['bytes'] or sha(dest)!=info['sha256']:
                        dest.parent.mkdir(parents=True,exist_ok=True);tmp=dest.with_name(dest.name+'.cleanup-pending')
                        f.get(p,str(tmp),max_concurrent_prefetch_requests=8)
                        assert tmp.stat().st_size==info['bytes'] and sha(tmp)==info['sha256'];os.replace(tmp,dest);copied+=1
                    receipt=dict(remote=p,local=str(dest),**info,time=time.time())
                    with (STATE/'verified.jsonl').open('a') as log:log.write(json.dumps(receipt)+'\n');log.flush();os.fsync(log.fileno())
                    # Recheck immutable source before unlinking exact file; symlink targets stay untouched.
                    remote(s,"from pathlib import Path;import hashlib,json;p=Path("+repr(p)+");root=Path("+repr(root)+");assert p.parent.resolve().is_relative_to(root);h=hashlib.sha256();f=p.open('rb')\nfor b in iter(lambda:f.read(8388608),b''):h.update(b)\nf.close();assert h.hexdigest()=="+repr(info['sha256'])+";p.unlink();print(json.dumps(True))")
                    removed+=0 if info['link'] else info['bytes'];verified+=1
                    (STATE/'status.json').write_text(json.dumps(dict(state='running',target=name,remaining=len(files)-i-1,verified=verified,copied=copied,released_bytes=removed,time=time.time())))
                guard(s,root)
                for d in reversed(dirs):f.rmdir(d)
            print(json.dumps(dict(completed=name,released_bytes=removed,verified=verified)),flush=True);break
        except Exception:
            (STATE/'last_error.txt').write_text(traceback.format_exc());print(traceback.format_exc(),flush=True)
            if attempt==9:raise
            time.sleep(5)
        finally:
            if s:s.close()
(STATE/'status.json').write_text(json.dumps(dict(state='complete',verified=verified,copied=copied,released_bytes=removed,time=time.time())))
