"""Opt-in transport/cache optimization; frozen DLL and wire records unchanged."""
import copy
import json
import inspect
import textwrap
from collections import OrderedDict


def install():
    from train_ppo import Rules
    from atomic_action_registry import Registry
    from atomic_battle_env import BattleEnv
    from atomic_entity_text import BattleEnv as TextEnv
    from sb3_battle_env import BattleEnv as NumericEnv
    if getattr(Rules, '_fast_installed', False):
        return
    original_call = Rules.call
    original_add, original_get = Registry.add, Registry.get
    original_step = BattleEnv.step
    original_init = BattleEnv.__init__
    shared_indices = {}
    original_observation = TextEnv._observation
    original_numeric_init = NumericEnv.__init__
    # Constructor only consumes len(initial['features']); the immutable protocol
    # header already publishes these coordinates. Actual episode reset is intact.
    source = textwrap.dedent(inspect.getsource(original_numeric_init))
    needle = "initial = self.rules.reset_task(rows[0]['task']['definition'])"
    assert source.count(needle) == 1
    source = source.replace(needle, "initial = {'features': self.rules.info['featureNames']}")
    namespace = {}
    exec(compile(source, __file__ + ':numeric_init', 'exec'), original_numeric_init.__globals__, namespace)
    # The original zero-argument super() requires its class closure. Replace with
    # the explicit gym.Env implementation (which is a no-op initialization).
    # This constructor currently contains no super call; assert that contract.
    assert 'super(' not in source
    NumericEnv.__init__ = namespace['__init__']

    def observation(self):
        cached = getattr(self, '_fast_observation', None)
        if cached is not None and cached[0] is self.current and cached[1] is self.previous and cached[2] == self.support_id:
            return cached[3]
        result = original_observation(self)
        self._fast_observation = (self.current, self.previous, self.support_id, result)
        return result

    def initialize(self, *args, **kwargs):
        original_init(self, *args, **kwargs)
        key = id(self.rules.actions)
        if key not in shared_indices:
            shared_indices[key] = self.key_to_id
        self.key_to_id = shared_indices[key]

    def call(self, **request):
        op = request.get('op')
        if op == 'atomicChoices' and getattr(self, '_prefetched_choices', None) is not None:
            result = self._prefetched_choices
            self._prefetched_choices = None
            if 'error' in result:
                raise RuntimeError(result['error'])
            return result
        self._prefetched_choices = None
        if op != 'atomicStep':
            return original_call(self, **request)
        # Send both requests before waiting, but drain both replies even on error.
        # AtomicChoices is observational (transactional undo); terminal returns [].
        self.p.stdin.write(json.dumps(request) + '\n' + '{"op":"atomicChoices"}\n')
        self.p.stdin.flush()
        first, second = self.p.stdout.readline(), self.p.stdout.readline()
        if not first or not second:
            raise RuntimeError('Rules process exited during atomic pipeline')
        result, choices = json.loads(first), json.loads(second)
        if 'error' in result:
            raise RuntimeError(result['error'])
        self._prefetched_choices = choices
        return result

    def remember(self, index, value):
        if not hasattr(self, '_hot_supports'):
            self._hot_supports = OrderedDict()
        self._hot_supports[index] = value
        self._hot_supports.move_to_end(index)
        while len(self._hot_supports) > 256:
            self._hot_supports.popitem(last=False)

    def add(self, sequences):
        with self.lock:
            index = original_add(self, sequences)  # durable format/flush unchanged
            remember(self, index, copy.deepcopy(sequences))
            return index

    def get(self, index):
        with self.lock:
            if hasattr(self, '_hot_supports') and index in self._hot_supports:
                self._hot_supports.move_to_end(index)
                return self._hot_supports[index]
            result = original_get(self, index)
            remember(self, index, result)
            return result

    def step(self, action):
        result = original_step(self, action)
        # Protocol JSON duplicates adjacent immutable state snapshots. Share only
        # equality-verified objects; saved JSON still contains every original field.
        if self.trace:
            row = self.trace[-1]
            previous = row['before']
            for micro in row.get('micro', []):
                if micro.get('before') == previous:
                    micro['before'] = previous
                previous = micro.get('after')
            if previous is not None and row['after'] == previous:
                row['after'] = previous
                if self.current['state'] == previous:
                    self.current['state'] = previous
        return result

    Rules.call, Rules._fast_installed = call, True
    Registry.add, Registry.get = add, get
    BattleEnv.step = step
    BattleEnv.__init__ = initialize
    TextEnv._observation = observation
    def uninstall():
        Rules.call = original_call
        Rules._fast_installed = False
        Registry.add, Registry.get = original_add, original_get
        BattleEnv.step, BattleEnv.__init__ = original_step, original_init
        TextEnv._observation = original_observation
        NumericEnv.__init__ = original_numeric_init
    return uninstall
