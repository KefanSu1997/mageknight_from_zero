"""Batch split-text structured priors and static/dynamic fusion; opt-in only."""
import numpy as np
import torch
from solo_split_text_policy import SplitTextDistribution

_original_distributions = SplitTextDistribution.distributions


def distributions(self, rows, nodes):
    requests = [(int(self.contexts[r]), int(n)) for r,n in zip(rows,nodes)]
    vectors = self.runtime.vectors(requests)
    offsets = np.cumsum([0]+[len(x) for x in self.flat[:-1]])
    groups, widths, texts, latent_rows, rule_rows = [], [], [], [], []
    for row,node,vector in zip(rows,nodes,vectors):
        tree,_ = self.runtime.tree(int(self.contexts[row]))
        descendants = getattr(tree,'_a_cached_descendants',None)
        if descendants is None:
            descendants = [None]*len(tree.nodes)
            for index in reversed(range(len(tree.nodes))):
                data = tree.nodes[index]
                descendants[index] = ([data['leaf']] if data['leaf'] is not None else
                    [leaf for c in data['children'].values() for leaf in descendants[c]])
            if sorted(descendants[0]) != list(range(tree.leaves)):
                raise ValueError('Incomplete candidate tree')
            tree._a_cached_descendants = descendants
        children = list(tree.nodes[node]['children'].values())
        if not children or np.asarray(vector).shape != (len(children)+1,384):
            raise ValueError('Complete option vectors required')
        widths.append(len(children))
        texts.append(np.asarray(vector[1:]))
        rule_rows.append(np.asarray(self.runtime.rule_vectors(int(self.contexts[row])),dtype=np.float32))
        latent_rows.extend([row]*len(children))
        groups.extend([np.asarray(descendants[c],dtype=np.int64)+offsets[row] for c in children])
    max_leaves = max(map(len,groups))
    # Limit temporary gather allocation, retaining the original exact fallback.
    max_rules=max(len(x) for x in rule_rows)
    if len(groups)*max_leaves > 4_000_000 or len(groups)*max_rules*384 > 16_000_000:
        return _original_distributions(self,rows,nodes)
    indices = np.zeros((len(groups),max_leaves),dtype=np.int64)
    valid = np.zeros(indices.shape,dtype=bool)
    for i,group in enumerate(groups):
        indices[i,:len(group)] = group
        valid[i,:len(group)] = True
    device = self.latent.device
    flat = torch.cat(self.flat)
    if not torch.isfinite(flat).all():
        raise ValueError('Nonfinite complete-action scores')
    scores = flat[torch.as_tensor(indices,device=device)]
    child_scores = scores.masked_fill(~torch.as_tensor(valid,device=device),-torch.inf).logsumexp(-1)
    width = max(widths)
    positions = np.concatenate([np.arange(w)+i*width for i,w in enumerate(widths)])
    positions = torch.as_tensor(positions,device=device)
    prior = child_scores.new_full((len(widths)*width,),-torch.inf).scatter(0,positions,child_scores).reshape(len(widths),width)
    prior = prior - prior.logsumexp(-1,keepdim=True)
    text = torch.as_tensor(np.concatenate(texts),device=device,dtype=self.latent.dtype)
    latent = self.latent[torch.as_tensor(latent_rows,device=device)]
    rules=np.zeros((len(groups),max_rules,384),np.float32)
    present=np.zeros((len(groups),max_rules),bool)
    offset=0
    for count,static in zip(widths,rule_rows):
        rules[offset:offset+count,:len(static)]=static
        present[offset:offset+count,:len(static)]=True
        offset+=count
    residual=self.policy.split_fusion(latent,text,
        torch.as_tensor(rules,device=device,dtype=latent.dtype),
        torch.as_tensor(present,device=device))
    extra = residual.new_zeros(len(widths)*width).scatter(0,positions,residual).reshape(len(widths),width)
    return torch.distributions.Categorical(logits=prior+extra)
