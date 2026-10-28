import os,json,hashlib,subprocess,time,concurrent.futures
from pathlib import Path
root=Path(r'D:\study_and_work\unity-MK-test\MageKnight_from_zero')
archive=Path(r'H:\MageKnightArchives\20261025_rsi_history')
summary=[]
free_before=__import__('shutil').disk_usage('D:/').free

def inventory(base):
 files={}; dirs=[]
 for p,ds,fs in os.walk(base,followlinks=False):
  for n in ds+fs:
   q=Path(p)/n
   if q.lstat().st_file_attributes & 1024: raise RuntimeError(f'Nested reparse point: {q}')
  dirs.extend(str((Path(p)/d).relative_to(base)) for d in ds)
  for f in fs:
   q=Path(p)/f;s=q.stat();files[str(q.relative_to(base))]=(s.st_size,s.st_mtime_ns)
 return files,dirs

def digest(p):
 h=hashlib.sha256()
 with open(p,'rb',buffering=1024*1024) as f:
  while b:=f.read(1024*1024): h.update(b)
 return h.hexdigest()
for name in ('experiments','evaluation_iterations'):
 src=root/'AutomationOutputs'/'RsiGame'/name; dst=archive/name
 assert src.resolve()==src and not dst.exists()
 files,dirs=inventory(src); total=sum(s[0] for s in files.values())
 print(f'{name}: copying {len(files)} files, {total/2**30:.2f} GiB',flush=True)
 result=subprocess.run(['robocopy',str(src),str(dst),'/E','/COPY:DAT','/DCOPY:DAT','/R:1','/W:1','/MT:8','/NFL','/NDL','/NJH','/NJS','/NP'],stdout=subprocess.DEVNULL)
 assert result.returncode<8,result.returncode
 destfiles,destdirs=inventory(dst)
 assert set(files)==set(destfiles) and set(dirs)==set(destdirs)
 entries=[]
 def verify(rel):
  n,mtime=files[rel];a=src/rel;b=dst/rel
  assert a.stat().st_size==b.stat().st_size==n
  ha=digest(a);hb=digest(b)
  assert ha==hb,rel
  assert (a.stat().st_size,a.stat().st_mtime_ns)==(n,mtime),f'Source changed: {rel}'
  return dict(relative=rel,bytes=n,sha256=ha)
 with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
  for i,e in enumerate(pool.map(verify,files),1):
   entries.append(e)
   if i%2000==0:print(f'{name}: SHA256 verified {i}/{len(files)}',flush=True)
 assert inventory(src)[0]==files,'Source changed during verification'
 manifest=archive/(name+'.manifest.json')
 record=dict(source=str(src),destination=str(dst),files=len(files),bytes=total,status='verified_before_switch',entries=entries)
 manifest.write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
 backup=Path(str(src)+'.relocation-verified-original-20261025')
 assert not backup.exists() and backup.parent==root/'AutomationOutputs'/'RsiGame'
 def quote(p):return "'"+str(p).replace("'","''")+"'"
 # One native PowerShell handles the checked move, junction, and cleanup.
 ps=f'''$ErrorActionPreference='Stop'
 $s={quote(src)}; $d={quote(dst)}; $b={quote(backup)}
 if ([IO.Path]::GetFullPath($b) -ne {quote(backup)} -or (Get-Item -LiteralPath $s).Attributes -band [IO.FileAttributes]::ReparsePoint) {{throw 'Unsafe source'}}
 Move-Item -LiteralPath $s -Destination $b
 try {{ New-Item -ItemType Junction -Path $s -Target $d | Out-Null }} catch {{ Move-Item -LiteralPath $b -Destination $s; throw }}
 $j=Get-Item -LiteralPath $s
 if ($j.LinkType -ne 'Junction' -or $j.Target -ne $d) {{throw 'Junction mismatch; original retained'}}
 if (!(Test-Path -LiteralPath {quote(manifest)})) {{throw 'Manifest missing'}}
 Remove-Item -LiteralPath $b -Recurse -Force
 '''
 subprocess.run(['pwsh','-NoProfile','-Command',ps],check=True)
 assert inventory(src)[0].keys()==files.keys()
 record['status']='complete_verified_junction';record['completedUtc']=time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime())
 manifest.write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
 summary.append({k:record[k] for k in ('source','destination','files','bytes','status','completedUtc')}|{'manifest':str(manifest)})
 (root/'AutomationOutputs'/'disk_relocation_20261025_rsi_history.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
 print(f'{name}: COMPLETE; D free {__import__("shutil").disk_usage("D:/").free/2**30:.2f} GiB',flush=True)
print('ALL COMPLETE',flush=True)
