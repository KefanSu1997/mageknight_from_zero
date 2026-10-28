"""Expected shared battle fields for the existing Unity automation interface.

This is a comparison adapter, not a simulated UI and not Unity acceptance.
UI-only selections/artwork and formal Outside/journey fields remain separate.
"""


def battle_projection(state):
    if state['phase']=='Outside' or not state.get('battleSessionPresent',state['inBattle']):
        raise ValueError('Outside/journey uses a different Unity capture path; do not guess battle fields')
    result={key:str(state[source]) for key,source in
            (('phase','phase'),('fame','fame'),('wounds','wounds'),('deck.count','deck'),
             ('influence','influence'),('block','block'),('kills','kills'),('hero.id','hero'),('dayPart','dayPart'))}
    result.update(won=str(state['won']),hand=str(len(state['hand'])))
    result['hand.ids']=','.join(state['hand'])
    result['source.units']=','.join(u['id'] for u in state['units'])
    result['units.count']=str(len(state['units']))
    result['units.ready']=','.join(str(u['ready']) for u in state['units'])
    result['units.wounds']=','.join(str(u['wounds']) for u in state['units'])
    result['source.usedThisTurn']=str(state['sourceUsedThisTurn'])
    for color in ('Red','Blue','White','Green'):
        result[color.lower()]=str(state['crystals'].get(color,0))
    for color in ('Red','Blue','White','Green','Gold','Black'):
        result['tokens.'+color.lower()]=str(state['tokens'].get(color,0))
    return result


def compare_unity_capture(state,capture):
    expected=battle_projection(state)
    return [dict(key=key,expected=value,observed=capture.get(key),missing=key not in capture)
            for key,value in expected.items() if capture.get(key)!=value]
