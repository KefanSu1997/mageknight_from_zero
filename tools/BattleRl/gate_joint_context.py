import sys,json,time,hashlib
from pathlib import Path
import numpy as np
import torch
from joint_option_policy import ContextReader
from joint_option_text import encode_document
from joint_chunked_attention import enable

torch.set_num_threads(1)
root=Path('H:/MageKnight_RL_Runs/20261013_joint_option_prepare')
reader=ContextReader()
def fingerprint():
 h=hashlib.sha256()
 for name,v in reader.model.state_dict().items():
  h.update(name.encode())
  for part in v.detach().reshape(-1).split(262144):h.update(part.float().cpu().numpy().tobytes())
 return h.hexdigest()
before=fingerprint();small=json.loads((root/'context_0001.json').read_text(encoding='utf-8'))
ids,pos=encode_document(reader.tok,small,32768);x=torch.tensor([ids],device='cuda')
with torch.no_grad():
 reader.model.config._attn_implementation='sdpa'
 old=reader.model(input_ids=x,attention_mask=torch.ones_like(x)).last_hidden_state
 enable(reader.model)
 new=reader.model(input_ids=x,attention_mask={'full_attention':None,'sliding_attention':None}).last_hidden_state
 difference=float((new-old).abs().max())
 cosine=float(torch.nn.functional.cosine_similarity(new.float().flatten(),old.float().flatten(),dim=0))
 assert cosine>.999 and difference<.25,(difference,cosine)
del old,new,x
results=[]
for name in ['context_0001.json','large_context.json']:
 doc=json.loads((root/name).read_text(encoding='utf-8'));ids,pos=encode_document(reader.tok,doc,32768)
 torch.cuda.reset_peak_memory_stats();started=time.perf_counter();v=reader.encode(doc);torch.cuda.synchronize()
 assert v.shape==(len(doc['options'])+1,384) and np.isfinite(v).all()
 results.append(dict(file=name,tokens=len(ids),options=len(pos),seconds=time.perf_counter()-started,peak_cuda_allocated=torch.cuda.max_memory_allocated()))
 print(json.dumps(results[-1]),flush=True)
after=fingerprint();assert before==after
assert all(not p.requires_grad and p.grad is None for p in reader.model.parameters())
result=dict(passed=True,small_attention_max_abs_difference=difference,small_attention_cosine=cosine,encoder_precision='bfloat16 frozen',encoder_sha_before=before,encoder_sha_after=after,zero_encoder_gradients=True,measurements=results,
 large_context_scope='real complete support of 1173 options with a representative public state; resource stress test, not a new game trajectory',
 positional_extrapolation='Inputs above 8192 use original default RoPE frequencies; capability not established by finite outputs')
(root/'context_gate.json').write_text(json.dumps(result,indent=2));print('context gate passed',flush=True)
