"""Verify/extract one explicitly named immutable payload and probe native reset."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import zipfile

archive=Path(sys.argv[1]);destination=Path(sys.argv[2]).resolve();expected=sys.argv[3]
assert hashlib.sha256(archive.read_bytes()).hexdigest()==expected
destination.mkdir(exist_ok=False)
with zipfile.ZipFile(archive) as z:
    for member in z.infolist():
        path=(destination/member.filename).resolve()
        if not path.is_relative_to(destination):raise ValueError('Unsafe bundle member')
    z.extractall(destination)
manifest=json.loads((destination/'payload_manifest.json').read_text())
for name,sha in manifest['files'].items():
    assert hashlib.sha256((destination/name).read_bytes()).hexdigest()==sha,name
sys.path.insert(0,str(destination/'tools/BattleRl'))
from solo_native_env import SoloNativeEnv
from solo_text_assets import SoloTextAssets
from solo_public_schema import PublicSchema
assets=SoloTextAssets(destination/'text_assets',destination)
schema=PublicSchema(destination/'public_dto_schema.json')
family=json.loads((destination/'gate_family.json').read_text())
with SoloNativeEnv(destination/'native/SoloConquestRl.dll',destination,destination/'probe',preparation=True) as env:
    state=env.reset(family['tasks'][0]['setup'])
    world=env._request(op='private_initial_world')
    for key in ('map_tiles','city_colors','city_levels','enemy_supply','ruins','initial_mana'):
        assert world[key]==family['world'][key],key
    report=dict(passed=True,payload_sha256=expected,files=len(manifest['files']),
        native_binding=env.capabilities['binding_sha256'],ruin_inventory_count=env.capabilities['ruin_inventory_count'],
        schema_sha256=schema.signature,catalog_signature=assets.catalog.signature,
        initial_support_count=len(state['commands']),cross_platform_inventory_equal=True,
        formal_training_started=False)
(destination/'deployment_probe.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
