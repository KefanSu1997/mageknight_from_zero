"""Public solo-world additions for A, without a learned DTO/tree encoder.

Clock scalars append to A's numeric observation. Spatial records are explicit
entity-attention inputs to be bound to the expanded entity vocabulary. This
module alone is not the complete policy adapter.
"""
import hashlib
import json
import math
import numpy as np

TACTICS = tuple(f'{part}_{i}' for part in ('day', 'night') for i in range(1, 7))
WORLD_FIELDS = frozenset(('Round','DayPart','PlayerQ','PlayerR','Cells','Tiles',
    'Dummy','PlayerTactic','DummyTactic','RemovedTactics','ActiveTurn','EndingTurn',
    'PendingTacticInput','PlayerAnnounced','ScenarioEnded','ExplorationPositions','LastScouting'))


def _keys(value, names):
    if not isinstance(value, dict) or set(value) != set(names):
        raise ValueError('Public world fields changed: '+str(set(value) if isinstance(value,dict) else type(value)))


def encode_world(world):
    _keys(world, WORLD_FIELDS)
    names, values, records = [], [], []
    def number(name, value, scale=1):
        if type(value) not in (int,float,bool) or not math.isfinite(value):
            raise ValueError('Nonfinite public field '+name)
        names.append('world.'+name); values.append(float(value)/scale)
    def categorical(name, value, vocabulary):
        if value not in vocabulary: raise ValueError('Unknown public category '+name+': '+str(value))
        for category in vocabulary: number(name+'='+str(category), value==category)
    number('Round',world['Round'],6)
    categorical('DayPart',world['DayPart'],('Day','Night'))
    number('PlayerQ',world['PlayerQ'],32); number('PlayerR',world['PlayerR'],32)
    for name in ('ActiveTurn','EndingTurn','PendingTacticInput','PlayerAnnounced','ScenarioEnded'):
        if type(world[name]) is not bool: raise ValueError('Expected public Boolean '+name)
        number(name,world[name])
    for name in ('PlayerTactic','DummyTactic'):
        categorical(name,world[name],(None,)+TACTICS)
    removed=world['RemovedTactics']
    if len(removed)!=len(set(removed)) or any(x not in TACTICS for x in removed):
        raise ValueError('Invalid removed tactic set')
    for tactic in TACTICS: number('RemovedTactics.'+tactic,tactic in removed)
    tiles=world['Tiles']; _keys(tiles,('MainRemaining','VisibleBack','IsReserve'))
    number('Tiles.MainRemaining',tiles['MainRemaining'],11)
    categorical('Tiles.VisibleBack',tiles['VisibleBack'],(None,'Countryside','Core'))
    number('Tiles.IsReserve',tiles['IsReserve'])
    dummy=world['Dummy']; _keys(dummy,('Round','Remaining','Crystals','Discard','Announced'))
    number('Dummy.Round',dummy['Round'],6); number('Dummy.Remaining',dummy['Remaining'],32)
    number('Dummy.Announced',dummy['Announced'])
    if len(dummy['Crystals'])!=4: raise ValueError('Dummy crystal color schema changed')
    for i,count in enumerate(dummy['Crystals']): number('Dummy.Crystals.'+str(i),count,10)
    # Preserve actual public discard order/colors. Physical audit IDs are not
    # meaningful action inputs; no private draw/order/seed is accepted here.
    for i,card in enumerate(dummy['Discard']):
        _keys(card,('PhysicalId','DefinitionId','Color','SecondColor'))
        records.append(dict(role='dummy_discard',index=i,identity=card['DefinitionId'],
                            color=card['Color'],second_color=card['SecondColor']))
    seen=set()
    for cell in sorted(world['Cells'],key=lambda c:(c['Q'],c['R'])):
        _keys(cell,('Q','R','Terrain','PrintedSite','Site'))
        position=(cell['Q'],cell['R'])
        if position in seen: raise ValueError('Duplicate public map coordinate')
        seen.add(position)
        records.append(dict(role='map_cell',q=position[0],r=position[1],
                            terrain=cell['Terrain'],printed_site=cell['PrintedSite']))
        site=cell['Site']
        if site is None: continue
        _keys(site,('Type','Conquered','OwnerId','CityColor','CityLevel','MineColors',
                    'TokenRevealed','RuinToken','Guards','RuinDetails','MonasteryBurned'))
        records.append(dict(role='map_site',q=position[0],r=position[1],
                            **{k:site[k] for k in site if k not in ('Guards','RuinDetails')}))
        for guard in site['Guards']:
            _keys(guard,('Slot','BackColor','Revealed','DefinitionId'))
            if not guard['Revealed'] and guard['DefinitionId'] is not None:
                raise ValueError('Hidden guard identity leaked into public world')
            records.append(dict(role='map_guard',q=position[0],r=position[1],**guard))
        if site['RuinDetails'] is not None:
            if not site['TokenRevealed']: raise ValueError('Hidden ruin details exposed')
            _keys(site['RuinDetails'],('Id','ManaCost','Fame','Enemies','Rewards'))
            records.append(dict(role='map_ruin',q=position[0],r=position[1],**site['RuinDetails']))
    for p in sorted(world['ExplorationPositions'],key=lambda p:(p['Q'],p['R'])):
        _keys(p,('Q','R')); records.append(dict(role='exploration_position',q=p['Q'],r=p['R']))
    scout=world['LastScouting']
    if scout is not None:
        _keys(scout,('EnemyId','EnemyColor','Q','R','GuardSlot','RuinToken','RuinDetails'))
        records.append(dict(role='scouting',**scout))
    signature=hashlib.sha256(json.dumps(names,separators=(',',':')).encode()).hexdigest()
    return dict(names=tuple(names),numeric=np.asarray(values,np.float32),records=records,
                numeric_schema_sha256=signature,
                excluded_audit_fields=('Dummy.Discard[*].PhysicalId',),
                status='world_projection_only_entity_tensor_binding_pending')
