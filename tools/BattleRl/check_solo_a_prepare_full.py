import json,sys,hashlib,zipfile,cProfile,pstats,io,time
from pathlib import Path
import numpy as np
repo=Path(__file__).resolve().parents[2];sys.path.insert(0,str(repo/'tools/BattleRl'))
from solo_public_schema import PublicSchema
from solo_text_assets import SoloTextAssets
from baseline_a_scenario_text import ScenarioText
import baseline_a_scenario_actions as old
import solo_a_command_compile_candidate as new
from solo_a_numeric_cache_candidate import catalog_argument_features
old.catalog_argument_features=new.catalog_argument_features=catalog_argument_features
root=Path(sys.argv[1]);archive=root/'cpu_profile_inputs.zip';assert hashlib.sha256(archive.read_bytes()).hexdigest()=='c6867517d1b058b62b4542d3faf291bd4bb0269837e51a00d0e7b42401805414'
data=root/'cpu_profile_inputs';data.mkdir(exist_ok=False)
with zipfile.ZipFile(archive) as z:
 for name in z.namelist():assert (data/name).resolve().is_relative_to(data.resolve())
 z.extractall(data)
schema=PublicSchema(data/'public_dto_schema.json');assets=SoloTextAssets(data/'text_assets',data);renderer=ScenarioText(schema,assets);states=json.loads((data/'actual_states.json').read_text())
def equal(a,b):
 if isinstance(a,np.ndarray):return isinstance(b,np.ndarray) and a.shape==b.shape and a.tobytes()==b.tobytes()
 if isinstance(a,dict):return set(a)==set(b) and all(equal(a[k],b[k]) for k in a)
 if isinstance(a,(list,tuple)):return len(a)==len(b) and all(equal(x,y) for x,y in zip(a,b))
 return a==b
for state in states:
 a=old.prepare_support(state['observation'],state['commands'],schema,assets);b=new.prepare_support(state['observation'],state['commands'],schema,assets)
 assert equal(a,b) and renderer.render(state['observation'],a)==renderer.render(state['observation'],b)
samples=[]
for name,module in [('old',old),('new',new),('new',new),('old',old)]:
 start=time.perf_counter()
 for state in states:
  support=module.prepare_support(state['observation'],state['commands'],schema,assets);renderer.render(state['observation'],support)
 samples.append(dict(version=name,seconds=time.perf_counter()-start))
profile=cProfile.Profile();profile.enable()
for state in states:
 support=new.prepare_support(state['observation'],state['commands'],schema,assets);renderer.render(state['observation'],support)
profile.disable();profile.dump_stats(str(root/'prepare_candidate_cpu.pstats'));stream=io.StringIO();pstats.Stats(profile,stream=stream).sort_stats('cumulative').print_stats(25);(root/'prepare_candidate_cpu_profile.txt').write_text(stream.getvalue())
result=dict(passed=True,states=len(states),commands=sum(len(x['commands']) for x in states),all_support_fields_and_documents_exact=True,samples=samples,scope='CPU input support/render gate; no policy training/GPU/full update claim')
(root/'prepare_candidate_full_gate.json').write_text(json.dumps(result,indent=2));print(json.dumps(result));print(stream.getvalue())
