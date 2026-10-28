"""Deterministic virtual-player deck clock, rulebook p.15.

Not a replacement for the native player's turn/round lifecycle. The scenario
adapter must call take_turn only at the dummy's scheduled turn and honor its
announcement through the native final-turn countdown. Hidden draw order and RNG
are checkpoint data only, never policy observations.
"""
from collections import Counter
import copy
import random

COLORS = frozenset(('red', 'blue', 'green', 'white'))


def validate_cards(cards):
    if not cards or any(set(c) != {'id', 'color'} or not c['id']
                        or c['color'] not in COLORS for c in cards):
        raise ValueError('Need physical card identifiers and printed basic colors')
    if len({c['id'] for c in cards}) != len(cards):
        raise ValueError('Physical dummy cards cannot be duplicated')


class DummyClock:
    def __init__(self, cards, crystals, seed):
        validate_cards(cards)
        if len(cards) != 16:
            raise ValueError('Dummy begins with the unused hero original 16 cards')
        if set(crystals) - COLORS or any(type(n) is not int or n < 0 for n in crystals.values()):
            raise ValueError('Invalid printed hero crystals')
        if sum(crystals.values()) != 3:
            raise ValueError('Dummy hero has three printed starting crystals')
        self.cards = copy.deepcopy(cards)
        self.crystals = Counter(crystals)
        self.rng = random.Random(seed)
        self.round = 1
        self.announced = False
        self.closed = False
        self.discard = []
        self.draw = copy.deepcopy(cards)
        self.rng.shuffle(self.draw)

    def take_turn(self):
        if self.closed or self.announced:
            raise ValueError('No extra dummy turn after round closed/announced')
        if not self.draw:
            self.announced = True
            return dict(announced=True, revealed=[], extra_count=0)
        first = self.draw[:3]
        del self.draw[:len(first)]
        extra_count = self.crystals[first[-1]['color']]
        extra = self.draw[:extra_count]
        del self.draw[:len(extra)]
        self.discard.extend(first + extra)
        # Exhaustion is announced next turn, never during this flip.
        return dict(announced=False, revealed=copy.deepcopy(first + extra),
                    extra_count=len(extra))

    def close_round(self):
        """Called only once native final-turn countdown has actually finished."""
        if self.closed:
            raise ValueError('Duplicate round end')
        self.closed = True

    def next_round(self, advanced_card, removed_spell_color):
        if not self.closed or self.round >= 6:
            raise ValueError('Round must finish; no seventh round in solo conquest')
        validate_cards([advanced_card])
        if advanced_card['id'] in {c['id'] for c in self.cards}:
            raise ValueError('Supply card already belongs to dummy')
        if removed_spell_color not in COLORS:
            raise ValueError('Use actual removed bottom supply spell color')
        # Supply ownership and removal are committed by the native adapter.
        self.cards.append(copy.deepcopy(advanced_card))
        self.crystals[removed_spell_color] += 1  # no three-crystal cap for dummy
        self.round += 1
        self.draw = copy.deepcopy(self.cards)
        self.rng.shuffle(self.draw)
        self.discard = []
        self.closed = self.announced = False

    def observation(self):
        return dict(round=self.round, remaining_cards=len(self.draw),
                    crystals=dict(self.crystals), announced=self.announced,
                    revealed_cards=copy.deepcopy(self.discard))

    def checkpoint(self):
        return copy.deepcopy(dict(version=1, cards=self.cards, crystals=dict(self.crystals),
                                  draw=self.draw, discard=self.discard, round=self.round,
                                  announced=self.announced, closed=self.closed,
                                  rng=self.rng.getstate()))

    @classmethod
    def restore(cls, saved):
        s = copy.deepcopy(saved)
        if s['version'] != 1 or not 1 <= s['round'] <= 6:
            raise ValueError('Unsupported clock checkpoint')
        validate_cards(s['cards'])
        if len(s['cards']) != 15+s['round']:
            raise ValueError('Dummy growth does not match round')
        expected = {c['id']: c for c in s['cards']}
        actual = s['draw'] + s['discard']
        if len(actual) != len(expected) or {c['id']: c for c in actual} != expected:
            raise ValueError('Checkpoint loses/duplicates/mutates physical cards')
        obj = cls.__new__(cls)
        for key in ('cards', 'draw', 'discard', 'round', 'announced', 'closed'):
            setattr(obj, key, s[key])
        obj.crystals = Counter(s['crystals'])
        obj.rng = random.Random()
        def tuples(value):
            return tuple(map(tuples, value)) if isinstance(value, (list, tuple)) else value
        obj.rng.setstate(tuples(s['rng']))
        return obj
