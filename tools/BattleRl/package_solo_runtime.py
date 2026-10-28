"""Small immutable Linux-compatible payload; excludes images and old experiments."""
import argparse
import hashlib
import json
from pathlib import Path
import zipfile

p=argparse.ArgumentParser();p.add_argument('--evidence',required=True);p.add_argument('--out',required=True)
p.add_argument('--native-dir');p.add_argument('--text-assets');args=p.parse_args()
root=Path(__file__).resolve().parents[2];e=Path(args.evidence);files={}
def add(path,name):
    if name in files:raise ValueError('Duplicate bundle member')
    files[name]=path
for path in (root/'tools/BattleRl').glob('solo_*.py'):add(path,'tools/BattleRl/'+path.name)
for name in ('train_solo_conquest.py','evaluate_solo_conquest.py','collect_solo_policy_gate.py'):
    add(root/'tools/BattleRl'/name,'tools/BattleRl/'+name)
for folder in ('Assets/Logic','tools/SoloConquestRl'):
    for path in (root/folder).rglob('*.cs'):
        if not any(part in ('obj','bin') for part in path.parts):
            add(path,'source_snapshot/'+path.relative_to(root).as_posix())
for folder in ('text_json','original_maps','original_ruins','policy_text'):
    for path in (root/'resources'/folder).rglob('*'):
        if path.is_file() and path.suffix in ('.json','.md'):add(path,path.relative_to(root).as_posix())
supp=json.loads((root/'resources/policy_text/solo_native_rules.json').read_text(encoding='utf-8-sig'))
for relative in sorted({s for r in supp['entries'] for s in r['sources']}):add(root/relative,relative)
for path in (Path(args.native_dir) if args.native_dir else root/'tools/SoloConquestRl/bin/Debug/net8.0').iterdir():
    if path.suffix=='.dll' or path.name.startswith('SoloConquestRl.') and path.suffix=='.json':add(path,'native/'+path.name)
add(e/'public_dto_schema.json','public_dto_schema.json')
for path in (Path(args.text_assets) if args.text_assets else e/'solo_text_assets_v2').iterdir():
    if path.is_file():add(path,'text_assets/'+path.name)
add(e/'world1200_native_v1/family_0000.json','gate_family.json')
manifest=dict(version=1,formal_training_started=False,files={n:hashlib.sha256(p.read_bytes()).hexdigest() for n,p in files.items()})
with zipfile.ZipFile(args.out,'x',compression=zipfile.ZIP_DEFLATED) as z:
    for name,path in files.items():z.write(path,name)
    z.writestr('payload_manifest.json',json.dumps(manifest,indent=2))
print(json.dumps(dict(path=args.out,files=len(files),bytes=Path(args.out).stat().st_size,sha256=hashlib.sha256(Path(args.out).read_bytes()).hexdigest())))
