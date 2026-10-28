"""Verify/extract a new immutable runtime; no formal launch."""
import hashlib,json,os,sys,zipfile
from pathlib import Path
source=Path('/root/autodl-tmp/solo_runtime_enemyshuffle_v6_bound.zip')
root=Path('/root/autodl-tmp/solo_conquest_enemyshuffle_v6')
assert hashlib.sha256(source.read_bytes()).hexdigest()=='93c439368fee9aeebb6102db195a3e286c429d1fe1399f0ffb994eb737f14542'
root.mkdir(exist_ok=False)
with zipfile.ZipFile(source) as z:
    for name in z.namelist():
        p=(root/name).resolve()
        assert root in p.parents and not name.startswith('/')
    z.extractall(root)
manifest=json.loads((root/'payload_manifest.json').read_text())
for name,digest in manifest['files'].items():
    assert hashlib.sha256((root/name).read_bytes()).hexdigest()==digest,name
os.environ['PATH']='/root/autodl-tmp/dotnet:'+os.environ['PATH']
sys.path.insert(0,str(root/'tools/BattleRl'))
from solo_native_env import SoloNativeEnv
from solo_text_assets import SoloTextAssets
assets=SoloTextAssets(root/'text_assets',root)
with SoloNativeEnv(root/'native/SoloConquestRl.dll',root,root/'deployment_probe',preparation=True) as env:
    schema=env._request(op='public_schema')
    expected=json.loads((root/'public_dto_schema.json').read_text())
    assert schema==expected,'Public input schema changed'
    report=dict(passed=True,native_binding=env.capabilities['binding_sha256'],
                catalog=assets.catalog.signature,files_verified=len(manifest['files']),formal_training_started=False)
(root/'deployment_probe.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
