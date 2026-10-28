"""CPU-only mathematical attention equivalence; not training or speed evidence."""
import ast,hashlib,json,sys
from pathlib import Path
from types import SimpleNamespace
import torch
import torch.nn.functional as F
repo=Path(__file__).resolve().parents[2]
old=repo/'tools/BattleRl/hierarchical_batch_encoder.py';candidate=repo/'tools/BattleRl/solo_a_attention_blocks_candidate.py'
sys.path.insert(0,str(candidate.parent))
from solo_a_attention_blocks_candidate import attention as new
node=next(n for n in ast.parse(old.read_text(encoding='utf-8-sig')).body if isinstance(n,ast.FunctionDef) and n.name=='attention')
ns={'torch':torch,'F':F};exec(compile(ast.Module(body=[node],type_ignores=[]),str(old),'exec'),ns);reference=ns['attention']
torch.set_num_threads(1);torch.manual_seed(419)
results=[]
with torch.inference_mode():
 for dtype in [torch.float64,torch.float32]:
  for length in [17,513,1025]:
   for window in [None,8,128]:
    lengths=torch.tensor([length,max(1,length//3)])
    module=SimpleNamespace(config=SimpleNamespace(_hierarchical_lengths=lengths,sliding_window=window),sliding_window=window)
    q,k,v=[torch.randn(2,2,length,8,dtype=dtype) for _ in range(3)]
    a=reference(module,q,k,v,None,scaling=.27)[0];b=new(module,q,k,v,None,scaling=.27)[0]
    tolerance=1e-10 if dtype==torch.float64 else 1e-5
    assert torch.allclose(a,b,atol=tolerance,rtol=tolerance),(dtype,length,window,float((a-b).abs().max()))
    results.append(dict(dtype=str(dtype),length=length,window=window,max_abs=float((a-b).abs().max())))
result=dict(passed=True,scope='CPU-only random valid-key/local-window masks, no model training or GPU speed claim',sources={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in [old,candidate]},cases=results)
Path(sys.argv[1]).write_text(json.dumps(result,indent=2));print(json.dumps(dict(passed=True,cases=len(results),max_abs=max(x['max_abs'] for x in results))))

