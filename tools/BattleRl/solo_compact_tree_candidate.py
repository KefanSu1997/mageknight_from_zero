"""Isolated exact tree layout with shared immutable prefix chains."""
from hierarchical_option_policy import ChoiceTree, operation_path


class Prefix:
    __slots__ = ('parent', 'edge', 'length')

    def __init__(self, parent=None, edge=None):
        self.parent, self.edge = parent, edge
        self.length = 0 if parent is None else len(parent) + 1

    def __len__(self):
        return self.length

    def __iter__(self):
        edges = []
        current = self
        while current.parent is not None:
            edges.append(current.edge)
            current = current.parent
        return reversed(edges)

    def __eq__(self, other):
        if self is other:
            return True
        return list(self) == list(other)


class CompactChoiceTree(ChoiceTree):
    def __init__(self, operations):
        self.nodes = [dict(children={}, leaf=None, prefix=Prefix())]
        self.paths = []
        for leaf, sequence in enumerate(operations):
            node, route = 0, []
            for edge in operation_path(sequence, leaf):
                children = self.nodes[node]['children']
                if edge not in children:
                    children[edge] = len(self.nodes)
                    self.nodes.append(dict(children={}, leaf=None,
                                           prefix=Prefix(self.nodes[node]['prefix'], edge)))
                route.append((node, edge))
                node = children[edge]
            assert self.nodes[node]['leaf'] is None
            self.nodes[node]['leaf'] = leaf
            self.paths.append(route)
        self.leaves = len(operations)
        assert sorted(n['leaf'] for n in self.nodes if n['leaf'] is not None) == list(range(self.leaves))
