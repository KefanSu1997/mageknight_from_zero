"""Experimental public world-context potential; native goal/reward unchanged.

Only existing printed village locations, visible position, owned skill IDs and
formal interaction/bound-unit state are used. Terminal potential is zero, so
discounted shaping telescopes to a task-initial constant, never a fake victory.
"""
import math


class WorldConsumerPotentialReward:
    def __init__(self, scale=0., gamma=.995):
        if not math.isfinite(scale) or scale < 0 or not 0 < gamma <= 1:
            raise ValueError('Nonnegative finite scale and valid discount required')
        self.scale, self.gamma = float(scale), float(gamma)

    @staticmethod
    def potential(state):
        world = state.get('world', {})
        if not world.get('enabled') or not {'hero_skill_pool_005:05', 'hero_skill_pool_002:07'}.intersection(state.get('skills', [])):
            return 0.
        villages = [cell for cell in world['cells'] if cell.get('site') == 'Village']
        if not villages:
            return 0.
        q, r = world['q'], world['r']
        distance = min((abs(cell['q'] - q) + abs(cell['r'] - r) + abs(cell['q'] - q + cell['r'] - r)) / 2 for cell in villages)
        location = max(0., 1. - distance / 3.)
        interaction = .5 if world.get('inNegotiation') else 0.
        bound = .5 if world.get('loyaltyBound') is not None else 0.
        preparation = min(6., max(0., state.get('movement', 0.))) / 12. if not state.get('inBattle', False) else 0.
        return location + interaction + bound + preparation

    def step(self, before, after, done):
        if self.scale == 0.:
            return 0.
        after_potential = 0. if done else self.potential(after)
        return self.scale * (self.gamma * after_potential - self.potential(before))
