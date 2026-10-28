"""Candidate core: factor structured leaf scores without changing their prior.

No environment or training entry point. Token-marker residual logits may augment
each conditional branch; zero residual exactly recovers the structured scorer.
"""
import torch

class StructuredHierarchy:
    def __init__(self, tree, leaf_logits, residual=None):
        if leaf_logits.ndim!=1 or len(leaf_logits)!=tree.leaves:
            raise ValueError('Every original complete candidate needs one score')
        if not torch.isfinite(leaf_logits).all():raise ValueError('Nonfinite leaf scores')
        self.tree=tree;self.logits=leaf_logits;self.residual=residual
        self.descendants=[None]*len(tree.nodes);self.cache={}
        for node in reversed(range(len(tree.nodes))):
            data=tree.nodes[node]
            self.descendants[node]=([data['leaf']] if data['leaf'] is not None else
                [leaf for child in data['children'].values() for leaf in self.descendants[child]])
        if sorted(self.descendants[0])!=list(range(tree.leaves)):
            raise ValueError('Tree must preserve every complete candidate exactly once')

    def categorical(self,node):
        if node not in self.cache:
            children=list(self.tree.nodes[node]['children'].values())
            if not children:raise ValueError('Leaf has no next decision')
            scores=torch.stack([torch.logsumexp(self.logits[self.descendants[c]],0) for c in children])
            if self.residual is not None:
                extra=self.residual(node)
                if extra.shape!=scores.shape or not torch.isfinite(extra).all():
                    raise ValueError('Residual must cover exactly all legal children')
                scores=scores+extra
            self.cache[node]=torch.distributions.Categorical(logits=scores)
        return self.cache[node]

    def log_prob(self,leaf):
        if not 0<=leaf<self.tree.leaves:raise ValueError('Candidate outside support')
        terms=[]
        for node,edge in self.tree.paths[leaf]:
            children=self.tree.nodes[node]['children']
            if len(children)>1:
                index=list(children).index(edge)
                terms.append(self.categorical(node).log_prob(self.logits.new_tensor(index,dtype=torch.long)))
        return torch.stack(terms).sum() if terms else self.logits.sum()*0

    def sample(self,generator=None):
        node=self.tree.branch(0);path=[]
        while self.tree.nodes[node]['leaf'] is None:
            cat=self.categorical(node)
            choice=int(torch.multinomial(cat.probs,1,generator=generator))
            child=list(self.tree.nodes[node]['children'].values())[choice]
            if child<=node:raise ValueError('Nonforward choice')
            path.append((node,choice));node=self.tree.branch(child)
        return self.tree.nodes[node]['leaf'],path
