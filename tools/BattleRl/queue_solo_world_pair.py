import json,os,subprocess,sys,time
from pathlib import Path
root=Path('/root/autodl-tmp');status=root/'solo_world_pair_status.json'
def write(**kw):status.write_text(json.dumps(dict(pid=os.getpid(),time=time.time(),**kw)))
proc=Path('/proc/713117');initial=None
if proc.exists():
    command=(proc/'cmdline').read_bytes()
    assert b'export_solo_world_families_v2.py' in command and b'solo_world1200_native_v2' in command
    initial=(proc/'stat').read_text().split(') ',1)[1].split()[19]
while initial is not None:
    try:current=(proc/'stat').read_text().split(') ',1)[1].split()[19]
    except FileNotFoundError:break
    if current!=initial:break
    write(state='waiting_identified_world_export',export_pid=713117);time.sleep(20)
if not (root/'solo_world1200_native_v2/complete.json').exists():
    write(state='export_incomplete');sys.exit(1)
env=dict(os.environ,PYTHONPATH=str(root/'solo_conquest_runtime_v2/tools/BattleRl'))
with (root/'solo_world_pair.log').open('x') as log:
    p=subprocess.Popen([sys.executable,str(root/'pair_solo_world_exports.py'),'--old',str(root/'solo_world1200_original_exports'),
        '--new',str(root/'solo_world1200_native_v2'),'--out',str(root/'solo_world1200_v2_preserved_splits')],env=env,stdout=log,stderr=subprocess.STDOUT)
    write(state='verifying_all_worlds',child_pid=p.pid);code=p.wait()
write(state='completed' if code==0 else 'failed',returncode=code)
