"""Breadth-first batched frozen encoding; task-local evaluation RNG retained."""
import json
from pathlib import Path
import numpy as np
import torch
import hierarchical_option_policy as h
from hierarchical_batch_encoder import BatchedReader

reader=None
def prefetch(path,requests):
    global reader
    pending=[];docs=[];folder=Path(path)/'hierarchical';folder.mkdir(exist_ok=True)
    for context,node in dict.fromkeys(requests):
        if not context:continue
        key=(path,context,node)
        if key in h._vectors:continue
        file=folder/f'{context:08d}_{node:06d}.npy'
        if file.exists():h._vectors[key]=np.load(file,allow_pickle=False);continue
        t,base=h.tree(path,context);doc=t.document(base,node)
        if not doc['options']:doc['options']=['唯一完整操作，直接执行']
        pending.append((key,file));docs.append(doc)
    if docs:
        if reader is None:
            reader=BatchedReader()
            h._reader=reader  # On-demand diagnostics share the one frozen encoder.
        for (key,file),v in zip(pending,reader.encode_many(docs)):
            h._vectors[key]=v;np.save(file,v,allow_pickle=False)
    while len(h._vectors)>512:h._vectors.popitem(last=False)

def forward(self,obs):
    ids=obs['context_id'].detach().cpu().numpy().reshape(-1).astype('int64')
    requests=[(int(i),h.tree(self.context_path,int(i))[0].branch(0) if i else 0) for i in ids]
    prefetch(self.context_path,requests)
    values=[h.node_vectors(self.context_path,i,n)[0] for i,n in requests]
    return self.projection(torch.as_tensor(np.stack(values),device=obs['context_id'].device))

def sample_rows(self,generators=None,deterministic=False):
    path=self.policy.context_path;selected=[0]*len(self.contexts);active={};records={}
    for row,context in enumerate(self.contexts):
        if context:
            t,_=h.tree(path,int(context));active[row]=(t,t.branch(0));records[row]=dict(context=int(context),route=[])
    while active:
        requests=[(int(self.contexts[row]),node) for row,(t,node) in active.items() if t.nodes[node]['leaf'] is None]
        prefetch(path,requests)
        for row,(t,node) in list(active.items()):
            if t.nodes[node]['leaf'] is not None:
                selected[row]=t.nodes[node]['leaf'];records[row]['leaf']=selected[row];del active[row];continue
            dist=self.categorical(row,node)
            choice=int(dist.logits.argmax()) if deterministic else int(torch.multinomial(dist.probs,1,generator=None if generators is None else generators[row]))
            children=list(t.nodes[node]['children'].items());edge,next_node=children[choice];assert next_node>node
            records[row]['route'].append(dict(node=node,choice=choice,branch_count=len(children),locked=edge))
            active[row]=(t,t.branch(next_node))
    if records:
        with (Path(path)/'selection_paths.jsonl').open('a',encoding='utf-8') as f:
            for row in sorted(records):f.write(json.dumps(records[row],ensure_ascii=False)+'\n')
    return torch.tensor(selected,device=self.latent.device)

def install():
    h.HierarchicalFeatures.forward=forward
    h.HierarchicalDistribution.sample_rows=sample_rows
