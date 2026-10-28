"""Single-player conquest score and world-family split contract.

Rulebook printed pp. 15/19. This is not a runnable scenario environment.
Engine-owned snapshots must supply physical inventory counts, not reward events.
"""
from dataclasses import dataclass
import hashlib
import json


@dataclass(frozen=True)
class ScoreState:
    fame: int
    spells: int
    advanced_actions: int
    artifacts: int
    crystals: int
    units: tuple  # (printed level, is wounded), each physical unit once
    fortified_shields: int  # keeps, mage towers, monasteries
    adventure_shields: int
    deck_wounds: int  # all owned deck zones, excludes wounds on units
    conquered_cities: int

    def components(self):
        integers = (self.fame, self.spells, self.advanced_actions, self.artifacts,
                    self.crystals, self.fortified_shields, self.adventure_shields,
                    self.deck_wounds, self.conquered_cities)
        if any(type(x) is not int or x < 0 for x in integers):
            raise ValueError('Score counters must be nonnegative native integers')
        if self.conquered_cities > 2:
            raise ValueError('Solo conquest has exactly two cities')
        if any(type(level) is not int or level < 1 or type(wounded) is not bool
               for level, wounded in self.units):
            raise ValueError('Invalid unit score entry')
        return dict(fame=self.fame, knowledge=2*self.spells+self.advanced_actions,
                    loot=2*self.artifacts+self.crystals//2,
                    units=sum(level//2 if wounded else level for level, wounded in self.units),
                    fortified=2*self.fortified_shields, adventure=2*self.adventure_shields,
                    wounds=-2*self.deck_wounds, cities=10*self.conquered_cities,
                    all_cities=15 if self.conquered_cities == 2 else 0)


def score(state, *, terminal=False, round_index=None, dummy_cards=None,
          player_announced=None):
    result = state.components()
    if terminal:
        if type(round_index) is not int or not 1 <= round_index <= 6:
            raise ValueError('Need actual ending round, not an action/time limit')
        if type(dummy_cards) is not int or dummy_cards < 0 or type(player_announced) is not bool:
            raise ValueError('Need actual dummy clock and announcement state')
        if round_index < 6 and state.conquered_cities != 2:
            raise ValueError('Early failure/truncation is not a completed conquest scenario')
        result.update(early_rounds=30*(6-round_index), remaining_dummy_cards=dummy_cards,
                      not_announced=0 if player_announced else 5)
    return result


class RewardLedger:
    """Telescoping score increments, gamma=1, with terminal bonuses once only.

    No extra heal/kill/win rewards. Inventory losses can produce negative reward.
    Returning to an identical state cannot manufacture a positive return.
    """
    def __init__(self, initial, scale=0.01):
        if scale <= 0:
            raise ValueError('Reward scale must be positive')
        self.scale = scale
        self.initial = self.previous = sum(score(initial).values())
        self.closed = False

    def advance(self, state, **ending):
        if self.closed:
            raise ValueError('Episode already settled')
        components = score(state, **ending)
        current = sum(components.values())
        reward = self.scale * (current - self.previous)
        self.previous = current
        self.closed = bool(ending.get('terminal', False))
        return reward, components


def family_digest(world):
    """Caller supplies the complete canonical world, excluding hero and RNG seed.

    A seed-only split is forbidden. Family includes composition AND supply/clock
    setup; later episode shuffles are nested inside that family's assigned split.
    """
    required = {'map_tiles', 'city_colors', 'city_levels', 'enemy_supply',
                'offer_supply', 'dummy_setup', 'ruins', 'initial_mana', 'rules_sha256', 'catalog_sha256'}
    if set(world) != required or world['city_levels'] != [5, 8]:
        raise ValueError('Incomplete or nonstandard canonical solo world')
    if any(not world[key] for key in required):
        raise ValueError('World fields cannot be empty placeholders')
    return hashlib.sha256(json.dumps(world, sort_keys=True, ensure_ascii=False,
                                     separators=(',', ':')).encode()).hexdigest()


def split_worlds(worlds, salt='solo-conquest-v1'):
    """Assign 1000/100/100 distinct canonical families; heroes expand afterwards."""
    keyed = [(family_digest(w), w) for w in worlds]
    if len(keyed) != 1200 or len({k for k, _ in keyed}) != 1200:
        raise ValueError('Require 1200 distinct fully specified world families')
    keyed.sort(key=lambda row: hashlib.sha256((salt+row[0]).encode()).hexdigest())
    return {name: [dict(family_sha256=k, world=w) for k, w in keyed[a:b]]
            for name, a, b in [('train', 0, 1000), ('validation', 1000, 1100),
                               ('test', 1100, 1200)]}
