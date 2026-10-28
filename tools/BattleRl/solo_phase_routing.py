"""Public, deterministic expert routing. Does not modify legal action supports."""
from collections import Counter


def route(observation):
    if observation['Terminal']:
        return 'terminal'
    combat = observation['Combat']
    active = combat['Active']
    if type(active) is not bool:
        raise ValueError('Combat.Active must be the native public boolean')
    if active == (combat['Phase'] == 'Outside'):
        raise ValueError('Native combat activity/phase disagreement')
    return 'combat' if active else 'world'


def split_public_text_state(observation):
    """Explicit semantic views; structured obligations remain separately available.

    No future draws, seeds, hidden defenders, or private snapshots are read.
    Selection remains over the unchanged native support, including obligations.
    """
    expert = route(observation)
    if expert == 'terminal':
        raise ValueError('Terminal has no policy input')
    inv, world = observation['Inventory'], observation['World']
    common = {k: inv[k] for k in ('Hero', 'Fame', 'Level', 'Reputation', 'Armor',
                                  'CommandSlots', 'DrawCount', 'Mana', 'Hand',
                                  'Units', 'Skills')}
    horizon = {k: world[k] for k in ('Round', 'DayPart', 'PlayerAnnounced',
                                    'Dummy', 'ActiveTurn', 'EndingTurn')}
    if expert == 'combat':
        here = [c for c in world['Cells']
                if c['Q'] == world['PlayerQ'] and c['R'] == world['PlayerR']]
        state = dict(resources=common, horizon=horizon,
                     combat=observation['Combat'], current_location=here,
                     played=inv['Played'],
                     discard_counts=dict(Counter(c['Id'] for c in inv['Discard'])),
                     round_aside_counts=dict(Counter(c['Id'] for c in inv['RoundAside'])))
    else:
        state = dict(resources=common, horizon=horizon,
                     position=[world['PlayerQ'], world['PlayerR']],
                     movement=inv['Movement'], influence=inv['Influence'],
                     healing=inv['Healing'],
                     known_sites=[dict(q=c['Q'], r=c['R'], site=c['Site'])
                                  for c in world['Cells'] if c['Site'] is not None],
                     offers=observation['Offers'],
                     discard_counts=dict(Counter(c['Id'] for c in inv['Discard'])),
                     round_aside_counts=dict(Counter(c['Id'] for c in inv['RoundAside'])))
    return expert, state
