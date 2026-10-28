"""CPU-only real-command numeric compilation gate; not full policy acceptance."""
import json,sys,time,hashlib
from pathlib import Path
import numpy as np
repo=Path(__file__).resolve().parents[2];sys.path.insert(0,str(repo/'tools/BattleRl'))
import baseline_a_scenario_actions as old
from solo_a_numeric_cache_candidate import catalog_argument_features
from solo_a_command_compile_v2_candidate import _compiled,_compile_active
old.catalog_argument_features=catalog_argument_features
source=(repo/'tools/BattleRl/baseline_a_scenario_actions.py').read_text()
start=source.index('        components=parameter_components(command)');end=source.index('        refs=candidate_entities',start)
body='\n'.join(line[4:] for line in source[start:end].splitlines())
ns=dict(old.__dict__);exec('def reference(command):\n'+body+'\n    return components,numeric\n',ns);reference=ns['reference']
corpus=Path(sys.argv[1]);commands=[json.loads(c) for c in json.loads(corpus.read_text())['commands']]
for command in commands:
 a,x=reference(command);b,y=_compiled(command)
 assert a==b and np.array_equal(x,y)
 b[0][0]['Input']='MUTATED'
 again,_=_compiled(command);assert again==a,'Caller must not poison cache'
samples=[]
for label,fn in [('old',reference),('new',_compiled),('new',_compiled),('old',reference)]:
 start=time.perf_counter()
 for command in commands:fn(command)
 samples.append(dict(implementation=label,seconds=time.perf_counter()-start))
result=dict(passed=True,commands=len(commands),samples=samples,cache=_compile_active.cache_info()._asdict(),corpus_sha256=hashlib.sha256(corpus.read_bytes()).hexdigest(),scope='Parameter components and 96 numeric values exact, caller mutation isolation; dynamic bindings/full policy not yet tested')
Path(sys.argv[2]).write_text(json.dumps(result,indent=2));print(json.dumps(result))

