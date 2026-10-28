"""Opt-in byte-bounded exact choice-tree cache for isolated PPO experiments."""
from hierarchical_option_policy import ChoiceTree
from solo_a_decoded_cache_v2_candidate import retained_bytes


class BoundedTreeCacheMixin:
    def configure_tree_cache(self, limit_bytes):
        if type(limit_bytes) is not int or limit_bytes <= 0:
            raise ValueError('Positive tree cache budget required')
        self.trees.clear()
        self.tree_cache_sizes = {}
        self.tree_cache_bytes = 0
        self.tree_cache_limit = limit_bytes
        self.tree_cache_hits = self.tree_cache_misses = 0

    def tree(self, context):
        context = int(context)
        if not hasattr(self, 'tree_cache_limit'):
            return super().tree(context)
        if context in self.trees:
            self.tree_cache_hits += 1
            self.trees.move_to_end(context)
            return self.trees[context]
        self.tree_cache_misses += 1
        value = self._read(context)
        tree = getattr(self, 'choice_tree_factory', ChoiceTree)(value['support']['operations'])
        result = (tree, value['document'])
        self.remember_prepared_tree(context, *result)
        return result

    def remember_prepared_tree(self, context, tree, document):
        """Adopt the collection worker's existing tree without rebuilding it."""
        context = int(context)
        if not hasattr(self, 'tree_cache_limit'):
            self.trees[context] = (tree, document)
            while len(self.trees) > 1024:
                self.trees.popitem(last=False)
            return
        if context in self.trees:
            del self.trees[context]
            self.tree_cache_bytes -= self.tree_cache_sizes.pop(context)
        # Count containers, strings, shared edges and all paths once per tree.
        # Counting document sharing separately is conservative across contexts.
        size = retained_bytes((vars(tree), document))
        if size <= self.tree_cache_limit:
            self.trees[context] = (tree, document)
            self.tree_cache_sizes[context] = size
            self.tree_cache_bytes += size
            while self.tree_cache_bytes > self.tree_cache_limit:
                old, _ = self.trees.popitem(last=False)
                self.tree_cache_bytes -= self.tree_cache_sizes.pop(old)
